using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using Compression.BSA;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.NexusApi;
using Wabbajack.VirtualFileSystem;

namespace Wabbajack.Lib
{
    public abstract class ACompiler : ABatchProcessor
    {
        public string? ModListName, ModListAuthor, ModListDescription, ModListWebsite, ModlistReadme;
        public Version? ModlistVersion;
        public AbsolutePath ModListImage;
        public bool ModlistIsNSFW;
        protected Version? WabbajackVersion;
        
        public AbsolutePath SourcePath { get; set; }
        public AbsolutePath DownloadsPath { get; set; }

        public abstract AbsolutePath VFSCacheName { get; }
        //protected string VFSCacheName => Path.Combine(Consts.LocalAppDataPath, $"vfs_compile_cache.bin");
        /// <summary>
        /// A stream of tuples of ("Update Title", 0.25) which represent the name of the current task
        /// and the current progress.
        /// </summary>
        public IObservable<(string, float)> ProgressUpdates => _progressUpdates;
        protected readonly Subject<(string, float)> _progressUpdates = new Subject<(string, float)>();

        public abstract ModManager ModManager { get; }

        public abstract AbsolutePath GamePath { get; }

        public virtual AbsolutePath ModListOutputFolder => ((RelativePath)"output_folder").RelativeToEntryPoint();
        public AbsolutePath ModListOutputFile { get; set; }

        public bool IgnoreMissingFiles { get; set; }

        public List<Archive> SelectedArchives { get; protected set; } = new List<Archive>();
        public List<Directive> InstallDirectives { get; protected set; } = new List<Directive>();
        public List<RawSourceFile> AllFiles { get; protected set; } = new List<RawSourceFile>();
        public ModList ModList = new ModList();

        public List<IndexedArchive> IndexedArchives = new List<IndexedArchive>();
        public Dictionary<AbsolutePath, IndexedArchive> ArchivesByFullPath { get; set; } = new Dictionary<AbsolutePath, IndexedArchive>();
        
        public Dictionary<Hash, IEnumerable<VirtualFile>> IndexedFiles = new Dictionary<Hash, IEnumerable<VirtualFile>>();

        public ACompiler(int steps, AbsolutePath sourcePath, AbsolutePath downloadsPath, Game compilingGame, AbsolutePath modlistOutputFile)
            : base(steps)
        {
            SourcePath = sourcePath;
            DownloadsPath = downloadsPath;
            CompilingGame = compilingGame;
            ModListOutputFile = modlistOutputFile;
        }

        public Game CompilingGame { get; set; }
        public GameMetaData CompilingGameMeta => CompilingGame.MetaData();

        public static void Info(string msg)
        {
            Utils.Log(msg);
        }

        public void Status(string msg)
        {
            Queue.Report(msg, Percent.Zero);
        }

        public static void Error(string msg)
        {
            Utils.Log(msg);
            throw new Exception(msg);
        }

        internal RelativePath IncludeId()
        {
            return RelativePath.RandomFileName();
        }

        internal async Task<RelativePath> IncludeFile(byte[] data)
        {
            var id = IncludeId();
            await ModListOutputFolder.Combine(id).WriteAllBytesAsync(data);
            return id;
        }

        internal AbsolutePath IncludeFile(out RelativePath id)
        {
            id = IncludeId();
            return ModListOutputFolder.Combine(id);
        }

        internal async Task<RelativePath> IncludeFile(string data)
        {
            var id = IncludeId();
            await ModListOutputFolder.Combine(id).WriteAllTextAsync(data);
            return id;
        }
        
        internal async Task<RelativePath> IncludeFile(AbsolutePath data)
        {
            var id = IncludeId();
            await data.CopyToAsync(ModListOutputFolder.Combine(id));
            return id;
        }

        
        internal async Task<(RelativePath, AbsolutePath)> IncludeString(string str)
        {
            var id = IncludeId();
            var fullPath = ModListOutputFolder.Combine(id);
            await fullPath.WriteAllTextAsync(str);
            return (id, fullPath);
        }

