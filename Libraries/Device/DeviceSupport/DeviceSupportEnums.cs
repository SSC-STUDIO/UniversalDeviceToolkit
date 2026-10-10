using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

/// <summary>Represents keyboard shortcut driver key flags.</summary>
[Flags]
public enum DriverKey
{
    FnF10 = 32,
    FnF4 = 256,
    FnF8 = 8192,
    FnSpace = 4096,
}

/// <summary>Identifies the product series of the Lenovo/Legion device.</summary>
public enum LegionSeries
{
    Legion_5 = 0,
    Legion_Pro_5 = 1,
    Legion_Slim_5 = 2,
    Legion_7 = 3,
    Legion_Pro_7 = 4,
    Legion_9 = 5,
    Legion_Go = 6,
    Lenovo_Slim = 7,
    Legion_Legacy = 8,
    IdeaPad = 9,
    IdeaPad_Gaming = 10,
    LOQ = 11,
    YOGA = 12,
    ThinkBook = 13,
    Unknown = 255
}

/// <summary>Identifies the Windows operating system version.</summary>
public enum OS
{
    [Display(Name = "Windows 11")]
    Windows11,
    [Display(Name = "Windows 10")]
    Windows10,
    [Display(Name = "Windows 8")]
    Windows8,
    [Display(Name = "Windows 7")]
    Windows7
}
