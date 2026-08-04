namespace AppFlow.UI.Views;

using AppFlow.Core.Models;
using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

public sealed partial class FavoritesPage : Page
{
    public FavoritesViewModel ViewModel { get; }

    public FavoritesPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<FavoritesViewModel>();
        DataContext = ViewModel;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) =>
        ViewModel.LoadCommand.Execute(null);

    private void GridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PackageInfo package) return;
        Frame.Navigate(typeof(AppDetailPage), new PackageNavigationRequest
        {
            PackageId = package.Id,
            SourceId = package.SourceId == "unavailable" ? null : package.SourceId
        });
    }
}
