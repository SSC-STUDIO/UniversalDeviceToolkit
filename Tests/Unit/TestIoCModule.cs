using Autofac;
using UniversalDeviceToolkit.Lib.Controllers;
using UniversalDeviceToolkit.Lib.Utils;
using System.Threading;

namespace UniversalDeviceToolkit.Tests;

public class TestIoCModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TestMainThreadDispatcher>().As<IMainThreadDispatcher>().SingleInstance();
        
        // Register a mock implementation of ISpectrumScreenCapture for testing
        builder.RegisterType<TestSpectrumScreenCapture>().As<SpectrumKeyboardBacklightController.ISpectrumScreenCapture>().SingleInstance();
    }
}

internal class TestSpectrumScreenCapture : SpectrumKeyboardBacklightController.ISpectrumScreenCapture
{
    public void CaptureScreen(ref UniversalDeviceToolkit.Lib.RGBColor[,] buffer, int width, int height, CancellationToken token)
    {
        // Mock implementation - do nothing for testing
    }
}
