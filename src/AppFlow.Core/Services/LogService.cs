namespace AppFlow.Core.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using Serilog;
using System.Collections.Concurrent;

public sealed class LogService : ILogService, IDisposable
{
    private const int MaxLogs = 1000;
    private readonly ConcurrentQueue<string> _recentLogs = new();
    private readonly ILogger _logger;

    public LogService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDirectory = Path.Combine(appData, "AppFlow", "logs");
        Directory.CreateDirectory(logDirectory);
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "appflow-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .CreateLogger();
    }

    public void LogInfo(string msg)
    {
        _logger.Information("{Message}", msg);
        EnqueueLog($"INFO: {msg}");
    }

    public void LogWarning(string msg)
    {
        _logger.Warning("{Message}", msg);
        EnqueueLog($"WARN: {msg}");
    }

    public void LogError(string msg, Exception? ex = null)
    {
        _logger.Error(ex, "{Message}", msg);
        EnqueueLog($"ERROR: {msg}" + (ex is null ? string.Empty : $" - {ex.Message}"));
    }

    public void LogAction(ActionResult result) =>
        LogInfo($"Action {result.ActionPerformed} on {result.PackageId} via {result.SourceUsed}. Success: {result.Success}");

    public List<string> GetRecentLogs(int count = 100) =>
        _recentLogs.TakeLast(Math.Clamp(count, 0, MaxLogs)).ToList();

    public void Dispose()
    {
        if (_logger is IDisposable disposable)
            disposable.Dispose();
    }

    private void EnqueueLog(string msg)
    {
        _recentLogs.Enqueue($"[{DateTime.UtcNow:O}] {msg}");
        while (_recentLogs.Count > MaxLogs)
            _recentLogs.TryDequeue(out _);
    }
}
