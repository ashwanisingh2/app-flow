namespace AppFlow.Core.Services;

using AppFlow.Core.Enums;
using AppFlow.Core.Interfaces;
using AppFlow.Core.Models;

public class ActionEngine : IActionEngine
{
    private readonly IEnumerable<ISourceAdapter> _adapters;
    private readonly ISecurityValidator _validator;

    public ActionEngine(IEnumerable<ISourceAdapter> adapters, ISecurityValidator validator)
    {
        _adapters = adapters;
        _validator = validator;
    }

    public async Task<ActionResult> ExecuteAsync(PackageAction action, IProgress<string> progress, CancellationToken ct = default)
    {
        var adapter = _adapters.FirstOrDefault(a => a.SourceId == action.SourceId);
        if (adapter == null)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = $"Adapter {action.SourceId} not found.",
                ActionPerformed = action.Type,
                PackageId = action.PackageId,
                SourceUsed = action.SourceId
            };
        }

        try
        {
            return action.Type switch
            {
                ActionType.Install => await adapter.InstallAsync(action, progress, ct),
                ActionType.Update => await adapter.UpdateAsync(action, progress, ct),
                ActionType.Uninstall => await adapter.UninstallAsync(action, progress, ct),
                ActionType.Repair => await adapter.RepairAsync(action, progress, ct),
                _ => new ActionResult { Success = false, ErrorMessage = "Unknown action", ActionPerformed = action.Type, PackageId = action.PackageId, SourceUsed = action.SourceId }
            };
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ErrorSuggestion = "Installation failed. Try running as administrator or check your internet connection.",
                ActionPerformed = action.Type,
                PackageId = action.PackageId,
                SourceUsed = action.SourceId
            };
        }
    }

    public async Task<ActionResult> ValidateAndExecuteAsync(PackageAction action, SourceQueryResult source, IProgress<string> progress, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(action, source);

        if (validation.BlockInstall)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = "Installation blocked by security policy.",
                ErrorSuggestion = string.Join(" ", validation.Errors),
                ActionPerformed = action.Type,
                PackageId = action.PackageId,
                SourceUsed = action.SourceId
            };
        }

        if (validation.RequiresUserConfirmation)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = "User confirmation required.",
                ErrorSuggestion = validation.ConfirmationMessage,
                ActionPerformed = action.Type,
                PackageId = action.PackageId,
                SourceUsed = action.SourceId
            };
        }

        return await ExecuteAsync(action, progress, ct);
    }
}
