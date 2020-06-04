using Wabbajack.Common;

namespace Wabbajack.Lib
{
    public class RecipeInstaller : MO2Installer
    {
        public RecipeInstaller(AbsolutePath archive, Recipe modList, AbsolutePath outputFolder, AbsolutePath downloadFolder, SystemParameters parameters) : base(archive, modList, outputFolder, downloadFolder, parameters)
        {
            SetPortable = false;
        }
    }
}
