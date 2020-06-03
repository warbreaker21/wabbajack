using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class OnlyMod : ACompilationStep
    {
        private string _modName;
        private AbsolutePath _modPath;

        public OnlyMod(ACompiler compiler) : base(compiler)
        {
            _modName = ((RecipeCompiler)compiler).ModName;
            _modPath = ((RecipeCompiler)compiler).MO2Folder.Combine("mods", _modName);
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            return !source.AbsolutePath.InFolder(_modPath) ? source.EvolveTo<IgnoredDirectly>() : null;
        }

        public override IState GetState()
        {
            throw new System.NotImplementedException();
        }
    }
}
