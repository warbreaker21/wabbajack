using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Wabbajack.Common;

namespace Wabbajack.Downloaders
{
    public class RemotingBackend
    {

        private DriverService _driverService;

        public void Startup()
        {
            lock (this)
            {
                if (_driverService != null) return;
                Utils.Log("Starting Remoting backend");
                ExtractIfNotExists("chromedriver.exe");
                var dir = Directory.GetCurrentDirectory();
                _driverService = ChromeDriverService.CreateDefaultService(dir, "chromedriver.exe");
                _driverService.HideCommandPromptWindow = true;
                _driverService.Start();

                ChildProcessTracker.AddProcess(Process.GetProcesses().Single(p => p.Id == _driverService.ProcessId));
            }
        }

        public IWebDriver GetDriver()
        {
            switch (_driverService)
            {
                case ChromeDriverService service:
                    var options = new ChromeOptions();
                    options.AddArguments(new List<string>() {
                        "--silent-launch",
                        "--no-startup-window",
                        "no-sandbox",
                        "headless",});

                    return new ChromeDriver(service, options);
            }

            return null;
        }

        private void ExtractIfNotExists(string file)
        {
            if (File.Exists(file)) return;
            
            using (var ins = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Wabbajack.Downloaders." + file))
            using(var ous = File.OpenWrite(file))
            {
                ous.SetLength(0);
                ins.CopyTo(ous);
            }
        }

        public void Shutdown()
        {
            lock (this)
            {
                Utils.Log("Shutting down Remoting backend");

                if (_driverService != null)
                {
                    _driverService.Dispose();
                    _driverService = null;
                }
            }
        }
    }
}
