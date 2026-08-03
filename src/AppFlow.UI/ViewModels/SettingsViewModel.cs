namespace AppFlow.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _selectedTheme = "System";

    [ObservableProperty]
    private bool _enableSecurityValidation = true;

    [ObservableProperty]
    private bool _enableSourceLocking = true;

    [RelayCommand]
    public void SaveSettings()
    {
        // Save logic to SQLite Settings table would go here
    }

    [RelayCommand]
    public async Task ExportPackageListAsync()
    {
        // Export logic
        await Task.Delay(100);
    }
}
