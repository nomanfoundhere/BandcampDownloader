using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace BandcampDownloader.Avalonia.Core;

/// <summary>
/// Helpers to run code on the UI thread of the Avalonia application.
/// </summary>
internal static class UiDispatcher
{
    public static async Task ExecuteOnUiAsync(System.Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(action);
        }
    }

    public static async Task<T> ExecuteOnUiAsync<T>(System.Func<T> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }

        return await Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <summary>
    /// Returns the main window of the application, if any.
    /// </summary>
    public static Window MainWindow => (Application.Current?.ApplicationLifetime as
        IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
