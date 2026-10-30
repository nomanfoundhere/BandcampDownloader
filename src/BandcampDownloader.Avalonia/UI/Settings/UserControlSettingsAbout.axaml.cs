using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Avalonia.UI.Update;
using BandcampDownloader.Core;

namespace BandcampDownloader.Avalonia.UI.Settings;

/// <summary>
/// "About" section of the settings window.
/// </summary>
internal sealed partial class UserControlSettingsAbout : UserControl
{
    public UserControlSettingsAbout()
    {
        InitializeComponent();

        Hyperlink.MakeLookLikeLink(TextBlockWebsite);
        Hyperlink.MakeLookLikeLink(TextBlockIssuesWebsite);
    }

    private void ButtonViewChangelog_Click(object sender, RoutedEventArgs e)
    {
        var windowUpdate = new WindowUpdate
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        var ownerWindow = TopLevel.GetTopLevel(this) as Window;
        if (ownerWindow != null)
        {
            windowUpdate.Show(ownerWindow);
        }
        else
        {
            windowUpdate.Show();
        }
    }

    private void TextBlockWebsite_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        Hyperlink.OpenUrl(Constants.URL_WEBSITE);
        e.Handled = true;
    }

    private void TextBlockIssuesWebsite_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        Hyperlink.OpenUrl(Constants.URL_ISSUES);
        e.Handled = true;
    }
}
