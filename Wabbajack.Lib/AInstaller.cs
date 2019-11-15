using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.CSP;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public abstract class AInstaller
    {
        public ModList ModList { get; internal set; }
        public Dictionary<string, string> HashedArchives { get; protected set; }

        public Context VFS { get; set; } = new Context();

        public string Outputfolder { get; internal set; }
        
        public string ModListArchive { get; internal set; }

        public string GameFolder { get; set; }


        public abstract string DownloadFolder { get; set; }

        protected void Info(string msg)
        {
            Utils.Log(msg);
        }

        protected void Status(string msg)
        {
            WorkQueue.Report(msg, 0);
        }

        protected void Status(string msg, int progress)
        {
            WorkQueue.Report(msg, progress);
        }

        protected void Error(string msg)
        {
            Utils.Log(msg);
            throw new Exception(msg);
        }

        protected async Task InstallArchives()
        {
            Info("Installing Archives");
            Info("Grouping Install Files");
            var grouped = ModList.Directives
                .OfType<FromArchive>()
                .GroupBy(e => e.ArchiveHashPath[0])
                .ToDictionary(k => k.Key);
            var archives = ModList.Archives
                .Select(a => new { Archive = a, AbsolutePath = HashedArchives.GetOrDefault(a.Hash) })
                .Where(a => a.AbsolutePath != null)
                .ToList();

            Info("Installing Archives");
            await archives.ToChannel()
                .UnorderedPipelineAsync(async a =>
                {
                    await InstallArchive(a.Archive, a.AbsolutePath, grouped[a.Archive.Hash]);
                    return a;
                })
                .TakeAll();
        }

        private async Task InstallArchive(Archive archive, string absolutePath, IGrouping<string, FromArchive> grouping)
        {
            Status($"Extracting {archive.Name}");

            var vfiles = grouping.Select(g =>
            {
                var file = VFS.Index.ByArchiveHashPath(g.ArchiveHashPath);
                g.FromFile = file;
                return g;
            }).ToList();

            var on_finish = await VFS.Stage(vfiles.Select(f => f.FromFile).Distinct());


            Status($"Copying files for {archive.Name}");

            void CopyFile(string from, string to, bool use_move)
            {
                if (File.Exists(to))
                {
                    var fi = new FileInfo(to);
                    if (fi.IsReadOnly)
                        fi.IsReadOnly = false;
                    File.Delete(to);
                }

                if (File.Exists(from))
                {
                    var fi = new FileInfo(from);
                    if (fi.IsReadOnly)
                        fi.IsReadOnly = false;
                }


                if (use_move)
                    File.Move(from, to);
                else
                    File.Copy(from, to);
            }

            vfiles.GroupBy(f => f.FromFile)
                  .DoIndexed((idx, group) =>
                  {
                      Utils.Status("Installing files", idx * 100 / vfiles.Count);
                      var first_dest = Path.Combine(Outputfolder, group.First().To);
                      CopyFile(group.Key.StagedPath, first_dest, true);

                      foreach (var copy in group.Skip(1))
                      {
                          var next_dest = Path.Combine(Outputfolder, copy.To);
                          CopyFile(first_dest, next_dest, false);
                      }

                  });

            Status("Unstaging files");
            on_finish();

            // Now patch all the files from this archive
            foreach (var to_patch in grouping.OfType<PatchedFromArchive>())
                using (var patch_stream = new MemoryStream())
                {
                    Status($"Patching {Path.GetFileName(to_patch.To)}");
                    // Read in the patch data

                    var patch_data = await LoadBytesFromPath(to_patch.PatchID);

                    var to_file = Path.Combine(Outputfolder, to_patch.To);
                    var old_data = new MemoryStream(File.ReadAllBytes(to_file));

                    // Remove the file we're about to patch
                    File.Delete(to_file);

                    // Patch it
                    using (var out_stream = File.OpenWrite(to_file))
                    {
                        BSDiff.Apply(old_data, () => new MemoryStream(patch_data), out_stream);
                    }

                    Status($"Verifying Patch {Path.GetFileName(to_patch.To)}");
                    var result_sha = to_file.FileHash();
                    if (result_sha != to_patch.Hash)
                        throw new InvalidDataException($"Invalid Hash for {to_patch.To} after patching");
                }
        }

        public async Task<byte[]> LoadBytesFromPath(string path)
        {
            using (var fs = new FileStream(ModListArchive, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            using (var ms = new MemoryStream())
            {
                var entry = ar.GetEntry(path);
                using (var e = entry.Open())
                    await e.CopyToAsync(ms);
                return ms.ToArray();
            }
        }
        public static ModList LoadFromFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var ar = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                var entry = ar.GetEntry("modlist");
                if (entry == null)
                {
                    entry = ar.GetEntry("modlist.json");
                    using (var e = entry.Open())
                        return e.FromJSON<ModList>();
                }
                using (var e = entry.Open())
                    return e.FromCERAS<ModList>(ref CerasConfig.Config);
            }
        }


        protected async Task InstallIncludedFiles()
        {
            Info("Writing inline files");
            var results = await ModList.Directives
                .OfType<InlineFile>()
                .ToChannel()
                .UnorderedPipelineAsync(async directive =>
                {
                    Status($"Writing included file {directive.To}");
                    var out_path = Path.Combine(Outputfolder, directive.To);
                    if (File.Exists(out_path)) File.Delete(out_path);
                    if (directive is RemappedInlineFile)
                        await WriteRemappedFile((RemappedInlineFile)directive);
                    else if (directive is CleanedESM)
                        await GenerateCleanedESM((CleanedESM)directive);
                    else
                        File.WriteAllBytes(out_path, await LoadBytesFromPath(directive.SourceDataID));
                    return directive;
                }).TakeAll();
        }


        private async Task WriteRemappedFile(RemappedInlineFile directive)
        {
            var data = Encoding.UTF8.GetString(await LoadBytesFromPath(directive.SourceDataID));

            data = data.Replace(Consts.GAME_PATH_MAGIC_BACK, GameFolder);
            data = data.Replace(Consts.GAME_PATH_MAGIC_DOUBLE_BACK, GameFolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.GAME_PATH_MAGIC_FORWARD, GameFolder.Replace("\\", "/"));

            data = data.Replace(Consts.MO2_PATH_MAGIC_BACK, Outputfolder);
            data = data.Replace(Consts.MO2_PATH_MAGIC_DOUBLE_BACK, Outputfolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.MO2_PATH_MAGIC_FORWARD, Outputfolder.Replace("\\", "/"));

            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_BACK, DownloadFolder);
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_DOUBLE_BACK, DownloadFolder.Replace("\\", "\\\\"));
            data = data.Replace(Consts.DOWNLOAD_PATH_MAGIC_FORWARD, DownloadFolder.Replace("\\", "/"));

            File.WriteAllText(Path.Combine(Outputfolder, directive.To), data);
        }

        private async Task GenerateCleanedESM(CleanedESM directive)
        {
            var filename = Path.GetFileName(directive.To);
            var game_file = Path.Combine(GameFolder, "Data", filename);
            Info($"Generating cleaned ESM for {filename}");
            if (!File.Exists(game_file)) throw new InvalidDataException($"Missing {filename} at {game_file}");
            Status($"Hashing game version of {filename}");
            var sha = game_file.FileHash();
            if (sha != directive.SourceESMHash)
                throw new InvalidDataException(
                    $"Cannot patch {filename} from the game folder hashes don't match have you already cleaned the file?");

            var patch_data = await LoadBytesFromPath(directive.SourceDataID);
            var to_file = Path.Combine(Outputfolder, directive.To);
            Status($"Patching {filename}");
            using (var output = File.OpenWrite(to_file))
            using (var input = File.OpenRead(game_file))
            {
                BSDiff.Apply(input, () => new MemoryStream(patch_data), output);
            }
        }

        protected async Task HashArchives()
        {
            var all = await Directory.EnumerateFiles(DownloadFolder)
                .Where(e => !e.EndsWith(".sha"))
                .ToChannel()
                .UnorderedPipelineAsync(async e => (await HashArchive(e), e))
                .TakeAll();

            HashedArchives = all.OrderByDescending(e => File.GetLastWriteTime(e.Item2))
                .GroupBy(e => e.Item1)
                .Select(e => e.First())
                .ToDictionary(e => e.Item1, e => e.Item2);
        }

        protected async Task<string> HashArchive(string e)
        {
            var cache = e + ".sha";
            if (cache.FileExists() && new FileInfo(cache).LastWriteTime >= new FileInfo(e).LastWriteTime)
                return File.ReadAllText(cache);

            Status($"Hashing {Path.GetFileName(e)}");
            File.WriteAllText(cache, await e.FileHashAsync());
            return await HashArchive(e);
        }

        protected async Task DownloadMissingArchives(List<Archive> missing, bool download = true)
        {
            if (download)
            {
                foreach (var a in missing.Where(a => a.State.GetType() == typeof(ManualDownloader.State)))
                {
                    var output_path = Path.Combine(DownloadFolder, a.Name);
                    a.State.Download(a, output_path);
                }
            }

            await missing.Where(a => a.State.GetType() != typeof(ManualDownloader.State))
                .PMapSync(archive =>
                {
                    Info($"Downloading {archive.Name}");
                    var output_path = Path.Combine(DownloadFolder, archive.Name);

                    if (!download) return DownloadArchive(archive, download);
                    if (output_path.FileExists())
                        File.Delete(output_path);

                    return DownloadArchive(archive, download);
                });
        }
        public bool DownloadArchive(Archive archive, bool download)
        {
            try
            {
                archive.State.Download(archive, Path.Combine(DownloadFolder, archive.Name));
            }
            catch (Exception ex)
            {
                Utils.Log($"Download error for file {archive.Name}");
                Utils.Log(ex.ToString());
                return false;
            }

            return false;
        }
    }
}
