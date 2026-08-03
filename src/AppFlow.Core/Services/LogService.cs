namespace AppFlow.Core.Services;

using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;
using Serilog;
using System.Collections.Concurrent;

public class LogService : ILogService
{
    private readonly ConcurrentQueue<string> _recentLogs = new();
    private const int MaxLogs = 1000;

    public LogService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logPath = Path.Combine(appData, "AppFlow", "logs", "appflow-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    public void LogInfo(string msg)
    {
        Log.Information(msg);
        EnqueueLog($"INFO: {msg}");
    }

    public void LogWarning(string msg)
    {
        Log.Warning(msg);
        EnqueueLog($"WARN: {msg}");
    }

    public void LogError(string msg, Exception? ex = null)
    {
        Log.Error(ex, msg);
        EnqueueLog($"ERROR: {msg} - {ex?.Message}");
    }

    public void LogAction(ActionResult result)
    {
        var msg = $"Action {result.ActionPerformed} on {result.PackageId} via {result.SourceUsed}. Success: {result.Success}";
        LogInfo(msg);
    }

    public List<string> GetRecentLogs(int count = 100)
    {
        return _recentLogs.TakeLast(count).ToList();
    }

    private void EnqueueLog(string msg)
    {
        var formatted = $"[{DateTime.UtcNow:O}] {msg}";
        _recentLogs.Enqueue(formatted);
        while (_recentLogs.Count > MaxLogs)
        {
            _recentLogs.TryDequeue(out _);
        }
    }
}
