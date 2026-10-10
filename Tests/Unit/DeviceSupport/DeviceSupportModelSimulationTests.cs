using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.DeviceSupport;
using UniversalDeviceToolkit.Tests.Infrastructure;
using Xunit;

namespace UniversalDeviceToolkit.Tests.DeviceSupport;

[Trait("Category", TestCategories.Unit)]
[Collection(TestCollections.ProcessState)]
public sealed class DeviceSupportModelSimulationTests
{
    public DeviceSupportModelSimulationTests()
    {
        LenovoDeviceSupportProvider.Instance.SetInstalledCatalog(null);
        LenovoDeviceSupportProvider.Instance.SetPreferredDevicePackId(null);
    }

    [Theory]
    [MemberData(nameof(MultiBrandBasicModeScenarios))]
    public void Evaluate_WithSimulatedBrandModel_ShouldUseExpectedBasicPack(MachineInformation machineInformation, string expectedPackId)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeFalse();
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be(expectedPackId);
        availability.EnabledFeatures.Should().Contain(["system-optimization", "language", "theme"]);
        availability.HiddenFeatures.Should().Contain(["lenovo-hardware-controls", "power-modes", "gpu-overclock", "fan-curve"]);
    }

    [Theory]
    [MemberData(nameof(HardwareSignalScenarios))]
    public void Evaluate_WithSimulatedHardwareInventorySignals_ShouldUseExpectedBasicPack(MachineInformation machineInformation, string expectedPackId)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be(expectedPackId);
        availability.HiddenFeatures.Should().Contain("lenovo-hardware-controls");
    }

    [Theory]
    [InlineData("ERAZER", "Beast X40", "medion-basic")]
    public void Evaluate_WhenDmiVendorUsesGamingSubBrandAndModelUsesSku_ShouldUseParentBasicPack(
        string vendor,
        string model,
        string expectedPackId)
    {
        var machineInformation = MachineInformationTestData.Create(vendor, model);

        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeFalse();
        availability.IsBasicMode.Should().BeTrue();
        availability.DevicePackId.Should().Be(expectedPackId);
        availability.EnabledFeatures.Should().Contain(["system-optimization", "language", "theme"]);
        availability.HiddenFeatures.Should().Contain(["lenovo-hardware-controls", "power-modes", "gpu-overclock", "fan-curve"]);
    }

    [Fact]
    public void Evaluate_WithSimulatedLegionMachineType_ShouldStillEnableLenovoHardwarePack()
    {
        var machineInformation = MachineInformationTestData.Create(
            "LENOVO",
            "Legion Y9000P IRX9",
            "83DE");

        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("lenovo-legion-pro-7");
        availability.EnabledFeatures.Should().Contain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
    }

    [Theory]
    [MemberData(nameof(AsusHardwareScenarios))]
    public void Evaluate_WithSimulatedAsusMachine_ShouldUseAsusHardwarePack(MachineInformation machineInformation)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("asus-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["fan-curve", "gpu-overclock"]);
    }

    public static TheoryData<MachineInformation> AsusHardwareScenarios() => new()
    {
        MachineInformationTestData.Create("ASUSTeK COMPUTER INC.", "ROG Strix SCAR 18 G835"),
        MachineInformationTestData.Create("ASUS", "TUF Gaming A16 FA608"),
        MachineInformationTestData.Create("ASUSTEK COMPUTER INCORPORATED", "Zenbook S 14 UX5406"),
        MachineInformationTestData.Create("ROG", "G835"),
        MachineInformationTestData.Create("TUF Gaming", "FA608"),
        MachineInformationTestData.WithComputerSystem("", "", "ASUSTeK COMPUTER INC.", "System Product Name", "ROG Flow"),
    };

    [Theory]
    [MemberData(nameof(HpHardwareScenarios))]
    public void Evaluate_WithSimulatedHpMachine_ShouldUseHpHardwarePack(MachineInformation machineInformation)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("hp-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["fan-curve", "gpu-overclock"]);
    }

    public static TheoryData<MachineInformation> HpHardwareScenarios() => new()
    {
        MachineInformationTestData.Create("HP", "OMEN Max 16"),
        MachineInformationTestData.Create("HP Inc.", "Victus 16"),
        MachineInformationTestData.Create("Hewlett-Packard Company", "OMEN Transcend 14"),
        MachineInformationTestData.Create("OMEN", "Max 16"),
        MachineInformationTestData.Create("Victus", "16"),
        MachineInformationTestData.WithComputerSystem("", "", "HP", "OMEN Transcend Laptop 14"),
    };

    [Theory]
    [MemberData(nameof(DellHardwareScenarios))]
    public void Evaluate_WithSimulatedAlienwareMachine_ShouldUseDellHardwarePack(MachineInformation machineInformation)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("dell-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["fan-curve", "gpu-overclock"]);
    }

    public static TheoryData<MachineInformation> DellHardwareScenarios() => new()
    {
        MachineInformationTestData.Create("Dell Inc.", "Alienware m18 R2"),
        MachineInformationTestData.Create("Dell Computer Corporation", "Dell G16 7630"),
        MachineInformationTestData.Create("Alienware", "m18 R2"),
        MachineInformationTestData.Create("Dell", "G15 5530"),
        MachineInformationTestData.WithComputerSystem("", "", "Dell Inc.", "Alienware m18 R2"),
    };

    [Theory]
    [MemberData(nameof(AcerHardwareScenarios))]
    public void Evaluate_WithSimulatedAcerMachine_ShouldUseAcerHardwarePack(MachineInformation machineInformation)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("acer-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["fan-curve", "gpu-overclock"]);
    }

    public static TheoryData<MachineInformation> AcerHardwareScenarios() => new()
    {
        MachineInformationTestData.Create("Acer Incorporated", "Predator Helios Neo 16"),
        MachineInformationTestData.Create("Acer", "Nitro V 16"),
        MachineInformationTestData.Create("Predator", "PHN16"),
        MachineInformationTestData.Create("Nitro", "ANV16"),
        MachineInformationTestData.WithBaseBoard("", "", "Acer", "Nitro ANV16"),
    };

    [Theory]
    [MemberData(nameof(GigabyteHardwareScenarios))]
    public void Evaluate_WithSimulatedGigabyteMachine_ShouldUseGigabyteSensorsOnlyPack(MachineInformation machineInformation)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("gigabyte-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors"]);
        availability.EnabledFeatures.Should().NotContain("power-modes");
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["power-modes", "fan-curve", "gpu-overclock"]);
    }

    public static TheoryData<MachineInformation> GigabyteHardwareScenarios() => new()
    {
        MachineInformationTestData.Create("Gigabyte Technology Co., Ltd.", "AORUS 16X"),
        MachineInformationTestData.Create("GIGABYTE", "AERO 16 OLED"),
        MachineInformationTestData.Create("AORUS", "16X"),
    };

    [Theory]
    [MemberData(nameof(MsiHardwareScenarios))]
    public void Evaluate_WithSimulatedMsiMachine_ShouldUseMsiHardwarePack(MachineInformation machineInformation)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("msi-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["fan-curve", "gpu-overclock"]);
    }

    public static TheoryData<MachineInformation> MsiHardwareScenarios() => new()
    {
        MachineInformationTestData.Create("Micro-Star International Co., Ltd.", "MSI Raider 18"),
        MachineInformationTestData.Create("MSI", "Katana 15 B13V"),
        MachineInformationTestData.Create("MICRO STAR INTERNATIONAL CO LTD", "Cyborg 15"),
    };

    [Theory]
    [MemberData(nameof(MechrevoHardwareScenarios))]
    public void Evaluate_WithSimulatedMechrevoMachine_ShouldUseMechrevoHardwarePack(MachineInformation machineInformation)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("mechrevo-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["fan-curve", "gpu-overclock"]);
    }

    public static TheoryData<MachineInformation> MechrevoHardwareScenarios() => new()
    {
        MachineInformationTestData.Create("MECHREVO", "Jiaolong 16 Pro"),
        MachineInformationTestData.Create("Mechanical Revolution", "Kuangshi 16 Super"),
        MachineInformationTestData.Create("Tongfang", "Code 01"),
    };

    [Theory]
    [MemberData(nameof(HaseeHardwareScenarios))]
    public void Evaluate_WithSimulatedHaseeMachine_ShouldUseHaseeHardwarePack(MachineInformation machineInformation)
    {
        var availability = LenovoDeviceSupportProvider.Instance.Evaluate(machineInformation);

        availability.IsSupported.Should().BeTrue();
        availability.IsBasicMode.Should().BeFalse();
        availability.DevicePackId.Should().Be("hasee-basic");
        availability.EnabledFeatures.Should().Contain(["lenovo-hardware-controls", "sensors", "power-modes"]);
        availability.HiddenFeatures.Should().NotContain("lenovo-hardware-controls");
        availability.HiddenFeatures.Should().Contain(["fan-curve", "gpu-overclock"]);
    }

    public static TheoryData<MachineInformation> HaseeHardwareScenarios() => new()
    {
        MachineInformationTestData.Create("Hasee", "ZhanShen T8"),
        MachineInformationTestData.Create("Hasee Computer", "ZhanShen Z8"),
    };

    public static TheoryData<MachineInformation, string> MultiBrandBasicModeScenarios() => new()
    {
        { MachineInformationTestData.Create("LENOVO", "IdeaPad Flex 5 Chromebook Plus"), "lenovo-chromebook-basic" },
        { MachineInformationTestData.Create("Google LLC", "Pixelbook Go"), "google-chromebook-basic" },
        { MachineInformationTestData.Create("SAMSUNG ELECTRONICS CO., LTD.", "Galaxy Chromebook Plus"), "samsung-basic" },
        { MachineInformationTestData.Create("Xiaomi Corporation", "Xiaomi Book Pro 16"), "xiaomi-basic" },
        { MachineInformationTestData.Create("TIMI", "Redmi G Pro 2024"), "xiaomi-basic" },
        { MachineInformationTestData.Create("Huawei Technologies Co., Ltd.", "MateBook GT 14"), "huawei-basic" },
        { MachineInformationTestData.Create("HUAWEI", "Qingyun L540"), "huawei-basic" },
        { MachineInformationTestData.Create("Apple Inc.", "MacBook Air"), "apple-basic" },
        { MachineInformationTestData.Create("Honor Device Co., Ltd.", "MagicBook X 16"), "honor-basic" },
        { MachineInformationTestData.Create("LG", "LG gram Pro 16"), "lg-basic" },
        { MachineInformationTestData.Create("Framework Computer Inc.", "Framework Laptop 13"), "framework-basic" },
        { MachineInformationTestData.Create("MAIBENBEN", "Xiaomai 6"), "maibenben-basic" },
        { MachineInformationTestData.Create("Valve Corporation", "Steam Deck"), "valve-handheld-basic" },
        { MachineInformationTestData.Create("GPD", "GPD Win Mini"), "gpd-handheld-basic" },
        { MachineInformationTestData.Create("AYANEO", "AYANEO Air Plus"), "ayaneo-handheld-basic" },
        { MachineInformationTestData.Create("Ayn Technologies", "AYN Odin2 Mini"), "ayaneo-handheld-basic" },
        { MachineInformationTestData.Create("ANBERNIC", "Win600"), "anbernic-handheld-basic" },
        { MachineInformationTestData.Create("RETROID", "Retroid Pocket 5"), "retroid-handheld-basic" },
        { MachineInformationTestData.Create("Shenzhen Xunlong Software", "Orange Pi Neo"), "orange-pi-handheld-basic" },
        { MachineInformationTestData.Create("ONEXPLAYER", "OneXPlayer Mini Pro"), "one-netbook-handheld-basic" },
        { MachineInformationTestData.Create("MINISFORUM", "UM890 Pro"), "minisforum-basic" },
        { MachineInformationTestData.Create("AZW", "Beelink GTR7"), "beelink-basic" },
        { MachineInformationTestData.Create("GEEKOM", "MiniAir 12"), "geekom-basic" },
        { MachineInformationTestData.Create("ZOTAC", "ZBOX CI669"), "zotac-basic" },
        { MachineInformationTestData.Create("System76", "Lemur Pro lemp13"), "system76-basic" },
        { MachineInformationTestData.Create("Star Labs Systems", "StarBook"), "star-labs-basic" },
        { MachineInformationTestData.Create("SLIMBOOK", "Executive 16"), "slimbook-basic" },
        { MachineInformationTestData.Create("TUXEDO", "Stellaris 16"), "xmg-schenker-basic" },
        { MachineInformationTestData.Create("NEC Personal Computers, Ltd.", "LAVIE NEXTREME Carbon"), "nec-lavie-basic" },
        { MachineInformationTestData.Create("Sharp Corporation", "Mebius Chromebook"), "sharp-basic" },
        { MachineInformationTestData.Create("Monster Notebook", "Tulpar T7 V20"), "monster-tulpar-basic" },
        { MachineInformationTestData.Create("Dream Machines", "RG4070-16"), "dream-machines-basic" },
        { MachineInformationTestData.Create("PC Specialist Ltd", "Recoil 17"), "pcspecialist-basic" },
        { MachineInformationTestData.Create("Eurocom Corporation", "Nightsky ARX15"), "eurocom-basic" },
        { MachineInformationTestData.Create("Eluktronics", "MECH-17 GP3"), "eluktronics-basic" },
        { MachineInformationTestData.Create("MAINGEAR", "Vector Pro 2"), "maingear-basic" },
        { MachineInformationTestData.Create("ORIGIN PC", "EON16-S"), "origin-pc-basic" },
        { MachineInformationTestData.Create("Corsair", "Voyager a1600"), "corsair-basic" },
        { MachineInformationTestData.Create("iBUYPOWER", "Y60 Gaming Desktop"), "cyberpower-ibuypower-basic" },
        { MachineInformationTestData.Create("Casper Bilgisayar", "Excalibur G870"), "casper-excalibur-basic" },
        { MachineInformationTestData.Create("AVITA", "LIBER V14"), "nexstgo-avita-basic" },
        { MachineInformationTestData.Create("Positivo Tecnologia", "Motion C4500"), "positivo-basic" },
        { MachineInformationTestData.Create("Wortmann AG", "TERRA MOBILE 1517"), "wortmann-terra-basic" },
        { MachineInformationTestData.Create("GMKtec", "NucBox K8 Plus"), "gmktec-basic" },
        { MachineInformationTestData.Create("MORE FINE", "M600 Mini PC"), "morefine-basic" },
        { MachineInformationTestData.Create("ACEMAGICIAN", "Tank03"), "acemagic-basic" },
        { MachineInformationTestData.Create("AOOSTAR", "GEM12 Pro"), "aoostar-basic" },
        { MachineInformationTestData.Create("TRIGKEY", "Mini PC S7"), "regional-mini-pc-basic" },
        { MachineInformationTestData.Create("Topdon", "TC001 Mini PC"), "regional-mini-pc-basic" },
        { MachineInformationTestData.Create("KTC", "KTC Mini PC"), "regional-mini-pc-basic" },
        { MachineInformationTestData.Create("Mele", "Quieter4C"), "mele-basic" },
        { MachineInformationTestData.Create("N-ONE", "N-one Nbook Fly"), "bmax-ninkear-basic" }
    };

    public static TheoryData<MachineInformation, string> HardwareSignalScenarios() => new()
    {
        {
            MachineInformationTestData.WithBaseBoard(
                "",
                "",
                "MAIBENBEN",
                "MaiBook Series"),
            "maibenben-basic"
        },
        {
            MachineInformationTestData.WithComputerSystem(
                "",
                "",
                "TIMI",
                "RedmiBook Pro 15"),
            "xiaomi-basic"
        },
        {
            MachineInformationTestData.WithComputerSystem(
                "",
                "",
                "Huawei Technologies Co., Ltd.",
                "MateBook D 16"),
            "huawei-basic"
        },
        {
            MachineInformationTestData.WithComputerSystem(
                "",
                "",
                "Apple Inc.",
                "MacBookPro18,3",
                "MacBook Pro"),
            "apple-basic"
        },
        {
            MachineInformationTestData.WithComputerSystem(
                "",
                "",
                "AZW",
                "SER8"),
            "beelink-basic"
        },
        {
            MachineInformationTestData.WithComputerSystem(
                "",
                "",
                "Valve Corporation",
                "Steam Deck"),
            "valve-handheld-basic"
        },
        {
            MachineInformationTestData.WithComputerSystem(
                "",
                "",
                "Moorechip Technologies",
                "Retroid Pocket 5"),
            "retroid-handheld-basic"
        },
        {
            MachineInformationTestData.WithComputerSystem(
                "",
                "",
                "Shenzhen MeLE Digital Technology",
                "Quieter4C"),
            "mele-basic"
        },
        {
            MachineInformationTestData.WithComputerSystem(
                "",
                "",
                "Notebook",
                "oryp13"),
            "system76-basic"
        },
        {
            MachineInformationTestData.WithBaseBoard(
                "",
                "",
                "StarLabs",
                "StarLite Mk V"),
            "star-labs-basic"
        },
        {
            MachineInformationTestData.WithChassis(
                "Unknown",
                "System Product Name",
                "",
                [3]),
            "universal-desktop-basic"
        }
    };
}