        public async Task<bool> GatherMetaData()
        {
            Utils.Log($"Getting meta data for {SelectedArchives.Count} archives");
            await SelectedArchives.PMap(Queue, async a =>
            {
                if (a.State is IMetaState metaState)
                {
                    if (metaState.URL == null || metaState.URL == Consts.WabbajackOrg)
                        return;

                    var b = await metaState.LoadMetaData();
                    Utils.Log(b
                        ? $"Getting meta data for {a.Name} was successful!"
                        : $"Getting meta data for {a.Name} failed!");
                }
                else
                {
                    Utils.Log($"Archive {a.Name} is not an AbstractMetaState!");
                }
            });

            return true;
        }

        public async Task ExportModList()
        {
            Utils.Log($"Exporting ModList to {ModListOutputFile}");

            // Modify readme and ModList image to relative paths if they exist
            if (ModListImage.Exists)
            {
                ModList.Image = (RelativePath)"modlist-image.png";
            }

            await using (var of = await ModListOutputFolder.Combine("modlist").Create()) 
                ModList.ToJson(of);

            await ClientAPI.SendModListDefinition(ModList);

            await ModListOutputFile.DeleteAsync();

            await using (var fs = await ModListOutputFile.Create())
            {
                using var za = new ZipArchive(fs, ZipArchiveMode.Create);
                await ModListOutputFolder.EnumerateFiles()
                    .DoProgress("Compressing ModList",
                async f =>
                {
                    var ze = za.CreateEntry((string)f.FileName);
                    await using var os = ze.Open();
                    await using var ins = await f.OpenRead();
                    await ins.CopyToAsync(os);
                });

                // Copy in modimage
                if (ModListImage.Exists)
                {
                    var ze = za.CreateEntry((string)ModList.Image);
                    await using var os = ze.Open();
                    await using var ins = await ModListImage.OpenRead();
                    await ins.CopyToAsync(os);
                }
            }

            Utils.Log("Exporting ModList metadata");
            var metadata = new DownloadMetadata
            {
                Size = ModListOutputFile.Size,
                Hash = await ModListOutputFile.FileHashAsync(),
                NumberOfArchives = ModList.Archives.Count,
                SizeOfArchives = ModList.Archives.Sum(a => a.Size),
                NumberOfInstalledFiles = ModList.Directives.Count,
                SizeOfInstalledFiles = ModList.Directives.Sum(a => a.Size)
            };
            metadata.ToJson(ModListOutputFile + ".meta.json");

            Utils.Log("Removing ModList staging folder");
            await Utils.DeleteDirectory(ModListOutputFolder);
        }

        public void GenerateManifest()
        {
            var manifest = new Manifest(ModList);
            manifest.ToJson(ModListOutputFile + ".manifest.json");
        }

        public async Task GatherArchives()
        {
            Info("Building a list of archives based on the files required");

            var hashes = InstallDirectives.OfType<FromArchive>()
                .Select(a => a.ArchiveHashPath.BaseHash)
                .Distinct();

            var archives = IndexedArchives.OrderByDescending(f => f.File.LastModified)
                .GroupBy(f => f.File.Hash)
                .ToDictionary(f => f.Key, f => f.First());

            SelectedArchives.SetTo(await hashes.PMap(Queue, hash => ResolveArchive(hash, archives)));
        }

        public async Task<Archive> ResolveArchive(Hash hash, IDictionary<Hash, IndexedArchive> archives)
        {
            if (archives.TryGetValue(hash, out var found))
            {
                return await ResolveArchive(found);
            }

            throw new ArgumentException($"No match found for Archive sha: {hash.ToBase64()} this shouldn't happen");
        }

        public async Task<Archive> ResolveArchive([NotNull] IndexedArchive archive)
        {
            if (!string.IsNullOrWhiteSpace(archive.Name)) 
                Utils.Status($"Checking link for {archive.Name}", alsoLog: true);

            if (archive.IniData == null)
                Error(
                    $"No download metadata found for {archive.Name}, please use MO2 to query info or add a .meta file and try again.");

            var result = new Archive(await DownloadDispatcher.ResolveArchive(archive.IniData));

            if (result.State == null)
                Error($"{archive.Name} could not be handled by any of the downloaders");

            result.Name = archive.Name ?? "";
            result.Hash = archive.File.Hash;
            result.Size = archive.File.Size;

            await result.State!.GetDownloader().Prepare();

            if (result.State != null && !await result.State.Verify(result))
                Error(
                    $"Unable to resolve link for {archive.Name}. If this is hosted on the Nexus the file may have been removed.");

            result.Meta = string.Join("\n", result.State!.GetMetaIni());

            
            return result;
        }

