using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.Downloaders
{
    public class LoversLabDownloader : IDownloader
    {
        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {
            
            Uri url = DownloaderUtils.GetDirectURL(archive_ini);
            if (url == null || url.Host != "www.loverslab.com" || archive_ini?.General?.modName == null) return null;

            return new State
            {
                Url = url.ToString(),
                ModName = archive_ini?.General?.modName
            };
        }

        public void Prepare()
        {
            if (!IsLoggedIn().Result)
                throw new Exception("not logged into LL, TODO");
        }

        private async Task<bool> IsLoggedIn()
        {
            using (var driver = await Driver.Create())
            {
                await driver.NavigateTo(new Uri("https://www.loverslab.com"));
                var html = (await driver.GetAttr("#elUserLink", "innerHTML"));
                return html != null;
            }
        }

        public class State : AbstractDownloadState
        {
            public string Url { get; set; }
            public string ModName { get; set; }

            public override bool IsWhitelisted(ServerWhitelist whitelist)
            {
                return true;
            }

            public override void Download(Archive a, string destination)
            {
                var state = ResolveDownloadUrl().Result;
                state.Download(destination);
            }

            private async Task<HTTPDownloader.State> ResolveDownloadUrl()
            {
                using (var driver = await Driver.Create())
                {
                    await driver.NavigateTo(new Uri(Url));
                    var result = await driver.Eval(
                        "var arr=[]; document.querySelectorAll(\".ipsDataItem\").forEach(i => arr.push([i.querySelector(\"span\").innerText, i.querySelector(\"a\").href])); JSON.stringify(arr)");
                    var list = result.FromJSONString<List<List<string>>>();
                    var client = await driver.GetClient();
                    var url = list.FirstOrDefault(a => a[0] == ModName)?[1];
                    await Task.Delay(10000);
                    return new HTTPDownloader.State { Url = url, Client = client };
                }
            }

            public override bool Verify()
            {
                return ResolveDownloadUrl().Result.Verify();
            }

            public override IDownloader GetDownloader()
            {
                throw new NotImplementedException();
            }

            public override string GetReportEntry(Archive a)
            {
                throw new NotImplementedException();
            }
        }
    }
}
