using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL;

public static class ThemeManager
{
    private static bool _contextMenuHandlerRegistered;

    public static bool IsDarkMode => ThemeService.IsDarkMode;

    public static ResourceDictionary AppResources => System.Windows.Application.Current.Resources;

    public static Common.MyColor colorGray1 = new(AppResources["ColorObjectGray1"]);
    public static Common.MyColor colorGray4 = new(AppResources["ColorObjectGray4"]);
    public static Common.MyColor colorGray5 = new(AppResources["ColorObjectGray5"]);

    public static Common.MyColor colorSemiTransparent =
        new(AppResources["ColorBrushSemiTransparent"]);

    public static void ThemeRefresh(int newTheme = -1)
    {
        ApplyStellovisPalette();
        colorGray1 = new Common.MyColor(AppResources["ColorObjectGray1"]);
        colorGray4 = new Common.MyColor(AppResources["ColorObjectGray4"]);
        colorGray5 = new Common.MyColor(AppResources["ColorObjectGray5"]);
        colorSemiTransparent = new Common.MyColor(AppResources["ColorBrushSemiTransparent"]);
        ThemeRefreshMain();
    }

    private static void ApplyStellovisPalette()
    {
        var colors = new (string Key, string Light, string Dark)[]
        {
            ("Canvas", "#F0F3F8", "#101722"),
            ("Surface", "#F7FFFFFF", "#E61B2533"),
            ("SurfaceHover", "#F2F6FC", "#253448"),
            ("SurfaceMuted", "#E9EFF6", "#17212E"),
            ("Border", "#DCE4EE", "#2B3A4D"),
            ("Text", "#263548", "#E7EEF8"),
            ("MutedText", "#66778C", "#9BADBF"),
            ("Accent", "#3D6AA0", "#426D9F"),
            ("AccentHover", "#345D8D", "#4A75A3"),
            ("NavigationText", "#305F99", "#BEDBFF"),
            ("OnAccent", "#FFFFFF", "#FFFFFF"),
            ("Header", "#263E5B", "#172434"),
            ("Sidebar", "#8CF4F8FF", "#12D5E7FF"),
            ("Hero", "#ECE5EDF7", "#D0203249"),
            ("HeroText", "#243F60", "#E7EEF9"),
            ("HeroSubtext", "#47627F", "#C5D4E6")
        };
        foreach (var (key, light, dark) in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsDarkMode ? dark : light));
            brush.Freeze();
            AppResources[$"Stellovis{key}Brush"] = brush;
        }

        foreach (var (key, source) in new[]
        {
            ("1", "Text"), ("Gray1", "Text"), ("Gray2", "MutedText"),
            ("Background", "Canvas"), ("TransparentBackground", "Surface")
        })
        {
            var brush = (SolidColorBrush)AppResources[$"Stellovis{source}Brush"];
            AppResources[$"ColorBrush{key}"] = brush;
            AppResources[$"ColorObject{key}"] = brush.Color;
        }
    }

    public static void ThemeRefreshMain()
    {
        Common.RunInUi(() =>
        {
            if (!Launcher.frmMain.IsLoaded) return;
            RefreshBackground();
            RefreshAllContextMenuThemes();
        });
    }
    
    // 主页面背景
    private static void RefreshBackground()
    {
        Launcher.frmMain.Starfield.IsDarkMode = IsDarkMode;
        Launcher.frmMain.Starfield.IsEnabled = Config.Preference.Background.BackgroundColorful;
        if (Config.Preference.Background.BackgroundColorful)
        {
            var brush = new LinearGradientBrush
            {
                EndPoint = new Point(0.1, 1),
                StartPoint = new Point(0.9, 0)
            };

            brush.GradientStops.Add(new GradientStop(((SolidColorBrush)AppResources["StellovisCanvasBrush"]).Color, 0));
            brush.GradientStops.Add(new GradientStop(((SolidColorBrush)AppResources["StellovisHeroBrush"]).Color, 1));
            Launcher.frmMain.PanForm.Background = brush;
        }
        else
        {
            Launcher.frmMain.PanForm.Background =
                (Brush)System.Windows.Application.Current.Resources["ColorBrushBackground"];
        }

        Launcher.frmMain.PanForm.Background.Freeze();
    }

    // 通用ContextMenu主题刷新
    private static void RefreshAllContextMenuThemes()
    {
        try
        {
            if (!_contextMenuHandlerRegistered)
            {
                EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent,
                    new RoutedEventHandler(OnContextMenuOpened));
                _contextMenuHandlerRegistered = true;
            }

            foreach (Window window in System.Windows.Application.Current.Windows)
                RefreshContextMenusInElement(window);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "刷新ContextMenu主题时出错");
        }
    }

    private static void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is ContextMenu contextMenu)
            {
                contextMenu.ClearValue(FrameworkElement.StyleProperty);
                contextMenu.UpdateDefaultStyle();
            }
        }
        catch
        {
            // 忽略个别错误
        }
    }

    private static void RefreshContextMenusInElement(DependencyObject element)
    {
        if (element is null)
            return;

        try
        {
            if (element is FrameworkElement { ContextMenu: not null } fe)
            {
                fe.ContextMenu.ClearValue(FrameworkElement.StyleProperty);
                fe.ContextMenu.UpdateDefaultStyle();
            }

            var childrenCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childrenCount; i++)
                RefreshContextMenusInElement(VisualTreeHelper.GetChild(element, i));
        }
        catch
        {
            // 忽略个别元素的错误，继续处理其他元素
        }
    }
}
