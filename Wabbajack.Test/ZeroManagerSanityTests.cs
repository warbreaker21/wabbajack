using System.Collections.Generic;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Test
{
    public class ZeroManagerSanityTests : ACompilerTest
    {
        public ZeroManagerSanityTests(ITestOutputHelper helper) : base(helper)
        {
            
        }

        [Fact]
        public async Task CanCompileABasicGame()
        {
            utils.Game = Game.Witcher3;
            var metaData = Game.Witcher3.MetaData();

            foreach (var file in metaData.RequiredFiles!)
            {
                await file.RelativeTo(metaData.GameLocation()).CopyToAsync(file.RelativeTo(utils.SourceFolder), true);
            }

            var rndData = utils.RandomData(1024);
            await "someFile".RelativeTo(utils.SourceFolder).WriteAllBytesAsync(rndData);

            await utils.ConfigureZeroManager();
            
            await utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", rndData}});

            await CompileAndInstallZeroManager();


            foreach (var file in metaData.RequiredFiles!)
            {
                Assert.True(file.RelativeTo(utils.InstallFolder).Exists, $"Required file `{file}` doesn't exist");
            }
        }

    }
}
