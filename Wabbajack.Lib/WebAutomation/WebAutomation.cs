using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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

        public Task<Uri> GetLocation()
        {
            var tcs = new TaskCompletionSource<Uri>();
            _window.Dispatcher.Invoke(() => tcs.SetResult(_window.WebView.Source));
            return tcs.Task;
        }

        public Task<string> GetAttr(string selector, string attr)
        {
            var tcs = new TaskCompletionSource<string>();
            _window.Dispatcher.Invoke(() =>
            {
                try
                {
                    var script = $"document.querySelector(\"{selector}\").{attr}";
                    var result = _window.WebView.InvokeScript("eval", script);
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
                    var result = _window.WebView.InvokeScript("eval", script);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }


        public async Task<HttpClient> GetClient()
        {
            
            var cookies = await Eval("document.cookie");
            var location = await GetLocation();
            var container = ParseCookies(location, cookies);
            var handler = new HttpClientHandler { CookieContainer = container };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Referrer = location;
            return client;
        }

        private CookieContainer ParseCookies(Uri location, string input)
        {
            // From https://stackoverflow.com/questions/28979882/parsing-cookies
            var urib = new UriBuilder();
            urib.Host = location.Host;
            urib.Scheme = location.Scheme;
            var uri = urib.Uri;

            var container = new CookieContainer();
            var values = input.TrimEnd(';').Split(';');
            foreach (var parts in values.Select(c => c.Split(new[] { '=' }, 2)))
            {
                var cookieName = parts[0].Trim();
                string cookieValue;

                if (parts.Length == 1)
                {
                    //Cookie attribute
                    cookieValue = string.Empty;
                }
                else
                {
                    cookieValue = parts[1];
                }

                container.Add(uri, new Cookie(cookieName, cookieValue));
            }

            return container;
        }
    }
}

