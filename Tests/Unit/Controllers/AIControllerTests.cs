using System;
using System.Runtime.CompilerServices;
using FluentAssertions;
using UniversalDeviceToolkit.Lib.AutoListeners;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Features;
using UniversalDeviceToolkit.Lib.Listeners;
using UniversalDeviceToolkit.Lib.Settings;
using Xunit;

namespace UniversalDeviceToolkit.Tests.Controllers;


[Trait("Category", TestCategories.Unit)]
public class AIControllerTests : UnitTestBase
{
    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        var controller = CreateController();

        controller.Should().NotBeNull();
    }

    [Fact]
    public void IsAIModeEnabled_GetSet_ShouldWorkCorrectly()
    {
        var settings = new BalanceModeSettings();
        var controller = CreateController(settings);

        controller.IsAIModeEnabled = true;
        controller.IsAIModeEnabled.Should().BeTrue();

        controller.IsAIModeEnabled = false;
        controller.IsAIModeEnabled.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var controller = CreateController();

        var act = () => controller.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        var controller = CreateController();

        var act = () =>
        {
            controller.Dispose();
            controller.Dispose();
            controller.Dispose();
        };

        act.Should().NotThrow();
    }

    private AIController CreateController()
    {
        return CreateController(new BalanceModeSettings());
    }

    private static AIController CreateController(BalanceModeSettings settings)
    {
        return new AIController(
            CreateUninitialized<PowerModeListener>(),
            CreateUninitialized<PowerStateListener>(),
            CreateUninitialized<GameAutoListener>(),
            CreateUninitialized<PowerModeFeature>(),
            settings);
    }

    private static T CreateUninitialized<T>() where T : class =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
