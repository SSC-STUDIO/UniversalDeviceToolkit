using FluentAssertions;
using Xunit;

namespace UniversalDeviceToolkit.CrossPlatform.Tests;

public sealed class DeviceSupportStatusTests
{
    private readonly CrossPlatformDeviceSupportEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ShouldMatchAppleMacBasicPack()
    {
        var support = _evaluator.Evaluate(
            new HardwareIdentity("Apple Inc.", "MacBook Pro MacBookPro18,3", "MacBook Pro", "MAC-SERIAL", "test"),
            isWindows: false);

        support.DevicePackId.Should().Be("apple-basic");
        support.DisplayName.Should().Be("Apple Basic");
        support.SupportLevel.Should().Be("Safe basic mode");
        support.IsHardwareControlAvailable.Should().BeFalse();
        support.HiddenFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Fact]
    public void Evaluate_ShouldMatchFrameworkAlias()
    {
        var support = _evaluator.Evaluate(
            new HardwareIdentity("Framework Computer Inc.", "Framework Laptop 16 A8", "Framework Laptop 16", "SERIAL", "test"),
            isWindows: false);

        support.DevicePackId.Should().Be("framework-basic");
        support.EnabledFeatures.Should().Contain("read-only-telemetry");
    }

    [Fact]
    public void Evaluate_ShouldMatchAlienwareVendorAlias()
    {
        var support = _evaluator.Evaluate(
            new HardwareIdentity("Alienware", "Alienware m18 R2", "m18 R2", "SERIAL", "test"),
            isWindows: false);

        support.DevicePackId.Should().Be("dell-basic");
        support.DisplayName.Should().Be("Dell Basic");
        support.SupportLevel.Should().Be("Safe basic mode");
        support.IsHardwareControlAvailable.Should().BeFalse();
    }

