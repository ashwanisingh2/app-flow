namespace AppFlow.UI.Views;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class AppDetailPage : Page
{
    public AppDetailViewModel ViewModel { get; }

    public AppDetailPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<AppDetailViewModel>();
        this.DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string packageId)
        {
            ViewModel.LoadPackageCommand.Execute(packageId);
        }
    }
}
