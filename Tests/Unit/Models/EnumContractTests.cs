using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Models;

/// <summary>
/// Compact contract checks for Lib enums. Replaces hundreds of per-member
/// IsDefined/HaveCount padding tests that added noise without catching regressions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EnumContractTests
{
    [Fact]
    public void LibPublicEnums_ShouldDeclareMembersWithStableNames()
    {
        var enums = GetLibPublicEnums().ToList();
        enums.Should().NotBeEmpty("Lib should export public enums");

        foreach (var enumType in enums)
        {
            var names = Enum.GetNames(enumType);
            names.Should().NotBeEmpty(because: $"{enumType.FullName} should declare at least one member");
            names.Should().OnlyHaveUniqueItems(because: $"{enumType.FullName} member names");
            names.Should().NotContain(string.Empty);
        }
    }

    [Fact]
    public void LibPublicEnumDisplayAttributes_WhenPresent_ShouldHaveNonEmptyNames()
    {
        foreach (var enumType in GetLibPublicEnums())
        {
            foreach (var name in Enum.GetNames(enumType))
            {
                var member = enumType.GetMember(name).FirstOrDefault();
                if (member is null)
                    continue;

                var attr = member.GetCustomAttributes(typeof(DisplayAttribute), false)
                    .Cast<DisplayAttribute>()
                    .FirstOrDefault();
                if (attr is null)
                    continue;

                attr.Name.Should().NotBeNullOrWhiteSpace(
                    because: $"{enumType.Name}.{name} DisplayAttribute.Name should not be empty");
            }
        }
    }

    [Fact]
    public void NativeWindowsMessage_ShouldExposeLidAndMonitorSignals()
    {
        var values = Enum.GetValues<NativeWindowsMessage>();
        values.Should().Contain(NativeWindowsMessage.LidOpened);
        values.Should().Contain(NativeWindowsMessage.LidClosed);
        values.Should().Contain(NativeWindowsMessage.MonitorOn);
    }

    [Theory]
    [InlineData(typeof(AutorunState), nameof(AutorunState.Enabled))]
    [InlineData(typeof(BatteryState), nameof(BatteryState.Conservation))]
    [InlineData(typeof(HDRState), nameof(HDRState.Off))]
    [InlineData(typeof(FnLockState), nameof(FnLockState.On))]
    [InlineData(typeof(Theme), nameof(Theme.Dark))]
    [InlineData(typeof(WindowsPowerMode), nameof(WindowsPowerMode.BestPerformance))]
    public void SampleUserFacingEnums_ShouldHaveDisplayAttributes(Type enumType, string memberName)
    {
        var member = enumType.GetMember(memberName).First();
        var attr = member.GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .FirstOrDefault();

        attr.Should().NotBeNull($"{enumType.Name}.{memberName} should have a Display attribute");
        attr!.Name.Should().NotBeNullOrWhiteSpace();
    }

    private static IEnumerable<Type> GetLibPublicEnums()
    {
        var libAssembly = typeof(BatteryState).Assembly;
        return libAssembly.GetExportedTypes()
            .Where(t => t.IsEnum)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
    }
}
