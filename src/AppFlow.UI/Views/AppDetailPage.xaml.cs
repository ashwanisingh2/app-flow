namespace AppFlow.UI.Views;

using AppFlow.Core.Models;
using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

public sealed partial class AppDetailPage : Page
{
    public AppDetailViewModel ViewModel { get; }

    public AppDetailPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<AppDetailViewModel>();
        ViewModel.RequestConfirmationAsync = ConfirmRiskAsync;
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var request = e.Parameter switch
        {
            PackageNavigationRequest typed => typed,
            string packageId => new PackageNavigationRequest { PackageId = packageId },
            _ => null
        };
        if (request is not null)
            ViewModel.LoadPackageCommand.Execute(request);
    }

    private async Task<bool> ConfirmRiskAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Security warning",
            Content = message,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
