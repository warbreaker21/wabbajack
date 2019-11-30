using System;
using System.Threading.Tasks;
using Xilium.CefGlue;
using Xilium.CefGlue.WPF;

namespace Wabbajack.Lib.WebAutomation
{
    public class WebAutomationWindowViewModel : ViewModel
    {
        private WebAutomationWindow _window;

        public WpfCefBrowser Browser => _window.WebView;

        public WebAutomationWindowViewModel(WebAutomationWindow window)
        {
            _window = window;
        }

        public async Task<Uri> NavigateTo(Uri uri)
        {
            Browser.Address = uri.ToString();
            while (Browser.IsLoading)
            {
                await Task.Delay(100);
            }

            return new Uri(Browser.Address);
        }


    }
}
