using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wabbajack.Common;
using Wabbajack.Lib;

namespace Wabbajack.Test
{
    public abstract class AVortexCompilerTest
    {
        public TestContext TestContext { get; set; }
        protected TestUtils utils { get; set; }


        [TestInitialize]
        public void TestInitialize()
        {
            Consts.TestMode = true;

            utils = new TestUtils
            {
                GameName = "darkestdungeon"
            };

            Utils.LogMessages.Subscribe(f => TestContext.WriteLine(f));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            utils.Dispose();
        }

        protected async Task<VortexCompiler> ConfigureAndRunCompiler()
        {
            var vortexCompiler = MakeCompiler();
            vortexCompiler.DownloadsFolder = utils.DownloadsFolder;
            vortexCompiler.StagingFolder = utils.InstallFolder;
            Directory.CreateDirectory(utils.InstallFolder);
            Assert.IsTrue(await vortexCompiler.Compile());
            return vortexCompiler;
        }

        protected VortexCompiler MakeCompiler()
        {
            var vortexCompiler = new VortexCompiler(utils.GameName, utils.GameFolder);
            return vortexCompiler;
        }

        protected async Task<ModList> CompileAndInstall()
        {
            var vortexCompiler = await ConfigureAndRunCompiler();
            await Install(vortexCompiler);
            return vortexCompiler.ModList;
        }

        protected async Task Install(VortexCompiler vortexCompiler)
        {
            var modList = Installer.LoadFromFile(vortexCompiler.ModListOutputFile);
            var installer = new Installer(vortexCompiler.ModListOutputFile, modList, utils.InstallFolder)
            {
                DownloadFolder = utils.DownloadsFolder,
                GameFolder = utils.GameFolder,
            };
            await installer.Install();
        }
    }
}
