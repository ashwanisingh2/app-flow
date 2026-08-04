namespace AppFlow.UI.Views;

using AppFlow.Core.Models;
using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

public sealed partial class SearchPage : Page
{
    public SearchViewModel ViewModel { get; }

    public SearchPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SearchViewModel>();
        DataContext = ViewModel;
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

    private void AutoSuggestBox_QuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args) =>
        ViewModel.SearchCommand.Execute(null);

    private void GridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PackageInfo package) return;
        Frame.Navigate(typeof(AppDetailPage), new PackageNavigationRequest
        {
            PackageId = package.Id,
            SourceId = package.SourceId
        });
    }
}
