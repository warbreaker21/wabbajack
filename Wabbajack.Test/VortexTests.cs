using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Lib.CompilationSteps;

namespace Wabbajack.Test
{
    [TestClass]
    public class VortexTests : AVortexCompilerTest
    {
        [TestMethod]
        public async Task TestVortexStackSerialization()
        {
            utils.AddMod("test");
            utils.Configure();

            var vortexCompiler = await ConfigureAndRunCompiler();
            var stack = vortexCompiler.MakeStack();

            var serialized = Serialization.Serialize(stack);
            var rounded = Serialization.Serialize(Serialization.Deserialize(serialized, vortexCompiler));

            Assert.AreEqual(serialized, rounded);
            Assert.IsNotNull(vortexCompiler.GetStack());
        }
    }
}
