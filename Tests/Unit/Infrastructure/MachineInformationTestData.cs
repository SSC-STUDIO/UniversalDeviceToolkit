using UniversalDeviceToolkit.Lib;

namespace UniversalDeviceToolkit.Tests.Infrastructure;

public static class MachineInformationTestData
{
    public static MachineInformation Create(
        string vendor,
        string model,
        string machineType = "0000",
        HardwareInventory? hardware = null) => new()
    {
        Vendor = vendor,
        MachineType = machineType,
        Model = model,
        SerialNumber = "TEST",
        SupportedPowerModes = [],
        Features = MachineInformation.FeatureData.Unknown,
        Properties = new MachineInformation.PropertyData(),
        Hardware = hardware ?? HardwareInventory.Empty
    };

    public static MachineInformation WithComputerSystem(
        string vendor,
        string model,
        string computerSystemManufacturer,
        string computerSystemModel,
        string systemFamily = "",
        string machineType = "0000") =>
        Create(vendor, model, machineType, new HardwareInventory
        {
            ComputerSystem = new()
            {
                Manufacturer = computerSystemManufacturer,
                Model = computerSystemModel,
                SystemFamily = systemFamily
            }
        });

    public static MachineInformation WithBaseBoard(
        string vendor,
        string model,
        string baseBoardManufacturer,
        string baseBoardProduct,
        string machineType = "0000") =>
        Create(vendor, model, machineType, new HardwareInventory
        {
            BaseBoard = new()
            {
                Manufacturer = baseBoardManufacturer,
                Product = baseBoardProduct
            }
        });

    public static MachineInformation WithChassis(
        string vendor,
        string model,
        string chassisManufacturer,
        ushort[] chassisTypes,
        string machineType = "0000") =>
        Create(vendor, model, machineType, new HardwareInventory
        {
            Chassis = new()
            {
                Manufacturer = chassisManufacturer,
                ChassisTypes = chassisTypes
            }
        });
}
