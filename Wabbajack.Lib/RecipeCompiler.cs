using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.CompilationSteps;
using Wabbajack.Lib.ModListRegistry;

namespace Wabbajack.Lib
{
    public class RecipeCompiler : MO2Compiler
    {
        public string ModName { get; }
        public AbsolutePath ModFolder { get; set; }

        public RecipeCompiler(AbsolutePath mo2Folder, string mo2Profile, AbsolutePath outputFile, string modName) : base(mo2Folder, mo2Profile, outputFile)
        {
            ModName = modName;
            ModFolder = mo2Folder.Combine("mods", modName);
        }


        public override IEnumerable<ICompilationStep> MakeStack() 
        {
            return base.MakeStack()
                .Where(s => !(s is IgnoreDisabledMods) && !(s is IgnoreOtherProfiles))
                .Cons(new OnlyMod(this));
        }

        protected override async Task Export()
        {
            Utils.Log($"Exporting Recipe to {ModListOutputFile}");

            // Modify readme and ModList image to relative paths if they exist
            if (ModListImage.Exists)
            {
                ModList.Image = (RelativePath)"modlist-image.png";
            }

            await using (var of = await ModListOutputFolder.Combine("recipe").Create()) 
                MakeRecipe(ModList).ToJson(of);

            await ModListOutputFile.DeleteAsync();

            await using (var fs = await ModListOutputFile.Create())
            {
                using (var za = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    await ModListOutputFolder.EnumerateFiles()
                        .DoProgress("Compressing Recipe",
                    async f =>
                    {
                        var ze = za.CreateEntry((string)f.FileName);
                        await using var os = ze.Open();
                        await using var ins = await f.OpenRead();
                        await ins.CopyToAsync(os);
                    });
                }
            }

            Utils.Log("Exporting Recipe metadata");
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


            Utils.Log("Removing Recipe staging folder");
            await Utils.DeleteDirectory(ModListOutputFolder);
        }

        private Recipe MakeRecipe(ModList modList)
        {
            return new Recipe
            {
                Version = modList.Version,
                Archives = modList.Archives,
                Directives = modList.Directives.Select(d =>
                {
                    d.To = d.To.RelativeTo(MO2Folder).RelativeTo(ModFolder);
                    return d;
                }).ToList(),
                Author = modList.Author,
                Name = ModName
            };
        }
    }
}
