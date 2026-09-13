using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Runs the packaged renderer and native bridge, including window visibility transitions.</summary>
internal static class UiSmokeCheck
{
    internal static async Task RunAsync(CoreWebView2Controller controller, NativeWindow window, bool startedMinimized, Action restore)
    {
        var webView = controller.CoreWebView2;
        if (startedMinimized)
        {
            if (Win32.IsWindowVisible(window.Handle) || controller.IsVisible)
                throw new InvalidOperationException("Minimized startup displayed the application window.");
            restore();
        }
        await WaitForAsync(webView, "Boolean(document.querySelector('main')?.innerText.trim() && document.querySelector('nav button'))", "rendered application");
        await AssertVisibilityAsync(controller, true);
        AssertWindowGeometry(window);
        await webView.ExecuteScriptAsync("""
            window.__udtSmoke = { done: false };
            Promise.all([window.bridge.getHostStatus(), window.bridge.invoke('host.getCapabilities')])
              .then(([status, capabilities]) => {
                window.__udtSmoke = { done: true, ok: status.ready === true && capabilities.platform === 'windows' };
              })
              .catch(error => { window.__udtSmoke = { done: true, ok: false, error: String(error) }; });
            """);
        await WaitForAsync(webView, "window.__udtSmoke?.done === true", "Host bridge response");
        var reply = await webView.ExecuteScriptAsync("window.__udtSmoke");
        using var result = JsonDocument.Parse(reply);
        if (!result.RootElement.GetProperty("ok").GetBoolean())
            throw new InvalidOperationException($"UI bridge check failed: {reply}");

        Win32.ShowWindow(window.Handle, 3);
        await AssertVisibilityAsync(controller, true);
        AssertWindowGeometry(window, maximized: true);
        window.Show();
        Win32.ShowWindow(window.Handle, 6);
        await AssertVisibilityAsync(controller, false);
        window.Show();
        await AssertVisibilityAsync(controller, true);
        window.Hide();
        await AssertVisibilityAsync(controller, false);
        window.Show();
        await AssertVisibilityAsync(controller, true);
        await WaitForAsync(webView, "Boolean(document.querySelector('main')?.innerText.trim())", "restored application");
        await AssertPageCacheAsync(webView);
        await AssertResizeAsync(controller, window);
        if (int.TryParse(Environment.GetEnvironmentVariable("UDT_UI_INSPECTION_SECONDS"), out var seconds) && seconds is > 0 and <= 60)
        {
            Console.WriteLine($"Visual inspection: resized tools window, process {Environment.ProcessId}, {seconds} seconds.");
            await Task.Delay(TimeSpan.FromSeconds(seconds));
        }
    }

