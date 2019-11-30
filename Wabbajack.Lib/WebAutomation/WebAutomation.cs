using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Wabbajack.Common;
using Xilium.CefGlue;

namespace Wabbajack.Lib.WebAutomation
{
    public enum DisplayMode
    {
        Visible,
        Hidden
    }

    public class Driver : IDisposable
    {
        private WebAutomationWindow _window;
        private WebAutomationWindowViewModel _ctx;
        private Task<WebAutomationWindow> _windowTask;

        static Driver()
        {
            /*var settings = new CefSettings {PersistSessionCookies = true};
            var args = new CefMainArgs(new string[]{});
            CefRuntime.Initialize(args, settings, null);*/
        }

        private Driver(DisplayMode displayMode = DisplayMode.Hidden)
        {
            var windowTask = new TaskCompletionSource<WebAutomationWindow>();

            var t = new Thread(() =>
            {
                _window = new WebAutomationWindow();
                _ctx = (WebAutomationWindowViewModel)_window.DataContext;
                // Initiates the dispatcher thread shutdown when the window closes
                    
                _window.Closed += (s, e) => _window.Dispatcher.InvokeShutdown();

                if (displayMode == DisplayMode.Hidden)
                {
                    _window.WindowState = WindowState.Minimized;
                    _window.ShowInTaskbar = false;
                }

                _window.Show();

                windowTask.SetResult(_window);
                // Makes the thread support message pumping
                System.Windows.Threading.Dispatcher.Run();
            });
            _windowTask = windowTask.Task;

            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        public static async Task<Driver> Create(DisplayMode mode = DisplayMode.Hidden)
        {
            var driver = new Driver(mode);
            driver._window = await driver._windowTask;
            driver._ctx = (WebAutomationWindowViewModel) driver._window.DataContext;
            return driver;
        }

        public Task<Uri> NavigateTo(Uri uri)
        {
            return _ctx.NavigateTo(uri);
        }

        public List<Cookie> GetCookies(string domainEnding)
        {
            var manager = CefCookieManager.GetGlobal(null);
            var visitor = new CookieVisitor();
            manager.VisitAllCookies(visitor);
            Thread.Sleep(500);
            return visitor.Cookies.Where(c=> c.Domain.EndsWith(domainEnding)).ToList();
        }
        public string GetAgentString()
        {
            return
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/78.0.3904.108 Safari/537.36";
        }

        private class CookieVisitor : CefCookieVisitor
        {
            public List<Cookie> Cookies { get; }= new List<Cookie>();
            protected override bool Visit(CefCookie cookie, int count, int total, out bool delete)
            {
                Cookies.Add(new Cookie
                {
                    Name = cookie.Name,
                    Value = cookie.Value,
                    Domain = cookie.Domain,
                    Path = cookie.Path
                });
                delete = false;
                return true;
            }
        }

        public class Cookie
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Domain { get; set; }
            public string Path { get; set; }
        }


        public Task<Uri> GetLocation()
        {
            var tcs = new TaskCompletionSource<Uri>();
            _window.Dispatcher.Invoke(() => tcs.SetResult(new Uri(_window.WebView.Address)));
            return tcs.Task;
        }

        public Task<string> GetAttr(string selector, string attr)
        {
            var tcs = new TaskCompletionSource<string>();
            _window.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var script = $"document.querySelector(\"{selector}\").{attr}";
                    var result = await _window.WebView.EvaluateJavaScript<string>(script);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public void Dispose()
        {
            _window.Dispatcher.Invoke(_window.Close);
        }

        public Task<string> Eval(string script)
        {
            var tcs = new TaskCompletionSource<string>();
            _window.Dispatcher.Invoke(() =>
            {
                try
                {
                    var result = _window.WebView.EvaluateJavaScript<string>(script).Result;
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }


        public static HttpClient GetClient(List<Cookie> cookies, string referer)
        {
            var container = ToCookieContainer(cookies);
            var handler = new HttpClientHandler {CookieContainer = container};
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Referrer = new Uri(referer);
            return client;
        }

        private static CookieContainer ToCookieContainer(List<Cookie> cookies)
        {
            var container = new CookieContainer();
            cookies
                .Do(cookie =>
            {
                container.Add(new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
            });

            return container;
        }
    }
}

