using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Common.CSP;
using Wabbajack.Lib.Downloaders;
using Wabbajack.VirtualFileSystem;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Lib
{
    public class VortexInstaller : AInstaller
    {
        public GameMetaData GameInfo { get; internal set; }

        public string StagingFolder { get; set; }
        public override string DownloadFolder { get; set; }
        public bool IgnoreMissingFiles { get; internal set; }

        public VortexInstaller(string archive, ModList modList)
        {
            ModListArchive = archive;
            ModList = modList;

            // TODO: only for testing
            IgnoreMissingFiles = true;

            GameInfo = GameRegistry.Games[ModList.GameType];
        }

        public async Task Install()
        {
            Directory.CreateDirectory(DownloadFolder);

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
            //InctallIncludedDownloadMetas();

            Info("Installation complete! You may exit the program.");
        }

        private void BuildFolderStructure()
        {
            Info("Building Folder Structure");
            ModList.Directives
                .OfType<FromArchive>()
                .Select(d => Path.Combine(StagingFolder, Path.GetDirectoryName(d.To)))
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
