using FluentAssertions;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using LibreHardwareMonitor.Hardware;
using Moq;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class SensorsGroupControllerTests
{
    [Fact]
    public void EnumerateHardwareTree_ShouldIncludeNestedSubHardware()
    {
        var embeddedController = CreateHardware("Embedded Controller", HardwareType.Motherboard);
        var lpc = CreateHardware("LPC", HardwareType.Motherboard, embeddedController.Object);
        var motherboard = CreateHardware("Motherboard", HardwareType.Motherboard, lpc.Object);
        var cpu = CreateHardware("CPU", HardwareType.Cpu);

        var result = SensorsGroupController.EnumerateHardwareTree([motherboard.Object, cpu.Object])
            .Select(hardware => hardware.Name)
            .ToArray();

        result.Should().Equal("Motherboard", "LPC", "Embedded Controller", "CPU");
    }

    [Fact]
    public void SelectCpuTemperatureSensorName_ShouldPreferPackageSensors()
    {
        var result = SensorSelector.SelectCpuTemperatureSensorName(
        [
            "Core #1",
            "CPU Package",
            "Core Max"
        ]);

        result.Should().Be("CPU Package");
    }

    [Fact]
    public void SelectCpuTemperatureSensorName_WhenPackageMissing_ShouldPreferCoreMax()
    {
        var result = SensorSelector.SelectCpuTemperatureSensorName(
        [
            "Core #1",
            "Core Max",
            "VRM"
        ]);

        result.Should().Be("Core Max");
    }

    [Fact]
    public void SelectCpuTemperatureSensorName_ShouldPreferTctlTdieBeforeGenericCpuSensors()
    {
        var result = SensorSelector.SelectCpuTemperatureSensorName(
        [
            "CPU",
            "Tctl/Tdie",
            "Core #1"
        ]);

        result.Should().Be("Tctl/Tdie");
    }

    [Fact]
    public void SelectCpuTemperatureSensorName_ShouldRecognizeProcessorPackageTemperatureAliases()
    {
        var result = SensorSelector.SelectCpuTemperatureSensorName(
        [
            "Core #0",
            "Processor Package Temperature"
        ]);

        result.Should().Be("Processor Package Temperature");
    }

    [Fact]
    public void SelectCpuTemperatureSensorName_ShouldRecognizeCpuZAndHwInfoDieAliases()
    {
        var dieResult = SensorSelector.SelectCpuTemperatureSensorName(
        [
            "Core #0",
            "CPU Die"
        ]);
        var ccdResult = SensorSelector.SelectCpuTemperatureSensorName(
        [
            "Core #0",
            "CPU CCD1"
        ]);
        var junctionResult = SensorSelector.SelectCpuTemperatureSensorName(
        [
            "Core #0",
            "Tjunction"
        ]);

        dieResult.Should().Be("CPU Die");
        ccdResult.Should().Be("CPU CCD1");
        junctionResult.Should().Be("Tjunction");
    }

    [Fact]
    public void SelectCpuVoltageSensorName_ShouldPreferCoreVoltageAliases()
    {
        var result = SensorSelector.SelectCpuVoltageSensorName(
        [
            "System Agent Voltage",
            "CPU Core Voltage",
            "Cache Voltage"
        ]);

        result.Should().Be("CPU Core Voltage");
    }

    [Fact]
    public void SelectCpuVoltageSensorName_ShouldPreferVidBeforeGenericAgentOrCacheVoltage()
    {
        var result = SensorSelector.SelectCpuVoltageSensorName(
        [
            "Cache Voltage",
            "IA VID",
            "System Agent Voltage"
        ]);

        result.Should().Be("IA VID");
    }

    [Fact]
    public void SelectCpuVoltageSensorName_ShouldRecognizeCpuZAndAmdVoltageAliases()
    {
        var cpuZResult = SensorSelector.SelectCpuVoltageSensorName(
        [
            "CPU VID",
            "System Agent Voltage"
        ]);
        var amdResult = SensorSelector.SelectCpuVoltageSensorName(
        [
            "SVI2 TFN",
            "Cache Voltage"
        ]);

        cpuZResult.Should().Be("CPU VID");
        amdResult.Should().Be("SVI2 TFN");
    }

    [Fact]
    public void SelectCpuVoltageSensorName_ShouldRecognizeVddAndVccCoreAliases()
    {
        var vddResult = SensorSelector.SelectCpuVoltageSensorName(
        [
            "System Agent Voltage",
            "CPU VDD"
        ]);
        var vddcrResult = SensorSelector.SelectCpuVoltageSensorName(
        [
            "Cache Voltage",
            "VDDCR CPU"
        ]);
        var vccResult = SensorSelector.SelectCpuVoltageSensorName(
        [
            "SA Voltage",
            "VCC Core"
        ]);

        vddResult.Should().Be("CPU VDD");
        vddcrResult.Should().Be("VDDCR CPU");
        vccResult.Should().Be("VCC Core");
    }

    [Fact]
    public void SelectCpuVoltageSensorName_ShouldRecognizeInputAndEffectiveVidAliases()
    {
        var inputResult = SensorSelector.SelectCpuVoltageSensorName(
        [
            "SA Voltage",
            "VCCIN"
        ]);
        var vidResult = SensorSelector.SelectCpuVoltageSensorName(
        [
            "System Agent Voltage",
            "CPU Core VID Effective"
        ]);
        var iaResult = SensorSelector.SelectCpuVoltageSensorName(
        [
            "Cache Voltage",
            "IA Voltage"
        ]);

        inputResult.Should().Be("VCCIN");
        vidResult.Should().Be("CPU Core VID Effective");
        iaResult.Should().Be("IA Voltage");
    }

    [Fact]
    public void IsLikelyCpuHybridCoreClockSensorName_ShouldRecognizeCommonAliases()
    {
        string[] pCoreNames =
        [
            "CPU P-Core #0",
            "CPU P Core 1",
            "Performance Core #2 Clock",
            "CPU Performance Core 3"
        ];
        string[] eCoreNames =
        [
            "CPU E-Core #8",
            "CPU E Core 9",
            "Efficient Core #10 Clock",
            "Efficiency Core 11"
        ];

        pCoreNames.Should().OnlyContain(name => SensorSelector.IsLikelyCpuPCoreClockSensorName(name));
        eCoreNames.Should().OnlyContain(name => SensorSelector.IsLikelyCpuECoreClockSensorName(name));
    }

    [Fact]
    public void IsLikelyCpuHybridCoreClockSensorName_ShouldIgnoreAverageAndEffectiveClocks()
    {
        SensorSelector.IsLikelyCpuPCoreClockSensorName("P-Core Average").Should().BeFalse();
        SensorSelector.IsLikelyCpuPCoreClockSensorName("P-Core Effective Clock").Should().BeFalse();
        SensorSelector.IsLikelyCpuECoreClockSensorName("E-Core Average").Should().BeFalse();
        SensorSelector.IsLikelyCpuECoreClockSensorName("E-Core Effective Clock").Should().BeFalse();
    }

    [Fact]
    public void SelectCpuPackagePowerSensorName_ShouldRecognizeAmdPptAlias()
    {
        var result = SensorSelector.SelectCpuPackagePowerSensorName(
        [
            "GPU Package",
            "CPU Core",
            "CPU PPT"
        ]);

        result.Should().Be("CPU PPT");
    }

    [Fact]
    public void SelectCpuPackagePowerSensorName_ShouldRecognizeProcessorPackagePowerAliases()
    {
        var result = SensorSelector.SelectCpuPackagePowerSensorName(
        [
            "IA Cores",
            "Processor Package Power",
            "GPU Package"
        ]);

        result.Should().Be("Processor Package Power");
    }

    [Fact]
    public void SelectCpuPackagePowerSensorName_ShouldRecognizeAmdApuPowerAliases()
    {
        var stapmResult = SensorSelector.SelectCpuPackagePowerSensorName(
        [
            "CPU Core",
            "CPU SoC",
            "APU STAPM"
        ]);
        var coreSocResult = SensorSelector.SelectCpuPackagePowerSensorName(
        [
            "CPU Core",
            "CPU SoC",
            "Core+SoC Power"
        ]);
        var socketResult = SensorSelector.SelectCpuPackagePowerSensorName(
        [
            "CPU Core",
            "CPU Socket Power"
        ]);

        stapmResult.Should().Be("APU STAPM");
        coreSocResult.Should().Be("Core+SoC Power");
        socketResult.Should().Be("CPU Socket Power");
    }

    [Fact]
    public void SelectCpuPackagePowerSensorName_ShouldRecognizeAmdMobileSpptAliases()
    {
        var apuResult = SensorSelector.SelectCpuPackagePowerSensorName(
        [
            "CPU Core Power (SVI3 TFN)",
            "CPU SoC Power (SVI3 TFN)",
            "APU sPPT"
        ]);
        var cpuResult = SensorSelector.SelectCpuPackagePowerSensorName(
        [
            "CPU Core",
            "CPU sPPT"
        ]);

        apuResult.Should().Be("APU sPPT");
        cpuResult.Should().Be("CPU sPPT");
    }

    [Fact]
    public void SelectCpuUsageSensorName_ShouldPreferCpuTotalBeforeCoreMaxOrThreadLoads()
    {
        var result = SensorSelector.SelectCpuUsageSensorName(
        [
            "CPU Core Max",
            "CPU Core #1 Thread #1",
            "CPU Total"
        ]);

        result.Should().Be("CPU Total");
    }

    [Fact]
    public void SelectCpuUsageSensorName_ShouldPreferCpuUsageAliasOverPerCoreEntries()
    {
        var result = SensorSelector.SelectCpuUsageSensorName(
        [
            "CPU Usage",
            "CPU Core #9"
        ]);

        result.Should().Be("CPU Usage");
    }

    [Fact]
    public void SelectGpuVramTemperatureSensorName_ShouldPreferMemorySpecificTemperatureNames()
    {
        var result = SensorSelector.SelectGpuVramTemperatureSensorName(
        [
            "GPU Hot Spot",
            "GPU Memory Junction",
            "Core"
        ]);

        result.Should().Be("GPU Memory Junction");
    }

    [Fact]
    public void SelectGpuVramTemperatureSensorName_ShouldRecognizeVramTemperatureAliases()
    {
        var result = SensorSelector.SelectGpuVramTemperatureSensorName(
        [
            "Core Temperature",
            "VRAM Temperature"
        ]);

        result.Should().Be("VRAM Temperature");
    }

    [Fact]
    public void SelectGpuVramTemperatureSensorName_ShouldRecognizeJunctionAliases()
    {
        var result = SensorSelector.SelectGpuVramTemperatureSensorName(
        [
            "GPU Temperature",
            "VRAM Junction"
        ]);

        result.Should().Be("VRAM Junction");
    }

    [Fact]
    public void SelectGpuHotSpotTemperatureSensorName_ShouldRecognizeGpuHotSpotAliases()
    {
        var result = SensorSelector.SelectGpuHotSpotTemperatureSensorName(
        [
            "GPU Temperature",
            "GPU Hot Spot",
            "GPU Memory Junction"
        ]);

        result.Should().Be("GPU Hot Spot");
    }

    [Fact]
    public void SelectGpuVramUsedSensorName_ShouldRecognizeDedicatedMemoryAliases()
    {
        var result = SensorSelector.SelectGpuVramUsedSensorName(
        [
            "Board Power Draw",
            "Dedicated Memory Used"
        ]);

        result.Should().Be("Dedicated Memory Used");
    }

    [Fact]
    public void SelectGpuVramUsedSensorName_ShouldPreferGpuMemoryUsedBeforeD3DDedicatedMemoryUsed()
    {
        var result = SensorSelector.SelectGpuVramUsedSensorName(
        [
            "D3D Dedicated Memory Used",
            "GPU Memory Used"
        ]);

        result.Should().Be("GPU Memory Used");
    }

    [Fact]
    public void SelectGpuVramTotalSensorName_ShouldRecognizeVramTotalAliases()
    {
        var result = SensorSelector.SelectGpuVramTotalSensorName(
        [
            "GPU Clock",
            "VRAM Total"
        ]);

        result.Should().Be("VRAM Total");
    }

    [Fact]
    public void SelectGpuVramUsedSensorName_ShouldRecognizeSharedMemoryAliases()
    {
        var result = SensorSelector.SelectGpuVramUsedSensorName(
        [
            "D3D Shared Memory Used",
            "GPU Clock"
        ]);

        result.Should().Be("D3D Shared Memory Used");
    }

    [Fact]
    public void SelectGpuVramTotalSensorName_ShouldRecognizeSharedMemoryTotalAliases()
    {
        var result = SensorSelector.SelectGpuVramTotalSensorName(
        [
            "D3D Shared Memory Total",
            "GPU Core"
        ]);

        result.Should().Be("D3D Shared Memory Total");
    }

    [Fact]
    public void SelectGpuVramFreeSensorName_ShouldRecognizeGpuMemoryFreeAliases()
    {
        var result = SensorSelector.SelectGpuVramFreeSensorName(
        [
            "GPU Core",
            "GPU Memory Free"
        ]);

        result.Should().Be("GPU Memory Free");
    }

    [Fact]
    public void SelectGpuPcieRxThroughputSensorName_ShouldRecognizeGpuPcieRxAlias()
    {
        var result = SensorSelector.SelectGpuPcieRxThroughputSensorName(
        [
            "GPU Memory",
            "GPU PCIe Rx"
        ]);

        result.Should().Be("GPU PCIe Rx");
    }

    [Fact]
    public void SelectGpuPcieRxThroughputSensorName_ShouldRecognizePcieReadAliases()
    {
        var result = SensorSelector.SelectGpuPcieRxThroughputSensorName(
        [
            "GPU Memory",
            "PCIe Read"
        ]);

        result.Should().Be("PCIe Read");
    }

    [Fact]
    public void SelectGpuPcieTxThroughputSensorName_ShouldRecognizeGpuPcieTxAlias()
    {
        var result = SensorSelector.SelectGpuPcieTxThroughputSensorName(
        [
            "GPU Core",
            "GPU PCIe Tx"
        ]);

        result.Should().Be("GPU PCIe Tx");
    }

    [Fact]
    public void SelectGpuPcieTxThroughputSensorName_ShouldRecognizePcieWriteAliases()
    {
        var result = SensorSelector.SelectGpuPcieTxThroughputSensorName(
        [
            "GPU Core",
            "PCIe Write"
        ]);

        result.Should().Be("PCIe Write");
    }

    [Fact]
    public void SelectGpuUsageSensorName_ShouldPreferD3D3DBeforeGenericGpuCoreLoad()
    {
        var result = SensorSelector.SelectGpuUsageSensorName(
        [
            "GPU Core",
            "D3D 3D"
        ]);

        result.Should().Be("D3D 3D");
    }

    [Fact]
    public void SelectGpuUsageSensorName_ShouldRecognizeGpuUtilizationAliases()
    {
        var result = SensorSelector.SelectGpuUsageSensorName(
        [
            "GPU Utilization",
            "GPU Memory Controller"
        ]);

        result.Should().Be("GPU Utilization");
    }

    [Fact]
    public void SelectGpuUsageSensorName_ShouldFallbackToUnnamedD3DLoadPrefix()
    {
        var result = SensorSelector.SelectGpuUsageSensorName(
        [
            "GPU Memory Controller",
            "D3D "
        ]);

        result.Should().Be("D3D ");
    }

    [Fact]
    public void SelectGpuPowerSensorName_ShouldPreferGpuPackageBeforeGenericPowerNames()
    {
        var result = SensorSelector.SelectGpuPowerSensorName(
        [
            "Board Power Draw",
            "GPU Package"
        ]);

        result.Should().Be("GPU Package");
    }

    [Fact]
    public void SelectGpuPowerSensorName_ShouldRecognizeGpuPowerAlias()
    {
        var result = SensorSelector.SelectGpuPowerSensorName(
        [
            "Power",
            "GPU Power"
        ]);

        result.Should().Be("GPU Power");
    }

    [Fact]
    public void SelectGpuPowerSensorName_ShouldRecognizeBoardAndChipPowerAliases()
    {
        var result = SensorSelector.SelectGpuPowerSensorName(
        [
            "Power",
            "GPU Chip Power",
            "GPU Board Power"
        ]);

        result.Should().Be("GPU Board Power");
    }

    [Fact]
    public void SelectGpuPowerSensorName_ShouldRecognizeAsicAndTotalGraphicsPowerAliases()
    {
        var result = SensorSelector.SelectGpuPowerSensorName(
        [
            "Power",
            "GPU ASIC Power",
            "Total Graphics Power"
        ]);

        result.Should().Be("Total Graphics Power");
    }

    [Fact]
    public void SelectGpuPowerSensorName_ShouldRecognizeBoardPowerAlias()
    {
        var result = SensorSelector.SelectGpuPowerSensorName(
        [
            "Power",
            "Board Power"
        ]);

        result.Should().Be("Board Power");
    }

    [Fact]
    public void SelectGpuPowerSensorName_ShouldRecognizeAmdPowerDrawAliases()
    {
        var result = SensorSelector.SelectGpuPowerSensorName(
        [
            "Power",
            "GPU PPT",
            "Average GPU Power"
        ]);

        result.Should().Be("GPU PPT");
    }

    [Fact]
    public void SelectGpuPowerSensorName_ShouldPreferSpecificPowerConsumptionBeforeGenericPower()
    {
        var consumptionResult = SensorSelector.SelectGpuPowerSensorName(
        [
            "Power",
            "GPU Power Consumption"
        ]);
        var coreResult = SensorSelector.SelectGpuPowerSensorName(
        [
            "Power",
            "GPU Core Power"
        ]);

        consumptionResult.Should().Be("GPU Power Consumption");
        coreResult.Should().Be("GPU Core Power");
    }

    [Fact]
    public void ResolveGpuPower_WhenLowPositivePowerExists_ShouldKeepCurrentValue()
    {
        var result = SensorsGroupController.ResolveGpuPower(4.5f, 22f);

        result.Should().Be(4.5f);
    }

    [Fact]
    public void ResolveGpuPower_WhenCurrentPowerDropsOut_ShouldKeepPreviousValue()
    {
        var result = SensorsGroupController.ResolveGpuPower(-1f, 22f);

        result.Should().Be(22f);
    }

    [Fact]
    public void ResolveGpuPower_WhenNoPowerExists_ShouldReturnUnavailable()
    {
        var result = SensorsGroupController.ResolveGpuPower(0f, -1f);

        result.Should().Be(-1f);
    }

    [Fact]
    public void SelectGpuTemperatureSensorName_ShouldPreferGpuCoreTemperatureAliases()
    {
        var result = SensorSelector.SelectGpuTemperatureSensorName(
        [
            "Temperature",
            "GPU Core"
        ]);

        result.Should().Be("GPU Core");
    }

    [Fact]
    public void SelectGpuCoreClockSensorName_ShouldPreferGpuCoreClockAliases()
    {
        var result = SensorSelector.SelectGpuCoreClockSensorName(
        [
            "Clock",
            "GPU Core"
        ]);

        result.Should().Be("GPU Core");
    }

    [Fact]
    public void SelectGpuCoreClockSensorName_ShouldRecognizeGraphicsAndShaderClockAliases()
    {
        var result = SensorSelector.SelectGpuCoreClockSensorName(
        [
            "Clock",
            "Shader Clock",
            "Graphics Clock"
        ]);

        result.Should().Be("Graphics Clock");
    }

    [Fact]
    public void SelectGpuMemoryClockSensorName_ShouldPreferGpuMemoryClockAliases()
    {
        var result = SensorSelector.SelectGpuMemoryClockSensorName(
        [
            "Clock",
            "GPU Memory"
        ]);

        result.Should().Be("GPU Memory");
    }

    [Fact]
    public void SelectGpuMemoryClockSensorName_ShouldRecognizeFramebufferClockAliases()
    {
        var result = SensorSelector.SelectGpuMemoryClockSensorName(
        [
            "Clock",
            "FB Clock"
        ]);

        result.Should().Be("FB Clock");
    }

    [Fact]
    public void SelectGpuVoltageSensorName_ShouldPreferGpuCoreVoltage()
    {
        var result = SensorSelector.SelectGpuVoltageSensorName(
        [
            "Voltage",
            "GPU Core Voltage"
        ]);

        result.Should().Be("GPU Core Voltage");
    }

    [Fact]
    public void SelectGpuVoltageSensorName_ShouldRecognizeGpuCoreAlias()
    {
        var result = SensorSelector.SelectGpuVoltageSensorName(
        [
            "Voltage",
            "GPU Core"
        ]);

        result.Should().Be("GPU Core");
    }

    [Fact]
    public void SelectGpuVoltageSensorName_ShouldRecognizeVddcAlias()
    {
        var result = SensorSelector.SelectGpuVoltageSensorName(
        [
            "Voltage",
            "VDDC"
        ]);

        result.Should().Be("VDDC");
    }

    [Fact]
    public void SelectGpuVoltageSensorName_ShouldPreferGpuVddcAliases()
    {
        var result = SensorSelector.SelectGpuVoltageSensorName(
        [
            "Voltage",
            "NVVDD",
            "GPU VDDC"
        ]);

        result.Should().Be("GPU VDDC");
    }

    [Fact]
    public void SelectGpuVoltageSensorName_ShouldRecognizeGpuVddAlias()
    {
        var result = SensorSelector.SelectGpuVoltageSensorName(
        [
            "Voltage",
            "GPU VDD"
        ]);

        result.Should().Be("GPU VDD");
    }

    [Fact]
    public void SelectMemoryUsedSensorName_ShouldRecognizeUsedMemoryAlias()
    {
        var result = SensorSelector.SelectMemoryUsedSensorName(
        [
            "Used Memory",
            "GPU Memory Used"
        ]);

        result.Should().Be("Used Memory");
    }

    [Fact]
    public void SelectMemoryAvailableSensorName_ShouldRecognizeFreeMemoryAlias()
    {
        var result = SensorSelector.SelectMemoryAvailableSensorName(
        [
            "GPU Memory Free",
            "Free Memory"
        ]);

        result.Should().Be("Free Memory");
    }

    [Fact]
    public void SelectMemoryAvailableSensorName_ShouldRecognizeMemoryFreeAlias()
    {
        var result = SensorSelector.SelectMemoryAvailableSensorName(
        [
            "GPU Memory Free",
            "Memory Free"
        ]);

        result.Should().Be("Memory Free");
    }

    [Fact]
    public void SelectMemoryLoadSensorName_ShouldPreferSystemMemoryLoadOverGpuMemoryLoad()
    {
        var result = SensorSelector.SelectMemoryLoadSensorName(
        [
            "GPU Memory",
            "Memory"
        ]);

        result.Should().Be("Memory");
    }

    [Fact]
    public void IsLikelyMemoryTemperatureSensorName_ShouldRecognizeDimmSpdAndTsodAliases()
    {
        string[] names =
        [
            "DIMM Thermal Sensor",
            "DDR5 SPD Hub Temperature",
            "TSOD Temperature",
            "PMIC Temperature",
            "Memory Module Temperature",
            "DIMM #1",
            "Memory Slot A1",
            "DDR Module Temperature"
        ];

        names.Should().OnlyContain(name => SensorSelector.IsLikelyMemoryTemperatureSensorName(name));
    }

    [Fact]
    public void SelectStorageTemperatureSensorName_ShouldPreferCompositeTemperature()
    {
        var result = SensorSelector.SelectStorageTemperatureSensorName(
        [
            "Temperature",
            "Composite"
        ]);

        result.Should().Be("Composite");
    }

    [Fact]
    public void SelectStorageTemperatureSensorName_ShouldRecognizeDriveTemperatureAlias()
    {
        var result = SensorSelector.SelectStorageTemperatureSensorName(
        [
            "Drive Temperature",
            "Temperature"
        ]);

        result.Should().Be("Drive Temperature");
    }

    [Fact]
    public void SelectStorageTemperatureSensorName_ShouldRecognizeNvmeAndControllerAliases()
    {
        var nvmeResult = SensorSelector.SelectStorageTemperatureSensorName(
        [
            "Temperature",
            "NVMe Composite"
        ]);
        var controllerResult = SensorSelector.SelectStorageTemperatureSensorName(
        [
            "Temperature",
            "Controller Temperature"
        ]);

        nvmeResult.Should().Be("NVMe Composite");
        controllerResult.Should().Be("Controller Temperature");
    }

    [Fact]
    public void SelectStorageTemperatureSensorName_ShouldRecognizeDriveCompositeAndControllerAliases()
    {
        var compositeResult = SensorSelector.SelectStorageTemperatureSensorName(
        [
            "Temperature",
            "Drive Composite Temperature"
        ]);
        var asicResult = SensorSelector.SelectStorageTemperatureSensorName(
        [
            "Temperature",
            "ASIC Controller Temperature"
        ]);
        var temperature1Result = SensorSelector.SelectStorageTemperatureSensorName(
        [
            "Temperature",
            "Temperature 1"
        ]);

        compositeResult.Should().Be("Drive Composite Temperature");
        asicResult.Should().Be("ASIC Controller Temperature");
        temperature1Result.Should().Be("Temperature 1");
    }

    [Fact]
    public void SelectMotherboardTemperatureSensorName_ShouldPreferPchAndChipsetAliases()
    {
        var pchResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "DIMM Temperature",
            "CPU Package",
            "GPU Core",
            "PCH Temperature"
        ]);
        var chipsetResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "Memory",
            "Chipset"
        ]);

        pchResult.Should().Be("PCH Temperature");
        chipsetResult.Should().Be("Chipset");
    }

    [Fact]
    public void SelectMotherboardTemperatureSensorName_ShouldRecognizeBoardControllerAliases()
    {
        var vrmResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "Memory",
            "VRM"
        ]);
        var tmpinResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "AUXTIN0",
            "CPU Package"
        ]);

        vrmResult.Should().Be("VRM");
        tmpinResult.Should().Be("AUXTIN0");
    }

    [Fact]
    public void SelectMotherboardTemperatureSensorName_ShouldRecognizeSuperIoAndExternalSensorAliases()
    {
        var superIoResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "CPU Package",
            "Super I/O"
        ]);
        var tSensorResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "GPU Core",
            "T_Sensor"
        ]);

        superIoResult.Should().Be("Super I/O");
        tSensorResult.Should().Be("T_Sensor");
    }

    [Fact]
    public void SelectMotherboardTemperatureSensorName_ShouldRecognizeEmbeddedControllerAndGenericBoardAliases()
    {
        var ecResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "CPU Package",
            "EC Temp"
        ]);
        var temp1Result = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "GPU Core",
            "Temperature #1"
        ]);
        var systemResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "DIMM Temperature",
            "System Temperature"
        ]);

        ecResult.Should().Be("EC Temp");
        temp1Result.Should().Be("Temperature #1");
        systemResult.Should().Be("System Temperature");
    }

    [Fact]
    public void SelectMotherboardTemperatureSensorName_ShouldRecognizeAcpiThermalZoneAliases()
    {
        var acpiResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "CPU Package",
            "ACPI Thermal Zone"
        ]);
        var zoneResult = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "GPU Core",
            "TZ00"
        ]);
        var temp2Result = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "DIMM Temperature",
            "Temperature #2"
        ]);

        acpiResult.Should().Be("ACPI Thermal Zone");
        zoneResult.Should().Be("TZ00");
        temp2Result.Should().Be("Temperature #2");
    }

    [Fact]
    public void SelectMotherboardTemperatureSensorName_ShouldIgnoreMemoryCpuAndGpuSensors()
    {
        var result = SensorSelector.SelectMotherboardTemperatureSensorName(
        [
            "DIMM",
            "CPU Package",
            "GPU Core"
        ]);

        result.Should().BeNull();
    }

    [Fact]
    public void IsBoardTemperatureHardware_ShouldIncludeSuperIoControllersWithBoardTemperatureSensors()
    {
        var sensor = CreateSensor("SYSTIN", SensorType.Temperature);
        var superIo = CreateHardwareWithSensors(
            "Nuvoton NCT6798D",
            Enum.Parse<HardwareType>("SuperIO"),
            sensor.Object);

        var result = SensorSelector.IsBoardTemperatureHardware(superIo.Object);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsBoardTemperatureHardware_ShouldExcludeDedicatedMetricHardware()
    {
        var sensor = CreateSensor("PCH Temperature", SensorType.Temperature);
        var cpu = CreateHardwareWithSensors("Intel Core", HardwareType.Cpu, sensor.Object);
        var storage = CreateHardwareWithSensors("NVMe SSD", HardwareType.Storage, sensor.Object);

        SensorSelector.IsBoardTemperatureHardware(cpu.Object).Should().BeFalse();
        SensorSelector.IsBoardTemperatureHardware(storage.Object).Should().BeFalse();
    }

    [Fact]
    public void SelectMemoryHardwareName_ShouldPreferTotalMemory()
    {
        var result = SensorSelector.SelectMemoryHardwareName(
        [
            "Physical Memory",
            "Total Memory"
        ]);

        result.Should().Be("Total Memory");
    }

    [Fact]
    public void SelectMemoryHardwareName_ShouldFallbackToMemoryNamedHardware()
    {
        var result = SensorSelector.SelectMemoryHardwareName(
        [
            "Memory Device",
            "Physical"
        ]);

        result.Should().Be("Memory Device");
    }

    [Fact]
    public void ToGigabytes_WhenSensorIsSmallData_ShouldConvertMegabytesToGigabytes()
    {
        var smallData = CreateDataSensor("GPU Memory Used", SensorType.SmallData, 8192f);
        var data = CreateDataSensor("GPU Memory Used", SensorType.Data, 8f);

        HardwareDiscoveryService.ToGigabytes(smallData.Object).Should().BeApproximately(8f, 0.0001f);
        HardwareDiscoveryService.ToGigabytes(data.Object).Should().BeApproximately(8f, 0.0001f);
        HardwareDiscoveryService.ToGigabytes(null).Should().BeNull();
    }

    [Fact]
    public void ResolveGpuVramMetrics_WhenUsedIsMissing_ShouldDeriveUsedAndUtilizationFromTotalAndFree()
    {
        var result = SensorsGroupController.ResolveGpuVramMetrics(-1f, 8192f, 7168f);

        result.used.Should().Be(1024f);
        result.total.Should().Be(8192f);
        result.utilization.Should().BeApproximately(12.5f, 0.01f);
    }

    [Fact]
    public void ResolveGpuVramMetrics_WhenTotalIsMissing_ShouldDeriveTotalFromUsedAndFree()
    {
        var result = SensorsGroupController.ResolveGpuVramMetrics(232f, -1f, 7956f);

        result.used.Should().Be(232f);
        result.total.Should().Be(8188f);
        result.utilization.Should().BeApproximately((232f / 8188f) * 100f, 0.01f);
    }

    [Fact]
    public void IsLikelyCpuComponentPowerSensorName_ShouldRecognizeCpuComponentPowerAliases()
    {
        SensorSelector.IsLikelyCpuComponentPowerSensorName("CPU Cores").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("CPU Core").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("IA Cores").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("VDDCR CPU Power").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("CPU Memory").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("DRAM").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("CPU Platform").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("CPU Graphics").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("GT Cores").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("GT Power").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("CPU SoC").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("SoC Power").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("VDDCR SoC Power").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("System Agent").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("PCH").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("Uncore Power").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("EDC").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("TDC").Should().BeTrue();
        SensorSelector.IsLikelyCpuComponentPowerSensorName("GPU Package").Should().BeFalse();
    }

    [Fact]
    public void ResolveCpuPower_WhenPackagePowerMissing_ShouldSumValidComponentPowers()
    {
        var result = SensorsGroupController.ResolveCpuPower(-1f, [24f, 3.5f, 7f]);

        result.Should().BeApproximately(34.5f, 0.001f);
    }

    [Fact]
    public void ResolveCpuPower_WhenPackagePowerExists_ShouldPreferPackagePower()
    {
        var result = SensorsGroupController.ResolveCpuPower(58f, [24f, 3.5f, 7f]);

        result.Should().Be(58f);
    }

    [Fact]
    public void ResolveCpuComponentPowers_ShouldMapCoresMemoryAndPlatform()
    {
        var result = SensorsGroupController.ResolveCpuComponentPowers(
        [
            ("CPU Cores", 24f),
            ("CPU Memory", 3.5f),
            ("CPU Platform", 7f)
        ]);

        result.cores.Should().Be(24f);
        result.memory.Should().Be(3.5f);
        result.platform.Should().Be(7f);
    }

    [Fact]
    public void ResolveCpuComponentPowers_ShouldMapAmdCoreSocAndMemoryAliases()
    {
        var result = SensorsGroupController.ResolveCpuComponentPowers(
        [
            ("CPU Core", 24f),
            ("CPU SoC", 6f),
            ("DRAM", 3f)
        ]);

        result.cores.Should().Be(24f);
        result.memory.Should().Be(3f);
        result.platform.Should().Be(6f);
    }

    [Fact]
    public void ResolveCpuComponentPowers_ShouldMapAmdSviPowerAliases()
    {
        var result = SensorsGroupController.ResolveCpuComponentPowers(
        [
            ("VDDCR CPU Power", 22f),
            ("CPU Core Power (SVI3 TFN)", 4f),
            ("VDDCR SoC Power", 5f)
        ]);

        result.cores.Should().Be(26f);
        result.platform.Should().Be(5f);
    }

    [Fact]
    public void ResolveCpuComponentPowers_ShouldMapIntelRaplAliases()
    {
        var result = SensorsGroupController.ResolveCpuComponentPowers(
        [
            ("IA Cores", 28f),
            ("CPU Graphics", 4f),
            ("DRAM", 2f)
        ]);

        result.cores.Should().Be(28f);
        result.memory.Should().Be(2f);
        result.platform.Should().Be(4f);
    }

    [Fact]
    public void ResolveCpuComponentPowers_ShouldMapIntelGtCoresAsPlatformPower()
    {
        var result = SensorsGroupController.ResolveCpuComponentPowers(
        [
            ("IA Cores", 24f),
            ("GT Cores", 3.5f)
        ]);

        result.cores.Should().Be(24f);
        result.platform.Should().Be(3.5f);
    }

    [Fact]
    public void ResolveCpuComponentPowers_ShouldMapPchAndSystemAgentAsPlatformPower()
    {
        var result = SensorsGroupController.ResolveCpuComponentPowers(
        [
            ("IA Cores", 24f),
            ("System Agent", 1.5f),
            ("PCH", 2.5f)
        ]);

        result.cores.Should().Be(24f);
        result.platform.Should().Be(4f);
    }

    [Fact]
    public void ResolveCpuComponentPowers_ShouldMapAdditionalPlatformPowerAliases()
    {
        var result = SensorsGroupController.ResolveCpuComponentPowers(
        [
            ("IA Power", 18f),
            ("GT Power", 2f),
            ("Uncore Power", 1.5f),
            ("EDC", 0.5f),
            ("TDC", 0.25f)
        ]);

        result.cores.Should().Be(18f);
        result.platform.Should().BeApproximately(4.25f, 0.001f);
    }

    private static Mock<IHardware> CreateHardware(string name, HardwareType hardwareType, params IHardware[] subHardware)
    {
        var hardware = new Mock<IHardware>();
        hardware.SetupGet(h => h.Name).Returns(name);
        hardware.SetupGet(h => h.HardwareType).Returns(hardwareType);
        hardware.SetupGet(h => h.SubHardware).Returns(subHardware);
        hardware.SetupGet(h => h.Sensors).Returns([]);
        return hardware;
    }

    private static Mock<IHardware> CreateHardwareWithSensors(string name, HardwareType hardwareType, params ISensor[] sensors)
    {
        var hardware = new Mock<IHardware>();
        hardware.SetupGet(h => h.Name).Returns(name);
        hardware.SetupGet(h => h.HardwareType).Returns(hardwareType);
        hardware.SetupGet(h => h.SubHardware).Returns([]);
        hardware.SetupGet(h => h.Sensors).Returns(sensors);
        return hardware;
    }

    private static Mock<ISensor> CreateSensor(string name, SensorType sensorType)
    {
        var sensor = new Mock<ISensor>();
        sensor.SetupGet(s => s.Name).Returns(name);
        sensor.SetupGet(s => s.SensorType).Returns(sensorType);
        return sensor;
    }

    private static Mock<ISensor> CreateDataSensor(string name, SensorType sensorType, float value)
    {
        var sensor = CreateSensor(name, sensorType);
        sensor.SetupGet(s => s.Value).Returns(value);
        return sensor;
    }
}
