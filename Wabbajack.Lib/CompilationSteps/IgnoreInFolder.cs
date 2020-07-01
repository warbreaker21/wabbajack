using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreInFolder : ACompilationStep
    {
        private AbsolutePath _folder;

        public IgnoreInFolder(ACompiler compiler, AbsolutePath folder) : base(compiler)
        {
            _folder = folder;
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            return source.AbsolutePath.InFolder(_folder) ? source.Ignore($"Inside ignored folder {_folder}") : null;
        }
    }
}
