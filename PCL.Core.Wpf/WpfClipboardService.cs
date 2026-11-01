using System.Windows;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF <see cref="Clipboard" /> 的剪贴板实现。</summary>
public sealed class WpfClipboardService : IClipboardService
{
    public string[]? GetFileDropList()
    {
        return PlatformServices.UiDispatcher?.Invoke(() =>
        {
            var dataObject = Clipboard.GetDataObject();
            return dataObject?.GetData(DataFormats.FileDrop) as string[];
        });
    }

    public string? GetText()
    {
        return PlatformServices.UiDispatcher?.Invoke(Clipboard.GetText);
    }

    public void SetText(string text)
    {
        PlatformServices.UiDispatcher?.Invoke(() => Clipboard.SetText(text));
    }
}
