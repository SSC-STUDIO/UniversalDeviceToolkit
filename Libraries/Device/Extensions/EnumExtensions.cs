using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace UniversalDeviceToolkit.Lib.Extensions;

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum enumValue)
    {
        var displayAttribute = enumValue.GetType()
            .GetMember(enumValue.ToString())
            .First()
            .GetCustomAttributes(false)
            .OfType<DisplayAttribute>()
            .FirstOrDefault();

        if (displayAttribute?.Name is null)
            return enumValue.ToString();

        if (displayAttribute.ResourceType?.GetProperty(displayAttribute.Name, BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is string str)
            return str;

        if (TryGetResourceManagerString(displayAttribute.ResourceType, displayAttribute.Name) is { } localizedName)
            return localizedName;

        return displayAttribute.Name;
    }

    private static string? TryGetResourceManagerString(Type? resourceType, string name)
    {
        try
        {
            if (resourceType?.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null) is not ResourceManager resourceManager)
                return null;

            var culture = resourceType.GetProperty("Culture", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null) as CultureInfo;
            return resourceManager.GetString(name, culture);
        }
        catch
        {
            return null;
        }
    }

    public static string GetFlagsDisplayName(this Enum enumValue, Enum? excluding = null)
    {
        var values = Enum.GetValues(enumValue.GetType()).Cast<Enum>();
        if (excluding is not null)
            values = values.Where(v => !v.Equals(excluding));
        var names = values.Where(enumValue.HasFlag).Select(GetDisplayName);
        return string.Join(", ", names);
    }
}
