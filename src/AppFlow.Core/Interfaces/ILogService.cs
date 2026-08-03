namespace AppFlow.Core.Interfaces;

using AppFlow.Core.Models;

public interface ILogService
{
    void LogInfo(string msg);
    void LogWarning(string msg);
    void LogError(string msg, Exception? ex = null);
    void LogAction(ActionResult result);
    List<string> GetRecentLogs(int count = 100);
}
