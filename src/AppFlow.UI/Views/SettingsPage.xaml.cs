namespace AppFlow.UI.Views;

using Microsoft.UI.Xaml.Controls;
using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        this.DataContext = ViewModel;
    }
}