    [Theory]
    [InlineData("TIMI", "Redmi G Pro 2024", "xiaomi-basic")]
    [InlineData("LENOVO", "IdeaPad Gaming 3 15ACH6", "lenovo-ideapad-gaming")]
    [InlineData("LENOVO", "Yoga Pro 7 14AHP9", "lenovo-yoga")]
    [InlineData("LENOVO", "小新 Pro 16 2024", "lenovo-xiaoxin-basic")]
    [InlineData("realme", "realme Book Prime", "realme-basic")]
    [InlineData("Infinix Mobility Limited", "INBook X2", "infinix-basic")]
    [InlineData("Motorola Mobility LLC", "Moto Book 60 14IRH10R", "motorola-lenovo-basic")]
    [InlineData("Gateway", "Gateway 14.1 Ultra Slim", "gateway-basic")]
    [InlineData("CHUWI", "CoreBook X", "chuwi-basic")]
    [InlineData("TECLAST", "F15 Plus", "teclast-basic")]
    [InlineData("Jumper", "EZbook X3", "jumper-basic")]
    [InlineData("Mechanical Revolution", "Kuangshi 16 Super", "mechrevo-basic")]
    [InlineData("Hasee", "ZhanShen Z8", "hasee-basic")]
    [InlineData("THUNDEROBOT", "911 Zero", "thunderobot-basic")]
    [InlineData("MACHENIKE", "F117", "machenike-basic")]
    [InlineData("COLORFUL", "Evol X15", "colorful-basic")]
    [InlineData("MAIBENBEN", "MaiBook X", "maibenben-basic")]
    [InlineData("LENOVO", "IdeaPad Flex 5 Chromebook Plus", "lenovo-chromebook-basic")]
    [InlineData("Google LLC", "Pixelbook Go", "google-chromebook-basic")]
    [InlineData("SAMSUNG ELECTRONICS CO., LTD.", "Galaxy Chromebook Plus", "samsung-basic")]
    [InlineData("TUXEDO", "InfinityBook Pro 14", "xmg-schenker-basic")]
    [InlineData("Valve Corporation", "Steam Deck", "valve-handheld-basic")]
    [InlineData("GPD", "GPD WIN 4", "gpd-handheld-basic")]
    [InlineData("AYANEO", "AYANEO NEXT", "ayaneo-handheld-basic")]
    [InlineData("ONEXPLAYER", "OneXPlayer 2 Pro", "one-netbook-handheld-basic")]
    [InlineData("AZW", "Beelink SER8", "beelink-basic")]
    [InlineData("System76", "Oryx Pro oryp13", "system76-basic")]
    [InlineData("Notebook", "oryp13", "system76-basic")]
    [InlineData("Star Labs Systems", "StarFighter", "star-labs-basic")]
    [InlineData("SLIMBOOK", "KDE Slimbook VII", "slimbook-basic")]
    [InlineData("NEC Personal Computers, Ltd.", "LAVIE NEXTREME Carbon", "nec-lavie-basic")]
    [InlineData("Sharp Corporation", "Mebius Chromebook", "sharp-basic")]
    [InlineData("Monster Notebook", "Tulpar T7 V20", "monster-tulpar-basic")]
    [InlineData("Dream Machines", "RG4070-16", "dream-machines-basic")]
    [InlineData("PC Specialist Ltd", "Recoil 17", "pcspecialist-basic")]
    [InlineData("Eurocom Corporation", "Nightsky ARX15", "eurocom-basic")]
    [InlineData("Eluktronics", "MECH-17 GP3", "eluktronics-basic")]
    [InlineData("MAINGEAR", "Vector Pro 2", "maingear-basic")]
    [InlineData("ORIGIN PC", "EON16-S", "origin-pc-basic")]
    [InlineData("Corsair", "Voyager a1600", "corsair-basic")]
    [InlineData("iBUYPOWER", "Y60 Gaming Desktop", "cyberpower-ibuypower-basic")]
    [InlineData("Casper Bilgisayar", "Excalibur G870", "casper-excalibur-basic")]
    [InlineData("AVITA", "LIBER V14", "nexstgo-avita-basic")]
    [InlineData("Positivo Tecnologia", "Motion C4500", "positivo-basic")]
    [InlineData("Wortmann AG", "TERRA MOBILE 1517", "wortmann-terra-basic")]
    [InlineData("Huawei Technologies", "Qingyun L540", "huawei-basic")]
    [InlineData("Dynabook Incorporated", "Portégé X40-K", "dynabook-basic")]
    [InlineData("GMKtec", "NucBox K8 Plus", "gmktec-basic")]
    [InlineData("MORE FINE", "M600 Mini PC", "morefine-basic")]
    [InlineData("ACEMAGICIAN", "Tank03", "acemagic-basic")]
    [InlineData("AOOSTAR", "GEM12 Pro", "aoostar-basic")]
    [InlineData("TRIGKEY", "Mini PC S7", "regional-mini-pc-basic")]
    [InlineData("Super Micro Computer, Inc.", "X13 Workstation", "universal-workstation-basic")]
    [InlineData("ASRock", "Z790 Taichi", "universal-motherboard-basic")]
    [InlineData("Default string", "System Product Name", "universal-desktop-basic")]
    [InlineData("CLEVO", "Barebone GM7", "clevo-tongfang-basic")]
    [InlineData("To Be Filled By O.E.M.", "To Be Filled By O.E.M.", "universal-motherboard-basic")]
    public void Evaluate_ShouldMatchExpandedBrandBasicPacks(string vendor, string model, string expectedPackId)
    {
        var support = _evaluator.Evaluate(
            new HardwareIdentity(vendor, model, model, "SERIAL", "test"),
            isWindows: false);

        support.DevicePackId.Should().Be(expectedPackId);
        support.SupportLevel.Should().Be("Safe basic mode");
        support.EnabledFeatures.Should().Contain(["diagnostics", "hardware-identity", "read-only-telemetry"]);
        support.HiddenFeatures.Should().Contain(["lenovo-hardware-controls", "power-modes"]);
    }

    [Fact]
    public void Evaluate_ShouldMatchLenovoLegionButStayInBasicMode()
    {
        var support = _evaluator.Evaluate(
            new HardwareIdentity("LENOVO", "Legion Pro 7 16IRX9", "Legion Pro 7", "SERIAL", "test"),
            isWindows: false);

        support.DevicePackId.Should().Be("lenovo-legion-pro-7");
        support.IsHardwareControlAvailable.Should().BeFalse();
        support.HiddenFeatures.Should().Contain("power-modes");
        support.Reason.Should().MatchRegex("(?i)disabled");
    }

    [Fact]
    public void Evaluate_OnWindowsMatch_ShouldStillUseCrossPlatformBasicPack()
    {
        var support = _evaluator.Evaluate(
            new HardwareIdentity("LENOVO", "Legion Pro 7 16IRX9", "Legion Pro 7", "SERIAL", "test"),
            isWindows: true);

        support.DevicePackId.Should().Be("lenovo-legion-pro-7");
        support.IsHardwareControlAvailable.Should().BeFalse();
        support.Reason.Should().MatchRegex("(?i)Windows desktop app");
    }

    [Fact]
    public void Evaluate_WhenVendorUnknown_ShouldReturnGenericBasicMode()
    {
        var support = _evaluator.Evaluate(HardwareIdentity.Unknown("test"), isWindows: false);

        support.DevicePackId.Should().Be("generic-pc-basic");
        support.DisplayName.Should().Be("Generic PC Basic");
        support.EnabledFeatures.Should().Contain("safe-basic-mode");
        support.HiddenFeatures.Should().Contain("gpu-overclock");
    }
}
