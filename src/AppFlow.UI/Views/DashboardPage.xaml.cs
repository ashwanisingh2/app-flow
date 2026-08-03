namespace AppFlow.UI.Views;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
        this.DataContext = ViewModel;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadDataCommand.Execute(null);
    }
}
