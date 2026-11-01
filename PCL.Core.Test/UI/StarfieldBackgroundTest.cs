using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test.UI;

[TestClass]
public class StarfieldBackgroundTest
{
    [TestMethod]
    public void RendersBothThemesAndAnimatedFramesWithoutInterceptingInput() => OnStaThread(() =>
    {
        var sky = new global::PCL.StarfieldBackground();
        Assert.IsFalse(sky.IsHitTestVisible);
        Assert.IsFalse(sky.Focusable);
        var dark = Render(sky, 640, 360);
        Field("_sceneTime").SetValue(sky, 4d);
        var animated = Render(sky, 640, 360);
        Assert.IsFalse(dark.SequenceEqual(animated), "Stardust should move between frames.");

        sky.IsDarkMode = false;
        var light = Render(sky, 640, 360);
        Assert.IsFalse(animated.SequenceEqual(light), "Theme changes must rebuild the sky.");
        Assert.IsTrue(light[0] > dark[0], "The light sky should remain light behind the interface.");
        Assert.IsTrue(light.Where((_, i) => i % 4 == 3).All(alpha => alpha == 255));
        Assert.AreEqual(320 * 180 * 4, Render(sky, 320, 180).Length);
    });

    [TestMethod]
    public void FrameClockIsLimitedAndStopsWhenMotionIsDisabled() => OnStaThread(() =>
    {
        var sky = new global::PCL.StarfieldBackground();
        _ = Render(sky, 320, 180);
        Field("_isRendering").SetValue(sky, true);
        void Frame(double milliseconds) => sky.AdvanceFrame(TimeSpan.FromMilliseconds(milliseconds));
        double Time() => (double)Field("_sceneTime").GetValue(sky)!;

        Frame(0);
        Frame(16);
        Assert.AreEqual(0d, Time(), "Rendering should be capped at roughly 30 frames per second.");
        Frame(34);
        var advanced = Time();
        Assert.IsTrue(advanced > 0);
        Assert.IsTrue(sky.IsArrangeValid, "Particle frames must not invalidate the surrounding interface layout.");
        Frame(40);
        Assert.AreEqual(advanced, Time());
        Frame(10000);
        Assert.IsTrue(Time() - advanced <= 0.100001, "A delayed frame must not cause a large visual jump.");

        sky.MotionEnabled = false;
        var paused = Time();
        Frame(20000);
        Assert.AreEqual(paused, Time());
        Assert.IsFalse((bool)Field("_isRendering").GetValue(sky)!);
        sky.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.IsFalse((bool)Field("_isRendering").GetValue(sky)!);
    });

    [TestMethod]
    public void MeteorsHaveQuietGapsAndDisappearWhenMotionIsDisabled() => OnStaThread(() =>
    {
        if (!SystemParameters.ClientAreaAnimation) Assert.Inconclusive("Windows has disabled animation.");
        var sky = new global::PCL.StarfieldBackground();
        byte[] MeteorFrame(double seconds)
        {
            Field("_sceneTime").SetValue(sky, seconds);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) sky.DrawMeteors(dc, 640, 360);
            var bitmap = new RenderTargetBitmap(640, 360, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var pixels = new byte[640 * 360 * 4];
            bitmap.CopyPixels(pixels, 640 * 4, 0);
            return pixels;
        }

        foreach (var quietTime in new[] { 0d, 3d, 6d, 12d, 20d, 35d, 50d })
            Assert.IsTrue(MeteorFrame(quietTime).All(value => value == 0), "The sky needs quiet gaps between meteors.");
        var first = MeteorFrame(4.5);
        Assert.IsTrue(first.Any(value => value > 100), "A meteor should have a visible bright head and trail.");
        Assert.IsFalse(first.SequenceEqual(MeteorFrame(4.8)), "The meteor should travel across the sky.");
        foreach (var passTime in new[] { 15.5, 29.5, 41.5, 60.5 })
            Assert.IsTrue(MeteorFrame(passTime).Any(value => value > 100));
        sky.IsDarkMode = false;
        Assert.IsTrue(MeteorFrame(4.5).Any(value => value > 0), "Light mode should also show meteors.");
        sky.MotionEnabled = false;
        Assert.IsTrue(MeteorFrame(4.5).All(value => value == 0), "Reduced motion should leave a static sky.");
    });

    private static FieldInfo Field(string name) =>
        typeof(global::PCL.StarfieldBackground).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static byte[] Render(global::PCL.StarfieldBackground sky, int width, int height)
    {
        sky.InvalidateVisual();
        sky.Measure(new Size(width, height));
        sky.Arrange(new Rect(0, 0, width, height));
        sky.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(sky);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    private static void OnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "Offscreen rendering timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
