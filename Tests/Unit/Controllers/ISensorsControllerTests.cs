using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using FluentAssertions;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Controllers.Sensors;
using UniversalDeviceToolkit.Lib.Utils;
using Moq;
using Xunit;
using UniversalDeviceToolkit.Abstractions.Utils;

namespace UniversalDeviceToolkit.Tests.Controllers;

[Trait("Category", TestCategories.Unit)]
public class ISensorsControllerTests : UnitTestBase
{
    [Fact]
    public void ISensorsController_ShouldHaveCorrectMethods()
    {
        var methodNames = new[]
        {
            nameof(ISensorsController.IsSupportedAsync),
            nameof(ISensorsController.PrepareAsync),
            nameof(ISensorsController.GetDataAsync),
            nameof(ISensorsController.GetFanSpeedsAsync)
        };

        foreach (var methodName in methodNames)
        {
            typeof(ISensorsController).GetMethod(methodName).Should().NotBeNull();
        }

        typeof(IDisposable).IsAssignableFrom(typeof(ISensorsController)).Should().BeTrue();
    }

    [Fact]
    public async Task ISensorsController_GetDataAsync_ShouldHaveDefaultParameter()
    {
        var method = typeof(ISensorsController).GetMethod("GetDataAsync");
        var parameters = method!.GetParameters();

        parameters.Should().HaveCount(1);
        parameters[0].HasDefaultValue.Should().BeTrue();
        parameters[0].DefaultValue.Should().Be(false);
    }
}
