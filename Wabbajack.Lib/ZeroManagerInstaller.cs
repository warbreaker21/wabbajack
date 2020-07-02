using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;

namespace Wabbajack.Lib
{
    public class ZeroManagerInstaller : AInstaller
    {
        public ZeroManagerInstaller(AbsolutePath archive, ModList modList, AbsolutePath outputFolder, AbsolutePath downloadFolder, Game game, bool warnOnOverride = true) 
            : base(archive, modList, outputFolder, downloadFolder, null,10, game)
        {
            WarnOnOverwrite = warnOnOverride;
        }

        public bool WarnOnOverwrite { get; set; }

        protected override async Task<bool> _Begin(CancellationToken cancel)
        { 
            if (cancel.IsCancellationRequested) return false;
            await Metrics.Send(Metrics.BeginInstall, ModList.Name);
            Utils.Log("Configuring Processor");

            Queue.SetActiveThreadsObservable(ConstructDynamicNumThreads(await RecommendQueueSize()));

            if (!Game.IsInstalled)
            {
                var otherGame = Game.CommonlyConfusedWith.Where(g => g.MetaData().IsInstalled).Select(g => g.MetaData()).FirstOrDefault();
                if (otherGame != null)
                {
                    await Utils.Log(new CriticalFailureIntervention(
                            $"In order to do a proper install Wabbajack needs to know where your {Game.HumanFriendlyGameName} folder resides. However this game doesn't seem to be installed, we did however find a installed " +
                            $"copy of {otherGame.HumanFriendlyGameName}, did you install the wrong game?",
                            $"Could not locate {Game.HumanFriendlyGameName}"))
                        .Task;
                }
                else
                {
                    await Utils.Log(new CriticalFailureIntervention(
                            $"In order to do a proper install Wabbajack needs to know where your {Game.HumanFriendlyGameName} folder resides. However this game doesn't seem to be installed",
                            $"Could not locate {Game.HumanFriendlyGameName}"))
                        .Task;
                }

                Utils.Log("Exiting because we couldn't find the game folder.");
                return false;
            }

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Validating Modlist");
            await ValidateModlist.RunValidation(ModList);

            OutputFolder.CreateDirectory();
            DownloadFolder.CreateDirectory();

            if (OutputFolder.Combine(Consts.MO2ModFolderName).IsDirectory && WarnOnOverwrite)
            {
                if ((await Utils.Log(new ConfirmUpdateOfExistingInstall { ModListName = ModList.Name, OutputFolder = OutputFolder }).Task) == ConfirmUpdateOfExistingInstall.Choice.Abort)
                {
                    Utils.Log("Exiting installation at the request of the user, existing mods folder found.");
                    return false;
                }
            }

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Optimizing ModList");
            await OptimizeModlist();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Hashing Archives");
            await HashArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Downloading Missing Archives");
            await DownloadArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Hashing Remaining Archives");
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

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Extracting Modlist contents");
            await ExtractModlist();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Priming VFS");
            await PrimeVFS();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Building Folder Structure");
            BuildFolderStructure();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Archives");
            await InstallArchives();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Included files");
            await InstallIncludedFiles();

            if (cancel.IsCancellationRequested) return false;
            UpdateTracker.NextStep("Installing Archive Metas");
            await InstallIncludedDownloadMetas();

            UpdateTracker.NextStep("Installation complete! You may exit the program.");
            await Metrics.Send(Metrics.FinishInstall, ModList.Name);

            return true;
        }
        
        protected async Task InstallIncludedFiles()
        {
            Info("Writing inline files");
            await ModList.Directives
                .OfType<InlineFile>()
                .PMap(Queue, async directive =>
                {
                    Status($"Writing included file {directive.To}");
                    var outPath = OutputFolder.Combine(directive.To);
                    await outPath.DeleteAsync();

                    switch (directive)
                    {
                        default:
                            await outPath.WriteAllBytesAsync(await LoadBytesFromPath(directive.SourceDataID));
                            break;
                    }
                });
        }

        public override ModManager ModManager { get; }
    }
}
