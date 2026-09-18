using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public sealed class TerminalProgress
{
    static readonly string[] _SPINNER = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    readonly TextWriter _output;
    readonly bool _interactive;
    readonly Func<int> _widthProvider;
    int _spinnerFrame;

    public TerminalProgress(TextWriter output, bool interactive, Func<int> widthProvider = null)
    {
        _output = output;
        _interactive = interactive;
        _widthProvider = widthProvider ?? (() => 100);
    }

    public static TerminalProgress ForConsole()
    {
        var interactive = !Console.IsOutputRedirected
            && !string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase);

        return new TerminalProgress(Console.Out, interactive, GetConsoleWidth);
    }

    public async Task<T> RunAsync<T>(
        string phase,
        int completed,
        int total,
        string item,
        Func<Task<T>> operation,
        Func<T, bool> succeeded)
    {
        if (!_interactive)
        {
            _output.WriteLine($"{phase} [{completed + 1}/{total}]: {item}");
        }

        var stopwatch = Stopwatch.StartNew();
        Task<T> operationTask;
        try
        {
            operationTask = operation();
        }
        catch
        {
            Complete(phase, completed + 1, total, item, stopwatch.Elapsed, success: false);
            throw;
        }

        if (_interactive)
        {
            while (!operationTask.IsCompleted)
            {
                Render(phase, completed, total, item, stopwatch.Elapsed, _SPINNER[_spinnerFrame++ % _SPINNER.Length]);
                await Task.WhenAny(operationTask, Task.Delay(100));
            }
        }

        try
        {
            var result = await operationTask;
            Complete(phase, completed + 1, total, item, stopwatch.Elapsed, succeeded(result));
            return result;
        }
        catch
        {
            Complete(phase, completed + 1, total, item, stopwatch.Elapsed, success: false);
            throw;
        }
    }

    public static string FormatLine(
        string indicator,
        string phase,
        int completed,
        int total,
        string item,
        TimeSpan elapsed,
        int width)
    {
        total = Math.Max(total, 1);
        completed = Math.Clamp(completed, 0, total);
        var percent = completed * 100 / total;
        const int barWidth = 12;
        var filled = completed * barWidth / total;
        var bar = new string('━', filled) + new string('─', barWidth - filled);
        var elapsedText = elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s"
            : $"{elapsed.TotalSeconds:0.0}s";
        var prefix = $"{indicator} {phase} [{bar}] {completed}/{total} {percent,3}%";
        var suffix = $" • {elapsedText}";
        var availableForItem = Math.Max(0, width - prefix.Length - suffix.Length - 3);
        var displayItem = Truncate(item ?? string.Empty, availableForItem);

        return displayItem.Length == 0
            ? Truncate(prefix + suffix, width)
            : $"{prefix} • {displayItem}{suffix}";
    }

    void Render(string phase, int completed, int total, string item, TimeSpan elapsed, string indicator)
    {
        var line = FormatLine(indicator, phase, completed, total, item, elapsed, _widthProvider());
        _output.Write($"\r\u001b[2K{line}");
        _output.Flush();
    }

    void Complete(string phase, int completed, int total, string item, TimeSpan elapsed, bool success)
    {
        var indicator = success ? "✓" : "✗";
        var line = FormatLine(indicator, phase, completed, total, item, elapsed, _widthProvider());
        if (_interactive)
        {
            _output.Write($"\r\u001b[2K{line}{Environment.NewLine}");
        }
        else
        {
            _output.WriteLine(line);
        }
        _output.Flush();
    }

    static string Truncate(string value, int width)
    {
        if (width <= 0) return string.Empty;
        if (value.Length <= width) return value;
        if (width == 1) return "…";
        return value[..(width - 1)] + "…";
    }

    static int GetConsoleWidth()
    {
        try
        {
            return Math.Max(20, Console.WindowWidth - 1);
        }
        catch
        {
            return 100;
        }
    }
}
