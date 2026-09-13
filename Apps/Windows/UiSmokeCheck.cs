using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace UniversalDeviceToolkit.Windows;

/// <summary>Runs the packaged renderer and native bridge, including window visibility transitions.</summary>
internal static class UiSmokeCheck
{
    internal static async Task RunAsync(CoreWebView2Controller controller, NativeWindow window)
    {
        var webView = controller.CoreWebView2;
        await WaitForAsync(webView, "Boolean(document.querySelector('main')?.innerText.trim() && document.querySelector('nav button'))", "rendered application");
        await AssertVisibilityAsync(controller, true);
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

        Win32.ShowWindow(window.Handle, 6);
        await AssertVisibilityAsync(controller, false);
        window.Show();
        await AssertVisibilityAsync(controller, true);
        window.Hide();
        await AssertVisibilityAsync(controller, false);
        window.Show();
        await AssertVisibilityAsync(controller, true);
        await WaitForAsync(webView, "Boolean(document.querySelector('main')?.innerText.trim())", "restored application");
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
