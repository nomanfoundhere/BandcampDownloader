using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Threading;

namespace BandcampDownloader.Avalonia.Tests;

/// <summary>
/// Captures the Avalonia log while a window is constructed/rendered, so that XAML and binding errors
/// (which are only logged, not thrown by Avalonia) fail the tests.
/// </summary>
internal sealed class AvaloniaLogScope : IDisposable
{
    private static readonly object InstallLock = new();
    private static readonly ConcurrentQueue<LogEntry> Entries = new();
    private static CollectorSink? _sink;

    private readonly int _entryCountAtStart;

    private AvaloniaLogScope()
    {
        _entryCountAtStart = Entries.Count;
    }

    /// <summary>
    /// Starts capturing the Avalonia log.
    /// </summary>
    public static AvaloniaLogScope Start()
    {
        lock (InstallLock)
        {
            if (_sink is null)
            {
                _sink = new CollectorSink();
                Logger.Sink = _sink;
            }
        }

        return new AvaloniaLogScope();
    }

    /// <summary>
    /// Returns the binding warnings/errors logged since the scope started.
    /// </summary>
    public IReadOnlyList<LogEntry> BindingErrorsSinceStart()
    {
        return Entries
            .Skip(_entryCountAtStart)
            .Where(entry => entry.Area == LogArea.Binding && entry.Level >= LogEventLevel.Warning)
            .ToList();
    }

    public void Dispose()
    {
        // The sink stays installed for the whole test run: the application only logs errors anyway.
    }

    public sealed record LogEntry(LogEventLevel Level, string Area, string Message);

    private sealed class CollectorSink : ILogSink
    {
        public bool IsEnabled(LogEventLevel level, string area)
        {
            return level >= LogEventLevel.Warning;
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
        {
            Entries.Enqueue(new LogEntry(level, area, messageTemplate));
        }

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            var message = messageTemplate;
            try
            {
                message = string.Format(System.Globalization.CultureInfo.InvariantCulture, messageTemplate, propertyValues);
            }
            catch (FormatException)
            {
                // Keep the raw message template; the test report does not need formatted values.
            }

            Entries.Enqueue(new LogEntry(level, area, message));
        }
    }
}

/// <summary>
/// Renders windows to PNG files under <c>artifacts/ui-screenshots/</c> as verification evidence.
/// </summary>
internal static class Screenshots
{
    /// <summary>
    /// Renders the specified window to <c>artifacts/ui-screenshots/&lt;fileName&gt;</c> and returns the file path.
    /// </summary>
    public static string Save(Window window, string fileName)
    {
        var directory = Path.Combine(FindRepositoryRoot(), "artifacts", "ui-screenshots");
        Directory.CreateDirectory(directory);

        // Let layout, styles and the render pass run before capturing the frame.
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()
                          ?? throw new InvalidOperationException($"No rendered frame available for {window.GetType().Name}");

        var path = Path.Combine(directory, fileName);
        frame.Save(path);

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length < 2_000)
        {
            throw new InvalidOperationException(
                $"The screenshot {path} is suspiciously small ({fileInfo.Length} bytes): the window probably rendered blank");
        }

        TestContext.Out.WriteLine($"[screenshot] {window.GetType().Name} -> {path} ({frame.PixelSize.Width}x{frame.PixelSize.Height}, {fileInfo.Length} bytes)");
        return path;
    }

    /// <summary>
    /// Walks up from the test output directory until the repository root (which contains scripts/dev-env.sh).
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "scripts", "dev-env.sh")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