        public async Task<Directive> RunStack(IEnumerable<ICompilationStep> stack, RawSourceFile source)
        {
            Utils.Status($"Compiling {source.Path}");
            foreach (var step in stack)
            {
                var result = await step.Run(source);
                if (result != null) return result;
            }

            throw new InvalidDataException("Data fell out of the compilation stack");
        }

        public abstract IEnumerable<ICompilationStep> GetStack();
        public abstract IEnumerable<ICompilationStep> MakeStack();

        public static void PrintNoMatches(ICollection<NoMatch> noMatches)
        {
            const int max = 10;
            Info($"No match for {noMatches.Count} files");
            if (noMatches.Count > 0)
            {
                int count = 0;
                foreach (var file in noMatches)
                {
                    if (count++ < max)
                    {
                        Utils.Log($"     {file.To} - {file.Reason}");
                    }
                    else
                    {
                        Utils.LogStraightToFile($"     {file.To} - {file.Reason}");
                    }
                    if (count == max && noMatches.Count > max)
                    {
                        Utils.Log($"     ...");
                    }
                }
            }
        }

        public bool CheckForNoMatchExit(ICollection<NoMatch> noMatches)
        {
            if (noMatches.Count > 0)
            {
                if (IgnoreMissingFiles)
                {
                    Info("Continuing even though files were missing at the request of the user.");
                }
                else
                {
                    Info("Exiting due to no way to compile these files");
                    return true;
                }
            }
            return false;
        }
        
        protected async Task InferMetas(AbsolutePath folder)
        {
            async Task<bool> HasInvalidMeta(AbsolutePath filename)
            {
                var metaname = filename.WithExtension(Consts.MetaFileExtension);
                if (!metaname.Exists) return true;
                return await DownloadDispatcher.ResolveArchive(metaname.LoadIniFile()) == null;
            }

            var to_find = (await folder.EnumerateFiles()
                    .Where(f => f.Extension != Consts.MetaFileExtension && f.Extension !=Consts.HashFileExtension)
                    .PMap(Queue, async f => await HasInvalidMeta(f) ? f : default))
                .Where(f => f.Exists)
                .ToList();

            if (to_find.Count == 0) return;

            Utils.Log($"Attempting to infer {to_find.Count} metas from the server.");

            await to_find.PMap(Queue, async f =>
            {
                var vf = VFS.Index.ByRootPath[f];

                var meta = await ClientAPI.InferDownloadState(vf.Hash);

                if (meta == null)
                {
                    var nexus = await NexusApiClient.Get();

                    meta = await nexus.InferMeta(f, CompilingGame);
                    if (meta == null)
                    {
                        await vf.AbsoluteName.WithExtension(Consts.MetaFileExtension).WriteAllLinesAsync(
                            "[General]",
                            "unknownArchive=true");
                        return;
                    }
                }

                Utils.Log($"Inferred .meta for {vf.FullPath.FileName}, writing to disk");
                await vf.AbsoluteName.WithExtension(Consts.MetaFileExtension).WriteAllTextAsync(meta.GetMetaIniString());
            });
        }
        
        protected async Task CleanInvalidArchivesAndFillState()
        {
            var remove = (await IndexedArchives.PMap(Queue, async a =>
            {
                try
                {
                    a.State = (await ResolveArchive(a)).State;
                    return null;
                }
                catch
                {
                    return a;
                }
            })).NotNull().ToHashSet();

            if (remove.Count == 0)
                return;

            Utils.Log(
                $"Removing {remove.Count} archives from the compilation state, this is probably not an issue but reference this if you have compilation failures");
            remove.Do(r => Utils.Log($"Resolution failed for: {r.File.FullPath}"));
            IndexedArchives.RemoveAll(a => remove.Contains(a));
        }
        
