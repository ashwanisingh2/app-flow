namespace AppFlow.UI.Views;

using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        DataContext = ViewModel;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) =>
        ViewModel.LoadSettingsCommand.Execute(null);
}
