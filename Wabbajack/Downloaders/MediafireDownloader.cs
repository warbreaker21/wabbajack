using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using Wabbajack.Common;
using Wabbajack.Validation;

namespace Wabbajack.Downloaders
{
    public class MediafireDownloader : IDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            var url = archive_ini?.General?.directURL;

            if (url != null && url.StartsWith("http://www.mediafire.com/file/"))
            {
                return new State {Url = url};
            }

            return null;
        }

        public void Prepare()
        {
        }

        public class State : AbstractDownloadState
        {
            public string Url { get; set; }
            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return whitelist.AllowedPrefixes.Any(u => Url.StartsWith(u));
            }

            public override void Download(Archive a, string destination)
            {
                var result = GetUrlAndClient();
                result(destination);
            }

            private Action<string> GetUrlAndClient()
            {
                using (var driver = RemotingBackend.GetDriver())
                {
                    Utils.Status($"Navigating to {Url}");
                    driver.Url = Url;
                    var href = driver.FindElement(By.CssSelector("a.input")).GetAttribute("href");
                    var client = driver.ConvertToHTTPClient();
                    return dest => client.DownloadUrl(href, dest);
                }
            }

            public override bool Verify()
            {
                try
                {
                    GetUrlAndClient();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public override IDownloader GetDownloader()
            {
                return DownloadDispatcher.GetInstance<MediafireDownloader>();
            }

            public override string GetReportEntry(Archive a)
            {
                return $"* [{a.Name}]({Url})";
            }
        }
    }
}