                /// <summary>
        ///     Fills in the Patch fields in files that require them
        /// </summary>
        protected async Task BuildPatches()
        {
            Info("Gathering patch files");

            var toBuild = InstallDirectives.OfType<PatchedFromArchive>()
                .Where(p => p.Choices.Length > 0)
                .SelectMany(p => p.Choices.Select(c => new PatchedFromArchive
                    {
                        To = p.To,
                        Hash = p.Hash,
                        ArchiveHashPath = c.MakeRelativePaths(),
                        FromFile = c,
                        Size = p.Size,
                    }))
                .ToArray();

            if (toBuild.Length == 0) return;
 
            var groups = toBuild
                .Where(p => p.PatchID == default)
                .GroupBy(p => p.ArchiveHashPath.BaseHash)
                .ToList();

            Info($"Patching building patches from {groups.Count} archives");
            var absolutePaths = AllFiles.ToDictionary(e => e.Path, e => e.AbsolutePath);
            await groups.PMap(Queue, group => BuildArchivePatches(group.Key, group, absolutePaths));


            await InstallDirectives.OfType<PatchedFromArchive>()
                .Where(p => p.PatchID == default)
                .PMap(Queue, async pfa =>
                {
                    var patches = pfa.Choices
                        .Select(c => (Utils.TryGetPatch(c.Hash, pfa.Hash, out var data), data, c))
                        .ToArray();

                    if (patches.All(p => p.Item1))
                    {
                        var (_, bytes, file) = patches.OrderBy(f => f.data!.Length).First();
                        pfa.FromFile = file;
                        pfa.FromHash = file.Hash;
                        pfa.ArchiveHashPath = file.MakeRelativePaths();
                        pfa.PatchID = await IncludeFile(bytes!);
                    }
                });

            var firstFailedPatch = InstallDirectives.OfType<PatchedFromArchive>().FirstOrDefault(f => f.PatchID == default);
            if (firstFailedPatch != null)
                Error($"Missing patches after generation, this should not happen. First failure: {firstFailedPatch.FullPath}");
        }
                
        private async Task BuildArchivePatches(Hash archiveSha, IEnumerable<PatchedFromArchive> group,
            Dictionary<RelativePath, AbsolutePath> absolutePaths)
        {
            await using var files = await VFS.StageWith(@group.Select(g => VFS.Index.FileForArchiveHashPath(g.ArchiveHashPath)));
            var byPath = files.GroupBy(f => string.Join("|", f.FilesInFullPath.Skip(1).Select(i => i.Name)))
                .ToDictionary(f => f.Key, f => f.First());
            // Now Create the patches
            await @group.PMap(Queue, async entry =>
            {
                Info($"Patching {entry.To}");
                Status($"Patching {entry.To}");
                var srcFile = byPath[string.Join("|", entry.ArchiveHashPath.Paths)];
                await using var srcStream = await srcFile.OpenRead();
                await using var destStream = await LoadDataForTo(entry.To, absolutePaths);
                var patchSize = await Utils.CreatePatchCached(srcStream, srcFile.Hash, destStream, entry.Hash);
                Info($"Patch size {patchSize} for {entry.To}");
            });
        }

        private async Task<FileStream> LoadDataForTo(RelativePath to, Dictionary<RelativePath, AbsolutePath> absolutePaths)
        {
            if (absolutePaths.TryGetValue(to, out var absolute))
                return await absolute.OpenRead();

            if (!to.StartsWith(Consts.BSACreationDir))
                throw new ArgumentException($"Couldn't load data for {to}");

            var bsaId = (RelativePath)((string)to).Split('\\')[1];
            var bsa = InstallDirectives.OfType<CreateBSA>().First(b => b.TempID == bsaId);

            var a = await BSADispatch.OpenRead(SourcePath.Combine(bsa.To));
            var find = (RelativePath)Path.Combine(((string)to).Split('\\').Skip(2).ToArray());
            var file = a.Files.First(e => e.Path == find);
            var returnStream = new TempStream();
            await file.CopyDataTo(returnStream);
            returnStream.Position = 0;
            return returnStream;

        }
        
        protected async Task IncludeArchiveMetadata()
        {
            Utils.Log($"Including {SelectedArchives.Count} .meta files for downloads");
            await SelectedArchives.PMap(Queue, async a =>
            {
                if (a.State is GameFileSourceDownloader.State) return;
                
                var source = DownloadsPath.Combine(a.Name + Consts.MetaFileExtension);
                var ini = a.State.GetMetaIniString();
                var (id, fullPath) = await IncludeString(ini);
                InstallDirectives.Add(new ArchiveMeta
                {
                    SourceDataID = id,
                    Size = fullPath.Size,
                    Hash = await fullPath.FileHashAsync(),
                    To = source.FileName
                });
            });
        }
    }
}
