namespace AppFlow.Core.Interfaces;

using AppFlow.Core.Models;

public interface ISecurityValidator
{
    Task<ValidationResult> ValidateAsync(PackageAction action, SourceQueryResult source);
}
