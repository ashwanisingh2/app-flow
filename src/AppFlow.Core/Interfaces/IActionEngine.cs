namespace AppFlow.Core.Interfaces;

using AppFlow.Core.Models;

public interface IActionEngine
{
    Task<ActionResult> ExecuteAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default);
    Task<ActionResult> ValidateAndExecuteAsync(PackageAction action, SourceQueryResult source, IProgress<string> progress, CancellationToken ct = default);
}
