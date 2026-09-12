using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Tests.Infrastructure;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.ProcessState)]
public sealed class LenovoDeviceSupportProviderTests
{
    public LenovoDeviceSupportProviderTests()
    {
        LenovoDeviceSupportProvider.Instance.SetInstalledCatalog(null);
        LenovoDeviceSupportProvider.Instance.SetPreferredDevicePackId(null);
    }

    [Fact]
    public void Evaluate_WhenLegionMachineTypeMatches_ShouldEnableSupportedMode()
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "83DE",
            Model = "Legion Y9000P IRX9"
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("lenovo-legion-pro-7");
        availability.EnabledFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Theory]
    [InlineData("82AX", "Y7000P 2020H")]
    [InlineData("82B0", "Y7000P2020H")]
    [InlineData("82GR", "Legion Y7000P 2020H")]
    [InlineData("0000", "Lenovo Y7000P2020H")]
    public void Evaluate_WhenY7000P2020HMatches_ShouldEnableLegion5HardwareSupport(string machineType, string model)
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = machineType,
            Model = model
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("lenovo-legion-5");
        availability.EnabledFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Theory]
    [InlineData("83DF", "Legion Y9000P IRX9", "lenovo-legion-pro-5")]
    [InlineData("83DG", "Legion Pro 5 16IRX9", "lenovo-legion-pro-5")]
    [InlineData("83F2", "Legion Pro 5 16IRX10", "lenovo-legion-pro-5")]
    [InlineData("83NN", "Legion Pro 5 16IRX10", "lenovo-legion-pro-5")]
    [InlineData("83F0", "Legion 5 15IRX10", "lenovo-legion-5")]
    [InlineData("83LY", "Legion 5 15IRX10", "lenovo-legion-5")]
    [InlineData("83GS", "LOQ 15IRX9", "lenovo-loq")]
    [InlineData("83AQ", "LOQ 15APH11", "lenovo-loq")]
    [InlineData("83JE", "LOQ 15IRX10", "lenovo-loq")]
    [InlineData("83E1", "Legion Go", "lenovo-legion-go")]
    [InlineData("83N0", "Legion Go S", "lenovo-legion-go")]
    [InlineData("83DE", "Legion Pro 7 16IRX9", "lenovo-legion-pro-7")]
    [InlineData("83F5", "Legion Pro 7 16IAX10H", "lenovo-legion-pro-7")]
    [InlineData("83ZZ", "Y7000P 2025", "lenovo-legion-5")]
    [InlineData("83G0", "Legion 9i 16IRX9", "lenovo-legion-9")]
    [InlineData("82Y5", "Legion Slim 5 14APH8", "lenovo-legion-slim-5")]
    public void Evaluate_WhenRecentLenovoGamingMatches_ShouldEnableHardwarePack(
        string machineType,
        string model,
        string expectedPackId)
    {
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = machineType,
            Model = model
        };

        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.DevicePackId.Should().Be(expectedPackId);
    }

    [Theory]
    [InlineData("ASUSTeK COMPUTER INC.", "ROG Zephyrus G16")]
    [InlineData("ASUSTEK", "TUF Gaming F15")]
    [InlineData("ASUS", "Vivobook Pro 15")]
    public void Evaluate_WhenAsusMachineMatches_ShouldEnableAsusHardwarePack(string vendor, string model)
    {
        var machineInformation = new MachineInformation
        {
            Vendor = vendor,
            MachineType = "0000",
            Model = model
        };

        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.DevicePackId.Should().Be("asus-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["fan-curve", "gpu-overclock"]);
    }

    [Fact]
    public async Task BuiltInCatalog_ShouldExposeManyHardwarePacksForStartupDeviceSetup()
    {
        var catalog = await LenovoDeviceSupportProvider.Instance.GetCatalogAsync();
        var hardwarePacks = catalog.DevicePacks
            .Where(p => p.EnabledFeatures.Contains("lenovo-hardware-controls", StringComparer.OrdinalIgnoreCase))
            .ToArray();

        hardwarePacks.Length.Should().BeGreaterThanOrEqualTo(8);
        hardwarePacks.Select(p => p.Id).Should().Contain([
            "lenovo-legion-pro-7",
            "lenovo-legion-pro-5",
            "lenovo-legion-5",
            "lenovo-loq",
            "lenovo-legion-go",
            "lenovo-ideapad-gaming"
        ]);

        // Expanded MTM coverage for 2024–2026 refreshes should be present.
        hardwarePacks.First(p => p.Id == "lenovo-legion-pro-5").MachineTypes.Should().Contain("83NN");
        hardwarePacks.First(p => p.Id == "lenovo-loq").MachineTypes.Should().Contain("83JE");
        hardwarePacks.First(p => p.Id == "lenovo-legion-5").MachineTypes.Should().Contain("83LY");

        // Each MTM may appear in only one pack so FirstOrDefault match is deterministic.
        var mtmOwners = catalog.DevicePacks
            .SelectMany(p => (p.MachineTypes ?? []).Select(mt => (Mt: mt.ToUpperInvariant(), PackId: p.Id)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Mt))
            .GroupBy(x => x.Mt)
            .Where(g => g.Select(x => x.PackId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => $"{g.Key} -> {string.Join(", ", g.Select(x => x.PackId).Distinct())}")
            .ToArray();
        mtmOwners.Should().BeEmpty("machine types must be unique across device packs");

        // Non-gaming consumer Lenovo lines stay basic (no EC hardware pack claims).
        var ideapad = catalog.DevicePacks.First(p => p.Id == "lenovo-ideapad");
        ideapad.EnabledFeatures.Should().NotContain("lenovo-hardware-controls");
        catalog.DevicePacks.First(p => p.Id == "lenovo-yoga").EnabledFeatures
            .Should().NotContain("lenovo-hardware-controls");
        catalog.DevicePacks.First(p => p.Id == "lenovo-thinkbook").EnabledFeatures
            .Should().NotContain("lenovo-hardware-controls");
    }

    [Fact]
    public void Evaluate_WhenOnlySkuContainsMachineType_ShouldEnablePro5ForY9000PIrx9()
    {
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "",
            Model = "Legion Y9000P IRX9",
            Hardware = new()
            {
                ComputerSystem = new()
                {
                    Manufacturer = "LENOVO",
                    Model = "83DF",
                    SystemFamily = "Legion Y9000P IRX9",
                    ChassisSkuNumber = "LENOVO_MT_83DF_BU_idea_FM_Legion Y9000P IRX9"
                }
            }
        };

        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.DevicePackId.Should().Be("lenovo-legion-pro-5");
        availability.EnabledFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Fact]
    public void Evaluate_WhenNonLenovoDevice_ShouldUseBasicMode()
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = "Unknown Vendor",
            MachineType = "0000",
            Model = "Generic PC"
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeFalse();
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be("generic-pc-basic");
        availability.EnabledFeatures.Should().Contain(["system-optimization"]);
        availability.HiddenFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Theory]
    [InlineData("LENOVO", "ThinkPad X1 Carbon Gen 12", "lenovo-thinkpad-basic")]
    [InlineData("LENOVO", "ThinkCentre Neo 50q", "lenovo-thinkcentre-basic")]
    [InlineData("LENOVO", "ThinkStation P8", "lenovo-thinkstation-basic")]
    [InlineData("LENOVO", "IdeaCentre 5", "lenovo-ideacentre-basic")]
    [InlineData("LENOVO", "Legion Tower 7i", "lenovo-legion-desktop-basic")]
    [InlineData("LENOVO", "XiaoXin Pro 16", "lenovo-xiaoxin-basic")]
    [InlineData("LENOVO", "小新 Pro 16 2024", "lenovo-xiaoxin-basic")]
    [InlineData("LENOVO", "Lenovo V15 G4", "lenovo-v-series-basic")]
    [InlineData("Microsoft Corporation", "Surface Laptop Studio", "microsoft-surface-basic")]
    [InlineData("Samsung", "Galaxy Book4 Pro", "samsung-basic")]
    [InlineData("Apple Inc.", "MacBook Pro", "apple-basic")]
    [InlineData("Huawei Technologies Co., Ltd.", "MateBook X Pro", "huawei-basic")]
    [InlineData("Redmi", "RedmiBook Pro 16", "xiaomi-basic")]
    [InlineData("realme", "realme Book Prime", "realme-basic")]
    [InlineData("Infinix Mobility Limited", "INBook X2", "infinix-basic")]
    [InlineData("Honor Device Co., Ltd.", "MagicBook Pro 16", "honor-basic")]
    [InlineData("LG", "LG gram 17", "lg-basic")]
    [InlineData("Framework Computer Inc.", "Framework Laptop 16", "framework-basic")]
    [InlineData("Panasonic Corporation", "TOUGHBOOK 55", "panasonic-basic")]
    [InlineData("TOSHIBA", "Tecra A40", "dynabook-basic")]
    [InlineData("FUJITSU CLIENT COMPUTING LIMITED", "LIFEBOOK U9313", "fujitsu-basic")]
    [InlineData("VAIO", "VAIO SX14", "vaio-basic")]
    [InlineData("Gateway", "Gateway 14.1 Ultra Slim", "gateway-basic")]
    [InlineData("CHUWI", "CoreBook X", "chuwi-basic")]
    [InlineData("TECLAST", "F15 Plus", "teclast-basic")]
    [InlineData("Jumper", "EZbook X3", "jumper-basic")]
    [InlineData("MEDION AG", "ERAZER Beast X40", "medion-basic")]
    [InlineData("XMG", "XMG Neo 16", "xmg-schenker-basic")]
    [InlineData("MAIBENBEN", "MaiBook X", "maibenben-basic")]
    [InlineData("TUXEDO", "InfinityBook Pro 14", "xmg-schenker-basic")]
    [InlineData("Valve Corporation", "Steam Deck", "valve-handheld-basic")]
    [InlineData("GPD", "GPD WIN 4", "gpd-handheld-basic")]
    [InlineData("AYANEO", "AYANEO NEXT", "ayaneo-handheld-basic")]
    [InlineData("AYN", "Odin2 Pro", "ayaneo-handheld-basic")]
    [InlineData("ANBERNIC", "RG556", "anbernic-handheld-basic")]
    [InlineData("Retroid", "Retroid Pocket 5", "retroid-handheld-basic")]
    [InlineData("Orange Pi", "Orange Pi Neo", "orange-pi-handheld-basic")]
    [InlineData("ONEXPLAYER", "OneXPlayer 2 Pro", "one-netbook-handheld-basic")]
    [InlineData("MINISFORUM", "Venus Series UM790 Pro", "minisforum-basic")]
    [InlineData("AZW", "Beelink SER8", "beelink-basic")]
    [InlineData("GEEKOM", "Mini IT13", "geekom-basic")]
    [InlineData("ZOTAC", "ZBOX MAGNUS", "zotac-basic")]
    [InlineData("System76", "Oryx Pro oryp13", "system76-basic")]
    [InlineData("Notebook", "oryp13", "system76-basic")]
    [InlineData("Star Labs Systems", "StarFighter", "star-labs-basic")]
    [InlineData("SLIMBOOK", "KDE Slimbook VII", "slimbook-basic")]
    [InlineData("Monster Notebook", "Tulpar T7 V20", "monster-tulpar-basic")]
    [InlineData("Dream Machines", "RG4070-16", "dream-machines-basic")]
    [InlineData("PC Specialist Ltd", "Recoil 17", "pcspecialist-basic")]
    [InlineData("Eurocom Corporation", "Nightsky ARX15", "eurocom-basic")]
    [InlineData("ORIGIN PC", "EON16-S", "origin-pc-basic")]
    [InlineData("iBUYPOWER", "Y60 Gaming Desktop", "cyberpower-ibuypower-basic")]
    [InlineData("Casper Bilgisayar", "Excalibur G870", "casper-excalibur-basic")]
    [InlineData("AVITA", "LIBER V14", "nexstgo-avita-basic")]
    [InlineData("Positivo Tecnologia", "Motion C4500", "positivo-basic")]
    [InlineData("Wortmann AG", "TERRA MOBILE 1517", "wortmann-terra-basic")]
    [InlineData("Shinelon", "Shinelon X7", "shinelon-basic")]
    [InlineData("DERE", "Dere R9 Pro", "dere-basic")]
    [InlineData("TCL", "TCL Book 14 Go", "tcl-basic")]
    [InlineData("ADATA Technology Co., Ltd.", "XPG Xenia 15G", "adata-xpg-basic")]
    [InlineData("TRANSSION HOLDINGS", "Tecno Megabook T1", "transsion-basic")]
    [InlineData("Multilaser Industrial S.A.", "Multilaser Ultra", "multilaser-basic")]
    [InlineData("Vestel Elektronik", "Vestel Laptop 15", "vestel-basic")]
    [InlineData("Axioo International", "Axioo Hype 5", "axioo-basic")]
    [InlineData("Advan Digital", "Advan Soulmate", "advan-basic")]
    [InlineData("MeLE", "Quieter4C", "mele-basic")]
    [InlineData("NiPoGi", "AK1 Plus Mini PC", "regional-mini-pc-basic")]
    [InlineData("PELADN", "HA-4 Mini PC", "regional-mini-pc-basic")]
    [InlineData("BMAX", "BMAX X15", "bmax-ninkear-basic")]
    [InlineData("Ninkear", "Ninkear N15 Pro", "bmax-ninkear-basic")]
    [InlineData("ASRock", "Z790 Taichi", "universal-motherboard-basic")]
    [InlineData("Super Micro Computer, Inc.", "X13 Workstation", "universal-workstation-basic")]
    [InlineData("Default string", "System Product Name", "universal-desktop-basic")]
    [InlineData("To Be Filled By O.E.M.", "To Be Filled By O.E.M.", "universal-motherboard-basic")]
    public void Evaluate_WhenBasicDevicePackMatches_ShouldHideHardwareControls(string vendor, string model, string expectedPackId)
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = vendor,
            MachineType = "0000",
            Model = model
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeFalse();
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be(expectedPackId);
        availability.EnabledFeatures.Should().Contain(["system-optimization", "language"]);
        availability.HiddenFeatures.Should().Contain(["lenovo-hardware-controls", "power-modes", "gpu-overclock"]);
    }

    [Theory]
    [InlineData("Hasee", "ZhanShen Z8", "hasee-basic")]
    [InlineData("THUNDEROBOT", "911 Zero", "thunderobot-basic")]
    [InlineData("MACHENIKE", "F117", "machenike-basic")]
    [InlineData("COLORFUL", "Evol X15", "colorful-basic")]
    [InlineData("MECHREVO", "Jiaolong 16", "mechrevo-basic")]
    [InlineData("Tongfang", "Eluktronics MECH-16", "clevo-tongfang-basic")]
    public void Evaluate_WhenDomesticBarebonePackMatches_ShouldEnableHardwareControls(string vendor, string model, string expectedPackId)
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = vendor,
            MachineType = "0000",
            Model = model
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be(expectedPackId);
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
    }

    [Theory]
    [InlineData("Samsung Electronics Co Ltd", "Galaxy Book4 Pro", "samsung-basic")]
    [InlineData("Apple Computer Inc", "MacBook Air", "apple-basic")]
    [InlineData("Dynabook Incorporated", "Portégé X40", "dynabook-basic")]
    [InlineData("ASROCK INC", "B650M Pro RS", "universal-motherboard-basic")]
    [InlineData("SUPER MICRO COMPUTER INC", "SYS-741GE", "universal-motherboard-basic")]
    public void Evaluate_WhenVendorUsesDifferentDmiFormatting_ShouldStillMatchBasicPack(string vendor, string model, string expectedPackId)
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = vendor,
            MachineType = "0000",
            Model = model
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be(expectedPackId);
    }

    [Fact]
    public void Evaluate_WhenVendorIsEmpty_ShouldReturnNamedGenericBasicPack()
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = "",
            MachineType = "",
            Model = ""
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeFalse();
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be("generic-pc-basic");
        availability.EnabledFeatures.Should().Contain(["system-optimization", "theme"]);
        availability.HiddenFeatures.Should().Contain(["lenovo-hardware-controls", "keyboard-backlight"]);
    }

    [Fact]
    public void Evaluate_WhenVendorIsEmptyButBaseBoardIdentifiesCustomPc_ShouldUseMotherboardPack()
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = "",
            MachineType = "",
            Model = "",
            Hardware = new()
            {
                BaseBoard = new()
                {
                    Manufacturer = "ASRock",
                    Product = "B650M Pro RS"
                }
            }
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeFalse();
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be("universal-motherboard-basic");
        availability.HiddenFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Fact]
    public void Evaluate_WhenDmiModelIsGenericButChassisIdentifiesDesktop_ShouldUseDesktopPack()
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = "Unknown",
            MachineType = "",
            Model = "Default string",
            Hardware = new()
            {
                Chassis = new()
                {
                    ChassisTypes = [3]
                }
            }
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeFalse();
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be("universal-desktop-basic");
    }

    [Theory]
    [InlineData("ACME", "Unknown Laptop", "generic-pc-basic")]
    [InlineData("Unknown", "Desktop PC", "universal-desktop-basic")]
    [InlineData("Custom", "Custom Workstation", "universal-workstation-basic")]
    public void Evaluate_WhenOnlyGenericClassMatches_ShouldUseUniversalBasicPack(string vendor, string model, string expectedPackId)
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = vendor,
            MachineType = "0000",
            Model = model
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeFalse();
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be(expectedPackId);
        availability.HiddenFeatures.Should().Contain(["lenovo-hardware-controls", "power-modes", "fan-curve"]);
    }

    [Fact]
    public void Evaluate_WhenMachineTypeMatchesHardwarePack_ShouldPreferItOverLenovoBasicKeyword()
    {
        // Arrange
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "83DH",
            Model = "Lenovo Slim 5"
        };

        // Act
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        // Assert
        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("lenovo-legion-slim-5");
        availability.EnabledFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Fact]
    public void Evaluate_WhenInstalledCatalogMatches_ShouldEnableInstalledPack()
    {
        // Arrange
        var provider = LenovoDeviceSupportProvider.Instance;
        provider.SetInstalledCatalog(new DeviceSupportCatalog
        {
            DevicePacks =
            [
                new DevicePack
                {
                    Id = "lenovo-custom-installed",
                    DisplayName = "Lenovo Custom Installed",
                    Vendor = "LENOVO",
                    Families = ["Lenovo"],
                    MachineTypes = ["9999"],
                    EnabledFeatures = ["lenovo-hardware-controls"]
                }
            ]
        });
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "9999",
            Model = "Lenovo Future Device"
        };

        try
        {
            // Act
            var availability = provider.Evaluate(machineInformation);

            // Assert
            availability.IsSupported.Should().BeTrue();
            availability.IsBasicMode.Should().BeFalse();
            availability.DevicePackId.Should().Be("lenovo-custom-installed");
        }
        finally
        {
            provider.SetInstalledCatalog(null);
        }
    }

    [Fact]
    public void Evaluate_WhenInstalledCatalogMatchesOnlyFamilySignal_ShouldUseInstalledPack()
    {
        // Arrange
        var provider = LenovoDeviceSupportProvider.Instance;
        provider.SetInstalledCatalog(new DeviceSupportCatalog
        {
            DevicePacks =
            [
                new DevicePack
                {
                    Id = "asus-rog-family-installed",
                    DisplayName = "ASUS ROG Family Installed",
                    Vendor = "ASUS",
                    Families = ["ROG"],
                    EnabledFeatures = ["system-optimization"],
                    HiddenFeatures = ["lenovo-hardware-controls"]
                }
            ]
        });
        var machineInformation = MachineInformationTestData.WithComputerSystem(
            "ASUS",
            "",
            "ASUSTeK COMPUTER INC.",
            "",
            "ROG");

        try
        {
            // Act
            var availability = provider.Evaluate(machineInformation);

            // Assert
            availability.IsSupported.Should().BeFalse();
            availability.IsBasicMode.Should().BeTrue();
            availability.DevicePackId.Should().Be("asus-rog-family-installed");
            availability.HiddenFeatures.Should().Contain("lenovo-hardware-controls");
        }
        finally
        {
            provider.SetInstalledCatalog(null);
        }
    }

    [Fact]
    public void Evaluate_WhenPreferredPackIsSet_ShouldOverrideAutoDetect()
    {
        var provider = LenovoDeviceSupportProvider.Instance;
        provider.SetPreferredDevicePackId("lenovo-thinkpad-basic");
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "83DF",
            Model = "Legion Y9000P IRX9"
        };

        try
        {
            var availability = provider.Evaluate(machineInformation);

            availability.IsSupported.Should().BeFalse();
            availability.DevicePackId.Should().Be("lenovo-thinkpad-basic");
            availability.HiddenFeatures.Should().Contain("lenovo-hardware-controls");
        }
        finally
        {
            provider.SetPreferredDevicePackId(null);
        }
    }

    [Fact]
    public void Evaluate_WhenPreferredPackIsGenericBasic_ShouldStayInBasicMode()
    {
        var provider = LenovoDeviceSupportProvider.Instance;
        provider.SetPreferredDevicePackId(CatalogDeviceSupportProvider.GenericBasicPackId);
        var machineInformation = new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "83DF",
            Model = "Legion Y9000P IRX9"
        };

        try
        {
            var availability = provider.Evaluate(machineInformation);

            availability.IsSupported.Should().BeFalse();
            availability.IsBasicMode.Should().BeTrue();
            availability.DevicePackId.Should().Be(CatalogDeviceSupportProvider.GenericBasicPackId);
        }
        finally
        {
            provider.SetPreferredDevicePackId(null);
        }
    }

    [Fact]
    public void Evaluate_WhenMachineTypeHasCtoSuffix_ShouldEnablePro5()
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "83DFCTO1WW",
            Model = "Legion Y9000P IRX9"
        });

        availability.IsSupported.Should().BeTrue();
        availability.DevicePackId.Should().Be("lenovo-legion-pro-5");
    }

    [Fact]
    public void Evaluate_WhenLenovoModelIsUnrecognized_ShouldUseGenericBasic()
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(new MachineInformation
        {
            Vendor = "LENOVO",
            MachineType = "0000",
            Model = "Unknown Device"
        });

        availability.IsSupported.Should().BeFalse();
        availability.DevicePackId.Should().Be(CatalogDeviceSupportProvider.GenericBasicPackId);
    }

    [Fact]
    public void Evaluate_WhenLaptopChassisIsNotebook_ShouldNotSelectBarebonePack()
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(
            MachineInformationTestData.WithChassis("ACME", "Unknown Laptop", "", [10]));

        availability.IsSupported.Should().BeFalse();
        availability.DevicePackId.Should().Be(CatalogDeviceSupportProvider.GenericBasicPackId);
    }
}
