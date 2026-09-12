using System;
using System.ComponentModel.DataAnnotations;
using UniversalDeviceToolkit.Lib.Resources;

namespace UniversalDeviceToolkit.Lib;

/// <summary>Represents the automatic update check frequency.</summary>
public enum UpdateCheckFrequency
{
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerHour")]
    PerHour,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerThreeHours")]
    PerThreeHours,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerTwelveHours")]
    PerTwelveHours,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerDay")]
    PerDay,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerWeek")]
    PerWeek,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerMonth")]
    PerMonth
}

/// <summary>Represents the result status of an update check operation.</summary>
public enum UpdateCheckStatus
{
    Success,
    RateLimitReached,
    Error
}
