using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using UniversalDeviceToolkit.Lib;
using UniversalDeviceToolkit.Lib.Controllers;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace UniversalDeviceToolkit.Lib.Controllers;

public class SpectrumScreenCapture : SpectrumKeyboardBacklightController.ISpectrumScreenCapture
{
    private const PixelFormat PIXEL_FORMAT = PixelFormat.Format32bppRgb;

    public void CaptureScreen(ref RGBColor[,] buffer, int width, int height, CancellationToken token)
    {
        // Primary screen bounds without System.Windows.Forms.Screen: the
        // primary monitor starts at (0,0) by definition.
        var screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

        using var targetImage = new Bitmap(width, height, PIXEL_FORMAT);

        using (var image = new Bitmap(screenWidth, screenHeight, PIXEL_FORMAT))
        {
            using (var graphics = Graphics.FromImage(image))
                graphics.CopyFromScreen(0, 0, 0, 0, new Size(screenWidth, screenHeight));

            token.ThrowIfCancellationRequested();

            using var targetGraphics = Graphics.FromImage(targetImage);
            targetGraphics.InterpolationMode = InterpolationMode.Bicubic;
            targetGraphics.DrawImage(image, new Rectangle(0, 0, width, height));
        }

        token.ThrowIfCancellationRequested();

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var pixel = targetImage.GetPixel(x, y);
                buffer[x, y] = new RGBColor(pixel.R, pixel.G, pixel.B);

                token.ThrowIfCancellationRequested();
            }
        }
    }
}
