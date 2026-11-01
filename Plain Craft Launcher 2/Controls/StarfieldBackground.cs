using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace PCL;

/// <summary>A cached sky with a small, frame-limited layer of drifting stardust.</summary>
public sealed class StarfieldBackground : FrameworkElement
{
    private readonly record struct Star(double X, double Y, double Radius, double Phase, double Speed);
    private readonly record struct MeteorPass(double Time, double X, double Y, double Travel);

    private static readonly Star[] Stars = CreateStars(144, 51706);
    private static readonly Star[] Dust = CreateStars(38, 11240);
    private static readonly SolidColorBrush DarkStar = CreateBrush("#D4E6FF");
    private static readonly SolidColorBrush LightStar = CreateBrush("#557CAA");
    private static readonly Pen DarkRay = CreatePen("#A2C6EF", 0.65);
    private static readonly Pen LightRay = CreatePen("#6589B5", 0.65);
    private static readonly MeteorPass[] MeteorPasses =
    [
        new(4, 0.98, 0.52, 0.5),
        new(15, 0.81, 0.06, 0.46),
        new(29, 1.02, 0.33, 0.55),
        new(41, 0.68, 0.15, 0.42)
    ];
    private static readonly Pen DarkMeteor = CreateMeteorPen("#D9EEFF", 1.6);
    private static readonly Pen DarkMeteorGlow = CreateMeteorPen("#84B6FF", 5);
    private static readonly Pen LightMeteor = CreateMeteorPen("#456FA5", 1.6);
    private static readonly RadialGradientBrush Glow = CreateCloudBrush("#9FCBFF", 0.24);

    private DrawingGroup? _sky;
    private readonly DrawingVisual _particles = new();
    private Window? _owner;
    private TimeSpan? _lastFrame;
    private double _sceneTime;
    private bool _isRendering;

