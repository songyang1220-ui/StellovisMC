using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Core.Wpf;

namespace PCL.Core.Test.UI;

[TestClass]
[DoNotParallelize]
public class WpfClipboardServiceTest
{
    [TestMethod]
    public async Task BackgroundClipboardOperationsUseTheStaDispatcher()
    {
        using var dispatcher = new ClipboardDispatcher();
        var previousDispatcher = PlatformServices.UiDispatcher;
        IDataObject? original = dispatcher.Invoke(Clipboard.GetDataObject);
        PlatformServices.UiDispatcher = dispatcher;
        try
        {
            var clipboard = new WpfClipboardService();
            const string sample = "StellovisMC clipboard regression test";
            await Task.Run(() => clipboard.SetText(sample));
            var roundTrip = await Task.Run(clipboard.GetText);
            Assert.IsTrue(roundTrip == sample, "Background clipboard text round-trip failed.");
            dispatcher.Invoke(() =>
            {
                var files = new System.Collections.Specialized.StringCollection { @"C:\StellovisMC-clipboard-test.txt" };
                Clipboard.SetFileDropList(files);
            });
            var paths = await Task.Run(clipboard.GetFileDropList);
            Assert.IsTrue(paths is { Length: 1 } && paths[0] == @"C:\StellovisMC-clipboard-test.txt",
                "Background clipboard file-drop reading failed.");
        }
        finally
        {
            PlatformServices.UiDispatcher = previousDispatcher;
            // Keep the user's clipboard data in memory and restore it without logging its contents.
            dispatcher.Invoke(() =>
            {
                if (original is null) Clipboard.Clear();
                else Clipboard.SetDataObject(original, true);
            });
        }
    }

    private sealed class ClipboardDispatcher : IUiDispatcher, IDisposable
    {
        private readonly Thread _thread;
        private readonly Dispatcher _dispatcher;

        public ClipboardDispatcher()
        {
            using var ready = new ManualResetEventSlim();
            Dispatcher? dispatcher = null;
            _thread = new Thread(() =>
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            }) { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            Assert.IsTrue(ready.Wait(TimeSpan.FromSeconds(5)));
            _dispatcher = dispatcher!;
        }

        public bool CheckAccess() => _dispatcher.CheckAccess();
        public void Post(Action action) => _dispatcher.BeginInvoke(action);
        public void Invoke(Action action) => _dispatcher.Invoke(action);
        public T? Invoke<T>(Func<T> func) => _dispatcher.Invoke(func);
        public Task InvokeAsync(Action action) => _dispatcher.InvokeAsync(action).Task;
        public Task<T> InvokeAsync<T>(Func<T> func) => _dispatcher.InvokeAsync(func).Task;
        public IUiModalFrame CreateModalFrame() => throw new NotSupportedException();
        public void Dispose()
        {
            _dispatcher.InvokeShutdown();
            Assert.IsTrue(_thread.Join(TimeSpan.FromSeconds(5)));
        }
    }
}
