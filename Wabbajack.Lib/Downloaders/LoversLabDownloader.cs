using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib.Validation;
using Wabbajack.Lib.WebAutomation;
using Alphaleonis.Win32.Filesystem;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using File = Alphaleonis.Win32.Filesystem.File;

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
            try
            {
                var cookies = new List<Driver.Cookie>();
                if (!File.Exists("loverslabcookies.changeme"))
                {
                    using (var driver = await Driver.Create(DisplayMode.Visible))
                    {
                        await driver.NavigateTo(new Uri("http://www.loverslab.com/login"));

                        while (cookies.All(c => c.Name != "ips4_login_key"))
                        {
                            cookies = driver.GetCookies("loverslab.com");
                            await Task.Delay(500);
                        }

                        cookies.ToJSON("loverslabcookies.changeme");
                        return Driver.GetClient(cookies, "https://www.loverslab.com");
                    }
                }
                else {
                    cookies = "loverslabcookies.changeme".FromJSON<List<Driver.Cookie>>();
                    return Driver.GetClient(cookies, "https://www.loverslab.com");
                }

            }
            catch (Exception ex)
            {
                return null;
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
                var stream = ResolveDownloadStream().Result;
                using (var file = File.OpenWrite(destination))
                {
                    stream.CopyTo(file);
                }
            }

            private async Task<Stream> ResolveDownloadStream()
            {
                var result = GetDownloader<LoversLabDownloader>();
                var html = await result._authedClient.GetStringAsync(
                    $"https://www.loverslab.com/files/file/11116-test-file-for-wabbajack-integration/?do=download&r={FileID}");

                var pattern = new Regex("(?<=csrfKey=).*(?=[&\"\'])");
                var csrfKey = pattern.Matches(html).Cast<Match>().Where(m => m.Length == 32).Select(m => m.ToString()).FirstOrDefault();

                if (csrfKey == null)
                    return null;

                var url =
                    $"https://www.loverslab.com/files/file/11116-test-file-for-wabbajack-integration/?do=download&r={FileID}&confirm=1&t=1&csrfKey={csrfKey}";

                var streamResult = await result._authedClient.GetAsync(url);
                if (streamResult.StatusCode != HttpStatusCode.OK)
                {
                    Utils.Error($"LoversLab servers reported an error for file: {FileID}");
                }

                var content_type = streamResult.Content.Headers.ContentType;

                if (content_type.MediaType == "application/json")
                {
                    Utils.Error("TODO");
                }

                return await streamResult.Content.ReadAsStreamAsync();

            }

            public override bool Verify()
            {
                var stream =  ResolveDownloadStream().Result;
                if (stream == null)
                {
                    return false;
                }

                stream.Close();
                return true;

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
