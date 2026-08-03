namespace AppFlow.Core.Interfaces;

using AppFlow.Core.Models;

public interface IActionHistoryStore
{
    Task LogActionAsync(ActionResult result, string packageName, CancellationToken ct = default);
}
