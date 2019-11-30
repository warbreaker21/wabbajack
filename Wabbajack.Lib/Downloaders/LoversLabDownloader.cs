using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;

namespace Wabbajack.Lib.Downloaders
{
    public class LoversLabDownloader : IDownloader
    {
        internal HttpClient _authedClient;

        public AbstractDownloadState GetDownloaderState(dynamic archive_ini)
        {

            Uri url = DownloaderUtils.GetDirectURL(archive_ini);
            if (url == null || url.Host != "www.loverslab.com") return null;
            var id = HttpUtility.ParseQueryString(url.Query)["r"];

            return new State
            {
                FileID = id
            };
        }

        public void Prepare()
        {
            var result = GetAuthedClient().Result;
            if (result == null)
                throw new Exception("not logged into LL, TODO");

            _authedClient = result;
        }

        private async Task<HttpClient> GetAuthedClient()
        {
            using (var driver = await Driver.Create(DisplayMode.Visible))
            {
                await driver.NavigateTo(new Uri("https://www.loverslab.com"));
                var html = (await driver.GetAttr("#elUserLink", "innerHTML"));
                return html != null ? await driver.GetClient() : null;
            }
        }

        public class State : AbstractDownloadState
        {
            public string FileID { get; set; }

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
                var result = GetDownloader<LoversLabDownloader>();
                var html = await result._authedClient.GetStringAsync(
                    $"https://www.loverslab.com/files/file/11116-test-file-for-wabbajack-integration/?do=download&r={FileID}");



                /*
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
                }*/
                return null;
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
