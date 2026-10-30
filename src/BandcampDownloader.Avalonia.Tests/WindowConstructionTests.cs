using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using BandcampDownloader.Avalonia.Core.Theme;
using BandcampDownloader.Avalonia.UI.Settings;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Avalonia.UI.Update;
using BandcampDownloader.Settings;

namespace BandcampDownloader.Avalonia.Tests;

/// <summary>
/// Headless tests of the Avalonia head: every window must be constructible from the real dependency
/// injection composition, be renderable (frame capture), and produce no Avalonia binding errors.
/// </summary>
/// <remarks>
/// The UI is ported concurrently with these tests: they deliberately do not assert the exact content of
/// the controls (which changes as the port progresses), only that the windows load and render.
/// </remarks>
[TestFixture]
public sealed class WindowConstructionTests
{
    private static readonly string[] SettingsTabScreenshots =
    [
        "settings-general.png",
        "settings-tags.png",
        "settings-cover-art.png",
        "settings-playlist.png",
        "settings-downloads.png",
        "settings-network.png",
        "settings-advanced.png",
        "settings-about.png",
    ];

    private AvaloniaLogScope _logScope = null!;

    [SetUp]
    public void SetUp()
    {
        TestEnvironment.EnsureInitialized();
        _logScope = AvaloniaLogScope.Start();

        // The theme is applied by App.axaml.cs too, once the Avalonia application exists.
        var themeService = TestEnvironment.Container.GetService<IThemeService>();
        themeService.ApplySkin(TestEnvironment.GetUserSettings().Theme);
    }

    [TearDown]
    public void TearDown()
    {
        _logScope.Dispose();
    }

    [AvaloniaTest]
    public void MainWindow_Is_Constructible_And_Renders()
    {
        // Construct through the dependency injection container composition (like App.axaml.cs does).
        var window = TestWindowFactory.Create(typeof(MainWindow));

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(window.IsVisible, Is.True);
        Screenshots.Save(window, "main-window.png");
        AssertNoBindingErrors();

        window.Close();
    }

    [AvaloniaTest]
    public void SettingsWindow_Is_Constructible_And_Renders_Every_Section()
    {
        // Inactive downloads: all sections enabled.
        var window = TestWindowFactory.Create(typeof(SettingsWindow), false);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(window.IsVisible, Is.True);
        Screenshots.Save(window, SettingsTabScreenshots[0]);

        // Every editable settings section is instantiated by the SettingsWindow XAML and keeps the
        // IUserControlSettings contract used by SettingsWindow (LoadSettings/CancelChanges/SaveSettings).
        // "About" is a section too but has no settings: like in the WPF version it does not implement the interface.
        var settingsSections = window.GetLogicalDescendants().OfType<IUserControlSettings>().ToList();
        Assert.That(
            settingsSections,
            Has.Count.EqualTo(SettingsTabScreenshots.Length - 1),
            "The 7 editable settings sections must be instantiated and implement IUserControlSettings");

        // All settings sections are instantiated by the SettingsWindow XAML; walk through the navigation
        // list to also verify that each one can be selected and rendered.
        var sectionsListBox = window
            .GetLogicalDescendants()
            .OfType<ListBox>()
            .FirstOrDefault(listBox => listBox.ItemCount == SettingsTabScreenshots.Length);

        if (sectionsListBox is null)
        {
            TestContext.Out.WriteLine(
                "[screenshot] WARNING: could not find the settings sections ListBox; per-section screenshots skipped " +
                "(the sections were still constructed by the XAML)");
        }
        else
        {
            for (var index = 0; index < SettingsTabScreenshots.Length && index < sectionsListBox.ItemCount; index++)
            {
                sectionsListBox.SelectedIndex = index;
                Dispatcher.UIThread.RunJobs();
                Screenshots.Save(window, SettingsTabScreenshots[index]);
            }
        }

        AssertNoBindingErrors();
        window.Close();
    }

    [AvaloniaTest]
    public void SettingsWindow_Is_Constructible_While_Downloads_Are_Active()
    {
        // Active downloads: the sections are disabled and the warning panel is visible.
        var window = TestWindowFactory.Create(typeof(SettingsWindow), true);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(window.IsVisible, Is.True);
            Assert.That(((SettingsWindow)window).ActiveDownloads, Is.True);
        }

        AssertNoBindingErrors();
        window.Close();
    }

    [AvaloniaTest]
    public void WindowUpdate_Is_Constructible_And_Renders()
    {
        var window = TestWindowFactory.Create(typeof(WindowUpdate));

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(window.IsVisible, Is.True);
        Screenshots.Save(window, "window-update.png");
        AssertNoBindingErrors();

        window.Close();
    }

    [AvaloniaTest]
    public void MessageBoxWindow_Is_Constructible_Configured_And_Renders()
    {
        var window = TestWindowFactory.Create(typeof(MessageBoxWindow));
        var messageBoxWindow = (MessageBoxWindow)window;

        messageBoxWindow.Configure(
            "A test message: the album could not be downloaded.",
            MessageBoxButtons.OkCancel,
            MessageBoxIcon.Warning,
            "Bandcamp Downloader");

        messageBoxWindow.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.That(messageBoxWindow.IsVisible, Is.True);
        Screenshots.Save(messageBoxWindow, "message-box.png");
        AssertNoBindingErrors();

        messageBoxWindow.Close();
    }

    private void AssertNoBindingErrors()
    {
        var bindingErrors = _logScope.BindingErrorsSinceStart();
        var formattedErrors = string.Join(
            Environment.NewLine,
            bindingErrors.Select(error => $"[{error.Level}] {error.Area}: {error.Message}"));

        Assert.That(bindingErrors, Is.Empty, $"Avalonia logged binding errors:{Environment.NewLine}{formattedErrors}");
    }
}
