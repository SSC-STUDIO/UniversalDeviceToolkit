using System.Text.Json;
using UniversalDeviceToolkit.Windows;
using Xunit;

namespace UniversalDeviceToolkit.Fast.Tests;

public sealed class TrayMenuTests
{
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public async Task InstallerAndCapabilityGates_HideUnavailableCommandsAndAutomationReads()
    {
        var calls = new List<string>();
        var selection = Json(new { features = new { keyboard = false, automation = false } });
        var menu = new TrayMenu((method, _, _) =>
        {
            calls.Add(method);
            return Task.FromResult(method switch
            {
                "host.getCapabilities" => Json(new { capabilities = new { macro = false } }),
                "settings.get" => Json(new { value = new { NavigationItemsVisibility = new { windowsOptimization = false } } }),
                _ => Json(new { })
            });
        }, selection, _ => { });
        var items = await menu.LoadAsync("en", default);

        Assert.Equal(new[] { "nav:/dashboard", "open", "close" }, items.Where(item => item.Command != null).Select(item => item.Command));
        Assert.DoesNotContain("automation.getState", calls);
        Assert.DoesNotContain("features.get", calls);
    }

    [Fact]
    public async Task HostSnapshot_PreservesZeroBatterySelectedPowerAndTriggerlessActions()
    {
        var menu = new TrayMenu((method, _, _) => Task.FromResult(method switch
        {
            "features.isSupported" => Json(new { value = true }),
            "features.get" => Json(new { value = "Balance" }),
            "features.getAllStates" => Json(new { value = new[] { "Quiet", "Balance", "GodMode" } }),
            "sensors.get" => Json(new { battery = new { chargeLevel = 0 } }),
            "automation.getState" => Json(new { pipelines = new object[]
            {
                new { id = "first", name = "First", trigger = (object?)null },
                new { id = "scheduled", name = "Scheduled", trigger = new { type = "timer" } },
                new { id = "gpu", name = "__udt.quickAction.deactivateGpu", trigger = (object?)null }
            } }),
            _ => Json(new { })
        }), null, _ => { });
        var items = await menu.LoadAsync("ja-JP", default);

        Assert.Contains("(0%)", items[0].Label);
        var power = Assert.Single(items, item => item.Children != null).Children;
        Assert.NotNull(power);
        Assert.Equal("powerMode:Balance", Assert.Single(power, item => item.Checked).Command);
        var actions = items.Where(item => item.Command?.StartsWith("run:") == true).ToArray();
        Assert.Equal(new[] { "run:gpu", "run:first" }, actions.Select(item => item.Command));
        Assert.Equal(ShellStrings.Get("ja", "deactivateGpu"), actions[0].Label);
        Assert.Equal(ShellStrings.Get("ja", "open"), items[^2].Label);
    }

    [Fact]
    public async Task Commands_ForwardExactRpcAndKeepNavigationAndQuitSeparate()
    {
        var calls = new List<(string Method, JsonElement Parameters)>();
        var menu = new TrayMenu((method, parameters, _) =>
        {
            calls.Add((method, Json(parameters ?? new { })));
            return Task.FromResult(Json(new { }));
        }, null, _ => { });
        var opened = new List<string?>();
        var quit = 0;
        await menu.ExecuteAsync("nav:/keyboard", opened.Add, () => quit++, default);
        await menu.ExecuteAsync("open", opened.Add, () => quit++, default);
        await menu.ExecuteAsync("close", opened.Add, () => quit++, default);
        await menu.ExecuteAsync("powerMode:GodMode", opened.Add, () => quit++, default);
        await menu.ExecuteAsync("run:custom-id", opened.Add, () => quit++, default);

        Assert.Equal(new string?[] { "/keyboard", null }, opened);
        Assert.Equal(1, quit);
        Assert.Equal("features.set", calls[0].Method);
        Assert.Equal("GodMode", calls[0].Parameters.GetProperty("value").GetString());
        Assert.Equal("powerMode", calls[0].Parameters.GetProperty("feature").GetString());
        Assert.Equal("automation.runNow", calls[1].Method);
        Assert.Equal("custom-id", calls[1].Parameters.GetProperty("pipelineId").GetString());
        await Assert.ThrowsAsync<ArgumentException>(() => menu.ExecuteAsync("nav:/arbitrary", opened.Add, () => quit++, default));
        Assert.Equal(2, calls.Count);
    }

    [Fact]
    public async Task HostFailure_LeavesOpenAndExitAvailableAndReportsTheFailure()
    {
        var errors = new List<string>();
        var menu = new TrayMenu((_, _, _) => Task.FromException<JsonElement>(new IOException("Offline")), null, errors.Add);
        var items = await menu.LoadAsync("en", default);
        Assert.Equal("open", items[^2].Command);
        Assert.Equal("close", items[^1].Command);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void NativeMenu_PreservesDistinctIdsSeparatorsAndCheckedSubmenu()
    {
        if (!OperatingSystem.IsWindows()) return;
        var commands = new Dictionary<nuint, string>();
        var menu = NativeMenu.Build([
            new("Power", Children: [new("Balance", "powerMode:Balance", Checked: true)]),
            new(""), new("Open", "open"), new("Exit", "close")
        ], commands);
        try
        {
            Assert.Equal(4, Win32.GetMenuItemCount(menu));
            Assert.NotEqual(Win32.GetMenuItemID(menu, 2), Win32.GetMenuItemID(menu, 3));
            Assert.Equal("close", commands[Win32.GetMenuItemID(menu, 3)]);
            var submenu = Win32.GetSubMenu(menu, 0);
            Assert.NotEqual(0, submenu);
            Assert.Equal(0x8u, Win32.GetMenuState(submenu, 0, 0x400) & 0x8u);
            Assert.Equal(0x800u, Win32.GetMenuState(menu, 1, 0x400) & 0x800u);
        }
        finally { Win32.DestroyMenu(menu); }
    }

    [Theory]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("ZH_hant_HK", "zh-Hant")]
    [InlineData("zh-SG", "zh-CN")]
    [InlineData("ja-JP", "ja")]
    [InlineData("nl-BE", "nl-NL")]
    [InlineData("pt-PT", "pt")]
    [InlineData("unknown", "en")]
    public void NativeLocale_ResolvesLanguageVariants(string language, string expected)
    {
        Assert.Equal(ShellStrings.Get(expected, "open"), ShellStrings.Get(language, "open"));
        Assert.Equal(ShellStrings.Get(expected, "close"), ShellStrings.Get(language, "close"));
        Assert.Equal(ShellStrings.Get(expected, "dashboard"), ShellStrings.Get(language, "dashboard"));
    }
}