    public static readonly DependencyProperty IsDarkModeProperty = DependencyProperty.Register(
        nameof(IsDarkMode), typeof(bool), typeof(StarfieldBackground),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((StarfieldBackground)d)._sky = null));

    public static readonly DependencyProperty MotionEnabledProperty = DependencyProperty.Register(
        nameof(MotionEnabled), typeof(bool), typeof(StarfieldBackground),
        new PropertyMetadata(true, (d, _) =>
        {
            var sky = (StarfieldBackground)d;
            sky.UpdateAnimationState();
            sky.DrawParticles();
        }));

    public bool IsDarkMode
    {
        get => (bool)GetValue(IsDarkModeProperty);
        set => SetValue(IsDarkModeProperty, value);
    }

    public bool MotionEnabled
    {
        get => (bool)GetValue(MotionEnabledProperty);
        set => SetValue(MotionEnabledProperty, value);
    }

    public StarfieldBackground()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
        AddVisualChild(_particles);
        Loaded += OnLoaded;
        Unloaded += (_, _) => Detach();
        IsVisibleChanged += (_, _) => UpdateAnimationState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Detach();
        _owner = Window.GetWindow(this);
        if (_owner is not null)
        {
            _owner.Activated += OnWindowActivityChanged;
            _owner.Deactivated += OnWindowActivityChanged;
            _owner.StateChanged += OnWindowActivityChanged;
            _owner.Closed += OnWindowClosed;
        }
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        UpdateAnimationState();
    }

    private void OnWindowActivityChanged(object? sender, EventArgs e) => UpdateAnimationState();
    private void OnWindowClosed(object? sender, EventArgs e) => Detach();

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
        void RefreshMotion()
        {
            UpdateAnimationState();
            DrawParticles();
        }
        if (Dispatcher.CheckAccess()) RefreshMotion();
        else Dispatcher.BeginInvoke((Action)RefreshMotion);
    }

    private void UpdateAnimationState()
    {
        var shouldRender = IsLoaded && IsVisible && MotionEnabled && SystemParameters.ClientAreaAnimation
            && _owner is { IsActive: true, WindowState: not WindowState.Minimized };
        if (shouldRender == _isRendering) return;
        _isRendering = shouldRender;
        _lastFrame = null;
        if (shouldRender) CompositionTarget.Rendering += OnRendering;
        else CompositionTarget.Rendering -= OnRendering;
    }

    private void Detach()
    {
        CompositionTarget.Rendering -= OnRendering;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        _isRendering = false;
        _lastFrame = null;
        if (_owner is null) return;
        _owner.Activated -= OnWindowActivityChanged;
        _owner.Deactivated -= OnWindowActivityChanged;
        _owner.StateChanged -= OnWindowActivityChanged;
        _owner.Closed -= OnWindowClosed;
        _owner = null;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs frame) AdvanceFrame(frame.RenderingTime);
    }

    internal void AdvanceFrame(TimeSpan frameTime)
    {
        if (!_isRendering) return;
        if (_lastFrame is null)
        {
            _lastFrame = frameTime;
            return;
        }
        var elapsed = (frameTime - _lastFrame.Value).TotalSeconds;
        if (elapsed + 0.0005 < 1d / 30d) return;
        _sceneTime += Math.Clamp(elapsed, 0, 0.1);
        _lastFrame = frameTime;
        DrawParticles();
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) =>
        index == 0 ? _particles : throw new ArgumentOutOfRangeException(nameof(index));

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _sky = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;

        dc.DrawDrawing(_sky ??= CreateSky(width, height));
        DrawParticles();
    }

    private void DrawParticles()
    {
        // Updating a DrawingVisual avoids a layout pass for every particle frame.
        using var dc = _particles.RenderOpen();
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 1 || height < 1) return;
        var brush = IsDarkMode ? DarkStar : LightStar;
        var ray = IsDarkMode ? DarkRay : LightRay;
        for (var i = 0; i < Dust.Length; i++)
        {
            var star = Dust[i];
            var x = Wrap(star.X * width + _sceneTime * star.Speed, width);
            var y = Wrap(star.Y * height - _sceneTime * star.Speed * 0.3, height);
            var twinkle = 0.45 + 0.25 * Math.Sin(_sceneTime * 0.55 + star.Phase);
            var point = new Point(x, y);
            dc.PushOpacity(twinkle);
            if (IsDarkMode && i % 7 == 0) dc.DrawEllipse(Glow, null, point, 8, 8);
            dc.DrawEllipse(brush, null, point, star.Radius, star.Radius);
            if (i % 7 == 0)
            {
                var length = 3 + star.Radius;
                dc.DrawLine(ray, new Point(x - length, y), new Point(x + length, y));
                dc.DrawLine(ray, new Point(x, y - length), new Point(x, y + length));
            }
            dc.Pop();
        }

        DrawMeteors(dc, width, height);
    }

    internal void DrawMeteors(DrawingContext dc, double width, double height)
    {
        if (!MotionEnabled || !SystemParameters.ClientAreaAnimation) return;
        var cycleTime = _sceneTime % 56;
        foreach (var pass in MeteorPasses)
        {
            var elapsed = cycleTime - pass.Time;
            if (elapsed is < 0 or >= 1.35) continue;
            var progress = elapsed / 1.35;
            var distance = width * pass.Travel * progress;
            var head = new Point(width * pass.X - distance, height * pass.Y + distance * 0.48);
            var length = Math.Clamp(width * 0.16, 90, 180);
            var tail = new Point(head.X + length, head.Y - length * 0.48);
            dc.PushOpacity(Math.Sin(progress * Math.PI) * (IsDarkMode ? 0.95 : 0.55));
            if (IsDarkMode)
            {
                dc.PushOpacity(0.2);
                dc.DrawLine(DarkMeteorGlow, tail, head);
                dc.Pop();
                dc.DrawEllipse(Glow, null, head, 10, 10);
            }
            dc.DrawLine(IsDarkMode ? DarkMeteor : LightMeteor, tail, head);
            dc.DrawEllipse(IsDarkMode ? DarkStar : LightStar, null, head, 1.5, 1.5);
            dc.Pop();
        }
    }

    private DrawingGroup CreateSky(double width, double height)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            var background = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString(IsDarkMode ? "#070F21" : "#EDF3FC"),
                (Color)ColorConverter.ConvertFromString(IsDarkMode ? "#142B46" : "#DCE8F7"),
                new Point(0, 0), new Point(1, 1));
            background.Freeze();
            dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
            dc.DrawEllipse(CreateCloudBrush(IsDarkMode ? "#334E95" : "#92ACD8", IsDarkMode ? 0.38 : 0.2),
                null, new Point(width * 0.16, height * 0.2), width * 0.66, height * 0.72);
            dc.DrawEllipse(CreateCloudBrush(IsDarkMode ? "#684492" : "#B8A9D8", IsDarkMode ? 0.28 : 0.12),
                null, new Point(width * 0.83, height * 0.76), width * 0.58, height * 0.64);
            dc.DrawEllipse(CreateCloudBrush(IsDarkMode ? "#397C91" : "#A4CDD8", IsDarkMode ? 0.2 : 0.15),
                null, new Point(width * 0.63, height * 0.12), width * 0.5, height * 0.48);
            foreach (var star in Stars)
            {
                dc.PushOpacity((IsDarkMode ? 0.22 : 0.13) + star.Speed * 0.08);
                dc.DrawEllipse(IsDarkMode ? DarkStar : LightStar, null,
                    new Point(star.X * width, star.Y * height), star.Radius * 0.7, star.Radius * 0.7);
                dc.Pop();
            }
        }
        group.Freeze();
        return group;
    }

    private static Star[] CreateStars(int count, int seed)
    {
        var random = new Random(seed);
        var stars = new Star[count];
        for (var i = 0; i < count; i++)
            stars[i] = new Star(random.NextDouble(), random.NextDouble(), 0.5 + random.NextDouble(),
                random.NextDouble() * Math.PI * 2, 0.7 + random.NextDouble() * 2.1);
        return stars;
    }

    private static double Wrap(double value, double extent) => value - Math.Floor(value / extent) * extent;

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(string color, double width)
    {
        var pen = new Pen(CreateBrush(color), width);
        pen.Freeze();
        return pen;
    }

    private static RadialGradientBrush CreateCloudBrush(string color, double opacity)
    {
        var center = (Color)ColorConverter.ConvertFromString(color);
        var edge = Color.FromArgb(0, center.R, center.G, center.B);
        var brush = new RadialGradientBrush(center, edge) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }

    private static Pen CreateMeteorPen(string color, double width)
    {
        var head = (Color)ColorConverter.ConvertFromString(color);
        var brush = new LinearGradientBrush(Color.FromArgb(0, head.R, head.G, head.B), head,
            new Point(1, 0), new Point(0, 1));
        brush.Freeze();
        var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }
}