    private static async Task AssertResizeAsync(CoreWebView2Controller controller, NativeWindow window)
    {
        var webView = controller.CoreWebView2;
        await webView.ExecuteScriptAsync("window.location.hash = '/tools'");
        await WaitForAsync(webView, "document.querySelector('[data-udt-page=\"/tools\"]')?.getClientRects().length > 0", "tools resize fixture");
        var work = NativeWindow.GetMonitor(window.Handle).Work;
        var scale = Win32.GetDpiForWindow(window.Handle) / 96.0;
        var startWidth = Math.Min(work.Right - work.Left, (int)(800 * scale));
        var startHeight = Math.Min(work.Bottom - work.Top, (int)(600 * scale));
        var endWidth = work.Right - work.Left;
        var endHeight = work.Bottom - work.Top;
        foreach (var material in new[] { "mica", "none", "acrylic", "mica" })
        {
            await webView.ExecuteScriptAsync($"document.documentElement.dataset.backdrop = '{material}'; window.bridge.setBackgroundMaterial('{material}');");
            for (var step = 0; step <= 16; step++)
            {
                var progress = (step <= 8 ? step : 16 - step) / 8.0;
                var width = startWidth + (int)((endWidth - startWidth) * progress);
                var height = startHeight + (int)((endHeight - startHeight) * progress);
                if (!Win32.SetWindowPos(window.Handle, 0, work.Left, work.Top, width, height, 0x0014))
                    throw new InvalidOperationException("Resize fixture could not set window bounds.");
                await Task.Delay(35);
                if (controller.Bounds.Width != width || controller.Bounds.Height != height)
                    throw new InvalidOperationException("The renderer did not follow the resized client bounds.");
            }
            if (material == "none" && controller.DefaultBackgroundColor.A != 255)
                throw new InvalidOperationException("Disabling the backdrop left an unpainted transparent surface.");
            if (material != "none" && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) && controller.DefaultBackgroundColor.A != 0)
                throw new InvalidOperationException("The WebView background obscures the native material.");
        }
        Win32.SetWindowPos(window.Handle, 0, work.Left, work.Top, endWidth, endHeight, 0x0014);
        await WaitForAsync(webView,
            $"Math.abs(innerWidth * devicePixelRatio - {endWidth}) <= 2 && Math.abs(innerHeight * devicePixelRatio - {endHeight}) <= 2",
            "renderer viewport after continuous resize");
        AssertWindowGeometry(window);
    }

    private static async Task AssertPageCacheAsync(CoreWebView2 webView)
    {
        await webView.ExecuteScriptAsync("""
            window.__udtCachedDashboard = document.querySelector('[data-udt-page="/dashboard"]');
            window.location.hash = '/optimization';
            """);
        await WaitForAsync(webView,
            "location.hash === '#/tools' && document.querySelectorAll('[data-udt-page=\"/tools\"] .udt-segmented-nav [role=tab]').length > 1",
            "legacy tools redirect and rendered tabs");
        await webView.ExecuteScriptAsync("""
            window.__udtCachedTools = document.querySelector('[data-udt-page="/tools"]');
            window.__udtCachedToolTab = window.__udtCachedTools.querySelectorAll('.udt-segmented-nav [role=tab]')[1];
            window.__udtCachedToolTab.click();
            """);
        await WaitForAsync(webView, "window.__udtCachedToolTab.getAttribute('aria-selected') === 'true'", "tool tab selection");
        await webView.ExecuteScriptAsync("window.location.hash = '/about?view=macro'");
        await WaitForAsync(webView,
            "Boolean(document.querySelector('[data-udt-page=\"/about\"]')?.innerText.trim()) && window.__udtCachedTools.isConnected && window.__udtCachedTools.getClientRects().length === 0",
            "hidden retained tools page");
        await webView.ExecuteScriptAsync("window.location.hash = '/tools'");
        await WaitForAsync(webView,
            "window.__udtCachedTools === document.querySelector('[data-udt-page=\"/tools\"]') && window.__udtCachedTools.getClientRects().length > 0 && window.__udtCachedToolTab.getAttribute('aria-selected') === 'true'",
            "restored tool tab state and DOM");
        await webView.ExecuteScriptAsync("window.location.hash = '/dashboard'");
        await WaitForAsync(webView,
            "window.__udtCachedDashboard === document.querySelector('[data-udt-page=\"/dashboard\"]') && window.__udtCachedDashboard?.getClientRects().length > 0",
            "retained dashboard");
    }

    private static void AssertWindowGeometry(NativeWindow window, bool maximized = false)
    {
        if (!Win32.GetWindowRect(window.Handle, out var outer) || !Win32.GetClientRect(window.Handle, out var client))
            throw new InvalidOperationException("Unable to read window geometry.");
        if (outer.Right - outer.Left != client.Right || outer.Bottom - outer.Top != client.Bottom)
            throw new InvalidOperationException("The native non-client frame still reduces the renderer area.");
        var work = NativeWindow.GetMonitor(window.Handle).Work;
        if (outer.Left < work.Left || outer.Top < work.Top || outer.Right > work.Right || outer.Bottom > work.Bottom)
            throw new InvalidOperationException("The window extends outside the monitor work area.");
        if (maximized)
        {
            if (outer.Left != work.Left || outer.Top != work.Top || outer.Right != work.Right || outer.Bottom != work.Bottom)
                throw new InvalidOperationException("The maximized window does not match the monitor work area.");
        }
        else
        {
            var point = (nint)(((outer.Top + 1) << 16) | ((outer.Left + 1) & 0xffff));
            if (Win32.SendMessage(window.Handle, 0x0084, 0, point) != 13)
                throw new InvalidOperationException("The frameless window lost its corner resize target.");
        }
    }

    private static async Task AssertVisibilityAsync(CoreWebView2Controller controller, bool visible)
    {
        var expected = visible ? "visible" : "hidden";
        await WaitForAsync(controller.CoreWebView2, $"document.visibilityState === '{expected}'", $"{expected} document");
        if (controller.IsVisible != visible || (visible && (controller.Bounds.Width <= 0 || controller.Bounds.Height <= 0)))
            throw new InvalidOperationException($"WebView controller is not {expected} with valid bounds.");
    }

    private static async Task WaitForAsync(CoreWebView2 webView, string expression, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < deadline)
        {
            if (await webView.ExecuteScriptAsync(expression).WaitAsync(TimeSpan.FromSeconds(5)) == "true") return;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Lightweight UI check timed out waiting for {description}.");
    }
}
