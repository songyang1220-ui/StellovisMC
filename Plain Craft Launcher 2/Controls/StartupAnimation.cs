using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PCL;

/// <summary>A brief, decorative introduction over the already prepared main window.</summary>
public sealed class StartupAnimation : FrameworkElement
{
    private readonly DrawingVisual _artwork = new();
    private DrawingGroup? _sky;
    private FormattedText? _title;
    private FormattedText? _subtitle;
    private TimeSpan? _firstFrame;
    private TimeSpan? _lastFrame;
    private double _elapsed;
    private static readonly Brush StarBrush = Brush("#D3E8FF");
    private static readonly Brush LightStarBrush = Brush("#6B88B8");
    private static readonly Brush BlueGlow = Cloud("#759DFF", 0.27);
    private static readonly Brush VioletGlow = Cloud("#9979E2", 0.15);

    public static readonly DependencyProperty LogoProperty = DependencyProperty.Register(
        nameof(Logo), typeof(ImageSource), typeof(StartupAnimation),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsDarkModeProperty = DependencyProperty.Register(
        nameof(IsDarkMode), typeof(bool), typeof(StartupAnimation),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) =>
        {
            var intro = (StartupAnimation)d;
            intro._sky = null;
            intro._title = null;
            intro._subtitle = null;
        }));

    public ImageSource? Logo
    {
        get => (ImageSource?)GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }
    public bool IsDarkMode
    {
        get => (bool)GetValue(IsDarkModeProperty);
        set => SetValue(IsDarkModeProperty, value);
    }
    public bool IsRunning { get; private set; }

    public StartupAnimation()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
        Visibility = Visibility.Collapsed;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        AddVisualChild(_artwork);
        Unloaded += (_, _) => Stop();
    }

    public void Start(bool motionEnabled)
    {
        Stop();
        if (!motionEnabled) return;
        _firstFrame = _lastFrame = null;
        _elapsed = 0;
        Opacity = 1;
        IsHitTestVisible = true;
        Visibility = Visibility.Visible;
        IsRunning = true;
        CompositionTarget.Rendering += OnRendering;
        InvalidateVisual();
    }

    private void Stop()
    {
        CompositionTarget.Rendering -= OnRendering;
        IsRunning = false;
        IsHitTestVisible = false;
        _firstFrame = _lastFrame = null;
        Visibility = Visibility.Collapsed;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation) { Stop(); return; }
        if (e is RenderingEventArgs frame) AdvanceFrame(frame.RenderingTime);
    }

    internal void AdvanceFrame(TimeSpan frameTime)
    {
        if (!IsRunning) return;
        _firstFrame ??= frameTime;
        if (_lastFrame is { } last && (frameTime - last).TotalSeconds + 0.0005 < 1d / 30d) return;
        _lastFrame = frameTime;
        _elapsed = Math.Max(0, (frameTime - _firstFrame.Value).TotalSeconds);
        if (_elapsed >= 1.1) { Stop(); return; }
        Opacity = 1 - Ease((_elapsed - 0.85) / 0.25);
        DrawArtwork();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) =>
        index == 0 ? _artwork : throw new ArgumentOutOfRangeException(nameof(index));

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _sky = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        DrawArtwork();
    }

    private void DrawArtwork()
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        using var dc = _artwork.RenderOpen();
        dc.DrawDrawing(_sky ??= CreateSky());
        var center = new Point(ActualWidth / 2, ActualHeight / 2 - 30);
        var gather = Ease(_elapsed / 0.72);
        var reveal = Ease((_elapsed - 0.12) / 0.5);
        var star = IsDarkMode ? StarBrush : LightStarBrush;

        dc.PushOpacity(0.5 + 0.5 * Math.Sin(gather * Math.PI));
        dc.DrawEllipse(BlueGlow, null, center, 210, 180);
        dc.DrawEllipse(VioletGlow, null, new Point(center.X + 55, center.Y + 25), 180, 155);
        dc.Pop();

        for (var i = 0; i < 32; i++)
        {
            var angle = i * 2.399963 + (1 - gather) * 0.32;
            var distance = 80 + i % 5 * 9 + (1 - gather) * (145 + i % 7 * 19);
            var point = new Point(center.X + Math.Cos(angle) * distance,
                center.Y + Math.Sin(angle) * distance * 0.64);
            dc.PushOpacity((0.2 + 0.55 * gather) * (1 - 0.65 * Ease((_elapsed - 0.65) / 0.2)));
            dc.DrawEllipse(star, null, point, i % 6 == 0 ? 1.7 : 1, i % 6 == 0 ? 1.7 : 1);
            if (i % 6 == 0)
            {
                var pen = new Pen(star, 0.7);
                dc.DrawLine(pen, new Point(point.X - 4, point.Y), new Point(point.X + 4, point.Y));
                dc.DrawLine(pen, new Point(point.X, point.Y - 4), new Point(point.X, point.Y + 4));
            }
            dc.Pop();
        }

        dc.PushOpacity(reveal);
        var scale = 0.88 + 0.12 * reveal;
        var iconCenter = new Point(center.X, center.Y + 10 * (1 - reveal));
        dc.PushTransform(new ScaleTransform(scale, scale, iconCenter.X, iconCenter.Y));
        if (Logo is { } logo) dc.DrawImage(logo, new Rect(iconCenter.X - 56, iconCenter.Y - 56, 112, 112));
        dc.Pop();
        dc.Pop();

        _title ??= Text("StellovisMC", 29, IsDarkMode ? "#F0F5FF" : "#253E5E", FontWeights.SemiBold);
        _subtitle ??= Text("MINECRAFT JAVA EDITION", 10, IsDarkMode ? "#C0D1EC" : "#536F94", FontWeights.Medium);
        dc.PushOpacity(Ease((_elapsed - 0.3) / 0.36));
        dc.DrawText(_title, new Point(center.X - _title.Width / 2, center.Y + 78));
        dc.Pop();
        dc.PushOpacity(Ease((_elapsed - 0.42) / 0.36));
        dc.DrawText(_subtitle, new Point(center.X - _subtitle.Width / 2, center.Y + 123));
        dc.Pop();
    }

    private DrawingGroup CreateSky()
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            var background = new LinearGradientBrush(
                Color(IsDarkMode ? "#080F22" : "#EFF4FC"), Color(IsDarkMode ? "#192B48" : "#DDE8F7"), 35);
            background.Freeze();
            dc.DrawRectangle(background, null, new Rect(0, 0, ActualWidth, ActualHeight));
            dc.DrawEllipse(Cloud(IsDarkMode ? "#504A9B" : "#A9B9E3", 0.16), null,
                new Point(ActualWidth * 0.65, ActualHeight * 0.6), ActualWidth * 0.5, ActualHeight * 0.7);
            var random = new Random(71026);
            for (var i = 0; i < 65; i++)
            {
                dc.PushOpacity(0.1 + random.NextDouble() * 0.3);
                var point = new Point(random.NextDouble() * ActualWidth, random.NextDouble() * ActualHeight);
                dc.DrawEllipse(IsDarkMode ? StarBrush : LightStarBrush, null, point, 0.7, 0.7);
                dc.Pop();
            }
        }
        group.Freeze();
        return group;
    }

    private FormattedText Text(string text, double size, string color, FontWeight weight) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size, Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static double Ease(double progress)
    {
        var value = Math.Clamp(progress, 0, 1);
        return 1 - Math.Pow(1 - value, 3);
    }

    private static Color Color(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush(Color(color));
        brush.Freeze();
        return brush;
    }
    private static RadialGradientBrush Cloud(string color, double opacity)
    {
        var center = Color(color);
        var brush = new RadialGradientBrush(center, System.Windows.Media.Color.FromArgb(0, center.R, center.G, center.B)) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }
}
