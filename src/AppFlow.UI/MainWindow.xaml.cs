namespace AppFlow.UI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using AppFlow.UI.Views;

/// <summary>
/// Main application window with NavigationView shell.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Set up title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Try to apply Mica backdrop (Win11), fallback to default
        TrySetMicaBackdrop();

        // Navigate to dashboard on startup
        ContentFrame.Navigate(typeof(DashboardPage));

        // Select the first nav item
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void TrySetMicaBackdrop()
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
        {
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            var pageType = tag switch
            {
                "dashboard" => typeof(DashboardPage),
                "search" => typeof(SearchPage),
                "favorites" => typeof(FavoritesPage),
                "history" => typeof(HistoryPage),
                "sources" => typeof(SourcesPage),
                "settings" => typeof(SettingsPage),
                _ => typeof(DashboardPage)
            };

            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType, null, new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
            }
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }

    private void GlobalSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.QueryText))
        {
            // Navigate to search page with query
            ContentFrame.Navigate(typeof(SearchPage), args.QueryText);

            // Select the search nav item
            NavView.SelectedItem = NavView.MenuItems[1];
        }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (Content is FrameworkElement rootElement)
        {
            if (rootElement.ActualTheme == ElementTheme.Dark)
            {
                rootElement.RequestedTheme = ElementTheme.Light;
                ThemeIcon.Glyph = "\uE793"; // Moon icon
            }
            else
            {
                rootElement.RequestedTheme = ElementTheme.Dark;
                ThemeIcon.Glyph = "\uE706"; // Sun icon
            }
        }
    }
}
