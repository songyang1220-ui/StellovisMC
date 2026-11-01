using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test.UI;

[TestClass]
public class StartupAnimationTest
{
    private static global::PCL.StartupAnimation Create()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Plain Craft Launcher 2", "Images", "icon.png")))
            repository = repository.Parent;
        Assert.IsNotNull(repository);
        return new global::PCL.StartupAnimation
        {
            Logo = new BitmapImage(new Uri(Path.Combine(repository.FullName, "Plain Craft Launcher 2", "Images", "icon.png")))
        };
    }

    private static void Start(global::PCL.StartupAnimation intro, bool motion) => intro.Start(motion);
    private static void Frame(global::PCL.StartupAnimation intro, double seconds) => intro.AdvanceFrame(TimeSpan.FromSeconds(seconds));
    private static bool Running(global::PCL.StartupAnimation intro) => intro.IsRunning;

    [TestMethod]
    public void StarsGatherAndBrandRevealsWithoutInvalidatingLayout() => OnStaThread(() =>
    {
        var intro = Create();
        Assert.IsFalse(intro.IsHitTestVisible);
        Assert.IsFalse(intro.Focusable);
        Start(intro, true);
        Assert.IsTrue(intro.IsHitTestVisible, "The introduction must cover clicks on controls that are still hidden.");
        var initial = Render(intro, 960, 600, "01-gather-start");
        Frame(intro, 0);
        Frame(intro, 0.55);
        Assert.IsTrue(intro.IsArrangeValid, "Animation frames must not trigger surrounding layout passes.");
        var revealed = Render(intro, 960, 600, "02-brand-reveal");
        Assert.IsFalse(initial.SequenceEqual(revealed));
        Assert.IsTrue(Running(intro));
        Frame(intro, 0.98);
        Assert.IsTrue(intro.Opacity is > 0 and < 1, "The introduction must fade into the ready homepage.");
        Render(intro, 960, 600, "03-fade-home");
        Frame(intro, 1.11);
        Assert.IsFalse(Running(intro));
        Assert.AreEqual(Visibility.Collapsed, intro.Visibility);
        Assert.IsFalse(intro.IsHitTestVisible);
        Frame(intro, 2);
        Assert.AreEqual(Visibility.Collapsed, intro.Visibility);
    });

    [TestMethod]
    public void DisabledMotionSkipsIntroductionAndUnloadingStopsClock() => OnStaThread(() =>
    {
        var intro = Create();
        Start(intro, false);
        Assert.IsFalse(Running(intro));
        Assert.AreEqual(Visibility.Collapsed, intro.Visibility);
        Start(intro, true);
        Assert.IsTrue(Running(intro));
        intro.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.IsFalse(Running(intro));
        Assert.AreEqual(Visibility.Collapsed, intro.Visibility);
        Start(intro, true);
        Frame(intro, 5);
        Frame(intro, 6.11);
        Assert.IsFalse(Running(intro), "Restart must use its own animation clock.");
    });

    [TestMethod]
    public void ArtworkRemainsCenteredAfterResizingAndRendersBothThemes() => OnStaThread(() =>
    {
        var intro = Create();
        Start(intro, true);
        Frame(intro, 0);
        Frame(intro, 0.7);
        var dark = Render(intro, 1100, 700, "04-dark-full");
        // WPF's intermediate gradient compositing can round an opaque pixel down by one.
        Assert.IsTrue(dark.Where((_, i) => i % 4 == 3).All(alpha => alpha >= 254),
            $"Opaque backdrop expected: first={dark[3]}, last={dark[^1]}, minimum={dark.Where((_, i) => i % 4 == 3).Min()}");
        var darkCenter = dark.Skip((320 * 1100 + 550) * 4).Take(3).ToArray();
        intro.IsDarkMode = false;
        intro.InvalidateVisual();
        var light = Render(intro, 960, 600, "05-light-resized");
        Assert.IsTrue(light[0] > dark[0]);
        Assert.AreEqual(960 * 600 * 4, light.Length);
        var lightCenter = light.Skip((270 * 960 + 480) * 4).Take(3).ToArray();
        for (var i = 0; i < 3; i++)
            Assert.IsTrue(Math.Abs(darkCenter[i] - lightCenter[i]) <= 2, "Resizing must keep the icon in the center.");
        intro.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    });

    private static byte[] Render(FrameworkElement intro, int width, int height, string previewName)
    {
        intro.Measure(new Size(width, height));
        intro.Arrange(new Rect(0, 0, width, height));
        intro.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(intro);
        var previewDirectory = Environment.GetEnvironmentVariable("STELLOVIS_STARTUP_PREVIEW_DIR");
        if (!string.IsNullOrEmpty(previewDirectory))
        {
            Directory.CreateDirectory(previewDirectory);
            using var stream = File.Create(Path.Combine(previewDirectory, previewName + ".png"));
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }
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
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "Startup rendering timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
