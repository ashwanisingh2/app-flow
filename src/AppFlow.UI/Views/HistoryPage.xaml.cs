namespace AppFlow.UI.Views;

using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<HistoryViewModel>();
        DataContext = ViewModel;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) =>
        ViewModel.LoadCommand.Execute(null);
}
