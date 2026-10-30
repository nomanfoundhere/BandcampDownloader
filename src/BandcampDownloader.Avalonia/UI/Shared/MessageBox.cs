using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using BandcampDownloader.Avalonia.Core.Localization;

namespace BandcampDownloader.Avalonia.UI.Shared;

internal enum MessageBoxButtons
{
    Ok,
    OkCancel,
    YesNo,
}

internal enum MessageBoxIcon
{
    Info,
    Warning,
    Error,
    Question,
}

/// <summary>
/// Cross-platform replacement for the WPF message boxes used by the Windows version.
/// </summary>
internal static class MessageBox
{
    /// <summary>
    /// Displays a message box and returns true if the user answered OK or Yes; false otherwise.
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> ShowAsync(
        Window owner,
        string message,
        MessageBoxButtons buttons = MessageBoxButtons.Ok,
        MessageBoxIcon icon = MessageBoxIcon.Info,
        string title = "Bandcamp Downloader")
    {
        var window = new MessageBoxWindow();
        window.Configure(message, buttons, icon, title);

        return await window.ShowDialogAsync(owner);
    }
}

internal sealed partial class MessageBoxWindow : Window
{
    private System.Threading.Tasks.TaskCompletionSource<bool> _result;

    public MessageBoxWindow()
    {
        InitializeComponent();
    }

    public void Configure(string message, MessageBoxButtons buttons, MessageBoxIcon icon, string title)
    {
        Title = title;
        MessageText.Text = message;
        IconText.Text = icon switch
        {
            MessageBoxIcon.Info => "ℹ",
            MessageBoxIcon.Warning => "⚠",
            MessageBoxIcon.Error => "⛔",
            MessageBoxIcon.Question => "?",
            _ => string.Empty,
        };
        IconText.Foreground = icon switch
        {
            MessageBoxIcon.Warning => Brushes.Orange,
            MessageBoxIcon.Error => Brushes.Red,
            MessageBoxIcon.Info => Brushes.DodgerBlue,
            _ => Brushes.Gray,
        };

        ButtonOk.IsVisible = buttons != MessageBoxButtons.YesNo;
        ButtonOk.Content = Localizer.Instance["messageBoxButtonOK"];
        ButtonYes.IsVisible = buttons == MessageBoxButtons.YesNo;
        ButtonYes.Content = Localizer.Instance["messageBoxButtonYes"];
        ButtonCancel.IsVisible = buttons != MessageBoxButtons.Ok;
        ButtonCancel.Content = Localizer.Instance["messageBoxButtonCancel"];
        ButtonNo.IsVisible = buttons == MessageBoxButtons.YesNo;
        ButtonNo.Content = Localizer.Instance["messageBoxButtonNo"];
    }

    /// <summary>
    /// Shows the window modally and returns true if the user answered OK or Yes; false otherwise.
    /// </summary>
    public async System.Threading.Tasks.Task<bool> ShowDialogAsync(Window owner)
    {
        _result = new System.Threading.Tasks.TaskCompletionSource<bool>();

        if (owner != null)
        {
            return await ShowDialog<bool>(owner);
        }

        // No owner (for instance an error raised during startup): show a regular window.
        Show();
        return await _result.Task;
    }

    private void ButtonOk_Click(object sender, RoutedEventArgs e) => Close(true);

    private void ButtonYes_Click(object sender, RoutedEventArgs e) => Close(true);

    private void ButtonNo_Click(object sender, RoutedEventArgs e) => Close(false);

    private void ButtonCancel_Click(object sender, RoutedEventArgs e) => Close(false);

    protected override void OnClosed(EventArgs e)
    {
        _result?.TrySetResult(false);
        base.OnClosed(e);
    }
}
