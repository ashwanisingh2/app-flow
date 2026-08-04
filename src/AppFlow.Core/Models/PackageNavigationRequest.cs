namespace AppFlow.Core.Models;

/// <summary>
/// Preserves the source selected on the search page while navigating to details.
/// </summary>
public sealed class PackageNavigationRequest
{
    public string PackageId { get; init; } = string.Empty;
    public string? SourceId { get; init; }
}
