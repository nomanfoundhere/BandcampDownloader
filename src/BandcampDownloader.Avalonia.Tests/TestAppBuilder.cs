using System;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;
using BandcampDownloader.Avalonia;

// REQUIRED for Avalonia.Headless.NUnit: exactly one assembly-level attribute pointing at a static
// class with a public static AppBuilder BuildAvaloniaApp() method.
[assembly: AvaloniaTestApplication(typeof(BandcampDownloader.Avalonia.Tests.TestAppBuilder))]

namespace BandcampDownloader.Avalonia.Tests;

public static class TestAppBuilder
{
    // UseHeadlessDrawing = false + UseSkia() is what makes CaptureRenderedFrame() return real pixels
    // (instead of a no-op drawing surface): this is the recipe validated in
    // spikes/avalonia-macos-smoke/headless-tests.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>(() => new App())
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
