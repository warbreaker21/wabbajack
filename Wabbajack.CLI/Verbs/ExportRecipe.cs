using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.CLI.Verbs
{
    [Verb("export-recipe", HelpText = @"Export a recipe as a .wabbajack_recipe file")]
    public class ExportRecipe : AVerb
    {
        [Option('m', "mod-folder", Required = true, HelpText = @"Path of the mod to export")]
        public string ModFolder { get; set; } = "";

        [Option('o', "output-path", Required = true, HelpText = @"Output filename")]
        public string OutputFileName { get; set; } = "";

        private string ClampString(string s, int max)
        {
            return s.Substring(0, Math.Min(s.Length, max));
        }

        protected override async Task<ExitCode> Run()
        {
            var modPath = ModFolder.RelativeTo(AbsolutePath.EntryPoint);
            var modName = (string)modPath.FileName;
            var mo2Folder = modPath.Parent.Parent;
            var mo2Profile = mo2Folder.Combine("profiles")
                .EnumerateDirectories().First(d => d.Combine("modlist.txt").Exists);
            var compiler = new RecipeCompiler(mo2Folder, (string)mo2Profile.FileName, OutputFileName.RelativeTo(AbsolutePath.EntryPoint), modName);
            compiler.PercentCompleted.Subscribe(s => Console.Write(ClampString($"\r{s}% Complete", 40)));
            await compiler.Begin();
            return ExitCode.Ok;
        }
    }
}
