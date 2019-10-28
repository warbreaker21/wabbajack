using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.ModListRegistry;
using Wabbajack.Lib.Validation;
using File = Alphaleonis.Win32.Filesystem.File;

namespace Wabbajack.Updater
{
    class Program
    {
        static void Main(string[] args)
        {
            Utils.SetLoggerFn(Console.WriteLine);

            WorkQueue.Init((a, b, c) => {}, (a, b) => {});

            Utils.Log("Wabbajack Validator");

            if (!Directory.Exists(Consts.ModListDownloadFolder))
                Directory.CreateDirectory(Consts.ModListDownloadFolder);

            var modlists = DownloadModlists();
            modlists.Do(m => ValidateModlist(m));
        }

        private static void ValidateModlist(ModlistMetadata data)
        {
            Utils.Log($"Validating {data.Title} by {data.Author}");

            var state = DownloadDispatcher.ResolveArchive(data.Links.Download);
            var dest = Path.Combine(Consts.ModListDownloadFolder, data.Title + Consts.ModlistExtension);

            if (!File.Exists(dest))
            {
                Utils.Log($"Downloading {data.Links.Download} to {dest}");
                state.Download(new Archive {Name = data.Title}, dest);
            }

            try
            {
                Utils.Log($"Loading: {dest}");
                var inst = Installer.LoadFromFile(dest);

                Utils.Log($"Verifying {inst.Archives.Count} archives");
                inst.Archives.PMap(a => { a.State.Verify(); });
            }
            catch (Exception ex)
            {
                Fail(data, ex.ToString());
            }
        }

        private static void Fail(ModlistMetadata data, string msg)
        {
            Utils.Log($"Failing: {data.Title}");
            Utils.Log(msg);
            var results_dir = Environment.GetEnvironmentVariable("MODLISTUPDATE_RESULTS") ?? ".";
            File.WriteAllLines(Path.Combine(results_dir, data.Title+".md"), new List<string>
            {
                "### Status: Failed",
                msg,
            });
        }

        private static List<ModlistMetadata> DownloadModlists()
        {
            Utils.Log("Loading modlist metadata");
            var result = ModlistMetadata.LoadFromGithub();
            Utils.Log($"Found {result.Count} modlists");
            return result;
        }
    }
}
