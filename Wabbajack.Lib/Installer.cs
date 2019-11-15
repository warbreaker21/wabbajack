using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wabbajack.Common;
using Wabbajack.Common.CSP;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Wabbajack.Lib.Validation;
using Wabbajack.VirtualFileSystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class Installer : AInstaller
    {
        private string _downloadsFolder;

        public Installer(string archive, ModList mod_list, string output_folder)
        {
            ModListArchive = archive;
            Outputfolder = output_folder;
            ModList = mod_list;
        }

        private string _downloadFolder;
        public override string DownloadFolder
        {
            get => _downloadsFolder ?? Path.Combine(Outputfolder, "downloads");
            set => _downloadsFolder = value;
        }

        public bool IgnoreMissingFiles { get; internal set; }



        public async Task Install()
        {
            var game = GameRegistry.Games[ModList.GameType];

            if (GameFolder == null)
                GameFolder = game.GameLocation;

            if (GameFolder == null)
            {
                MessageBox.Show(
                    $"In order to do a proper install Wabbajack needs to know where your {game.MO2Name} folder resides. We tried looking the" +
                    "game location up in the windows registry but were unable to find it, please make sure you launch the game once before running this installer. ",
                    "Could not find game location", MessageBoxButton.OK);
                Utils.Log("Exiting because we couldn't find the game folder.");
                return;
            }

            ValidateGameESMs();
            await ValidateModlist.RunValidation(ModList);

            Directory.CreateDirectory(Outputfolder);
            Directory.CreateDirectory(DownloadFolder);

            if (Directory.Exists(Path.Combine(Outputfolder, "mods")))
            {
                if (MessageBox.Show(
                        "There already appears to be a Mod Organizer 2 install in this folder, are you sure you wish to continue" +
                        " with installation? If you do, you may render both your existing install and the new modlist inoperable.",
                        "Existing MO2 installation in install folder",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation) == MessageBoxResult.No)
                {
                    Utils.Log("Existing installation at the request of the user, existing mods folder found.");
                    return;
                }
            }


            await HashArchives();
            await DownloadArchives();
            await HashArchives();

            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            if (missing.Count > 0)
            {
                foreach (var a in missing)
                    Info($"Unable to download {a.Name}");
                if (IgnoreMissingFiles)
                    Info("Missing some archives, but continuing anyways at the request of the user");
                else
                    Error("Cannot continue, was unable to download one or more archives");
            }

            await VFS.IntegrateFromPortable(ModList.PortableFiles, HashedArchives);

            BuildFolderStructure();
            await InstallArchives();
            await InstallIncludedFiles();
            await InstallIncludedDownloadMetas();
            await BuildBSAs();

            await zEditIntegration.GenerateMerges(this);

            Info("Installation complete! You may exit the program.");
            // Removed until we decide if we want this functionality
            // Nexus devs weren't sure this was a good idea, I (halgari) agree.
            //AskToEndorse();
        }

        private async Task InstallIncludedDownloadMetas()
        {
            await ModList.Directives
                   .OfType<ArchiveMeta>()
                   .ToChannel()
                   .UnorderedPipelineAsync(async directive =>
                   {
                       Status($"Writing included .meta file {directive.To}");
                       var out_path = Path.Combine(DownloadFolder, directive.To);
                       if (File.Exists(out_path)) File.Delete(out_path);
                       File.WriteAllBytes(out_path, await LoadBytesFromPath(directive.SourceDataID));
                       return directive;
                   }).TakeAll();
        }

        private void ValidateGameESMs()
        {
            foreach (var esm in ModList.Directives.OfType<CleanedESM>().ToList())
            {
                var filename = Path.GetFileName(esm.To);
                var game_file = Path.Combine(GameFolder, "Data", filename);
                Utils.Log($"Validating {filename}");
                var hash = game_file.FileHash();
                if (hash != esm.SourceESMHash)
                {
                    Utils.Error("Game ESM hash doesn't match, is the ESM already cleaned? Please verify your local game files.");
                }
            }
        }

        private async Task BuildBSAs()
        {
            var bsas = ModList.Directives.OfType<CreateBSA>().ToList();
            Info($"Building {bsas.Count} bsa files");

            foreach (var bsa in bsas)
            {
                Status($"Building {bsa.To}");
                var source_dir = Path.Combine(Outputfolder, Consts.BSACreationDir, bsa.TempID);

                using (var a = bsa.State.MakeBuilder())
                {
                    var results = await bsa.FileStates
                        .ToChannel()
                        .UnorderedPipelineSync(async state =>
                        {
                            Status($"Adding {state.Path} to BSA");
                            using (var fs = File.OpenRead(Path.Combine(source_dir, state.Path)))
                            {
                                await a.AddFile(state, fs);
                            }
                        }).TakeAll();

                    Info($"Writing {bsa.To}");
                    await a.Build(Path.Combine(Outputfolder, bsa.To));
                }
            }
            
            var bsa_dir = Path.Combine(Outputfolder, Consts.BSACreationDir);
            if (Directory.Exists(bsa_dir))
            {
                Info($"Removing temp folder {Consts.BSACreationDir}");
                Directory.Delete(bsa_dir, true, true);
            }
        }

        private void BuildFolderStructure()
        {
            Info("Building Folder Structure");
            ModList.Directives
                .Select(d => Path.Combine(Outputfolder, Path.GetDirectoryName(d.To)))
                .ToHashSet()
                .Do(f =>
                {
                    if (Directory.Exists(f)) return;
                    Directory.CreateDirectory(f);
                });
        }

        private async Task DownloadArchives()
        {
            var missing = ModList.Archives.Where(a => !HashedArchives.ContainsKey(a.Hash)).ToList();
            Info($"Missing {missing.Count} archives");

            Info("Getting Nexus API Key, if a browser appears, please accept");

            var dispatchers = missing.Select(m => m.State.GetDownloader()).Distinct();

            foreach (var dispatcher in dispatchers)
                dispatcher.Prepare();
            
            await DownloadMissingArchives(missing);
        }
    }
}
