namespace AppFlow.UI.Views;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using AppFlow.Core.Models;

public sealed partial class SearchPage : Page
{
    public SearchViewModel ViewModel { get; }

    public SearchPage()
    {
        this.InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SearchViewModel>();
        this.DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string query && !string.IsNullOrWhiteSpace(query))
        {
            ViewModel.SearchQuery = query;
            ViewModel.SearchCommand.Execute(null);
        }
    }

    private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.SearchCommand.Execute(null);
    }

    private void GridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PackageInfo pkg)
        {
            Frame.Navigate(typeof(AppDetailPage), pkg.Id);
        }
    }
}
