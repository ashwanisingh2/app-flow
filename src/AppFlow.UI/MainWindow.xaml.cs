namespace AppFlow.UI;

using AppFlow.Core.Models;
using AppFlow.Database.Repositories;
using AppFlow.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

/// <summary>Main application window with NavigationView shell.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        TrySetMicaBackdrop();
        ContentFrame.Navigate(typeof(DashboardPage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    public void ApplyTheme(string theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        var dark = RootGrid.ActualTheme == ElementTheme.Dark
                   || RootGrid.RequestedTheme == ElementTheme.Dark;
        ThemeToggle.IsChecked = dark;
        ThemeIcon.Glyph = dark ? "\uE706" : "\uE793";
    }

    public async Task ShowStartupErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "AppFlow startup problem",
            Content = message,
            CloseButtonText = "Close"
        };
        await dialog.ShowAsync();
    }

    private void TrySetMicaBackdrop()
    {
        if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem item) return;

        var pageType = item.Tag?.ToString() switch
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
            ContentFrame.Navigate(
                pageType,
                null,
                new Microsoft.UI.Xaml.Media.Animation.EntranceNavigationTransitionInfo());
        }
    }

    private void NavView_BackRequested(
        NavigationView sender,
        NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack) ContentFrame.GoBack();
    }

    private void GlobalSearchBox_QuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.QueryText)) return;
        ContentFrame.Navigate(typeof(SearchPage), args.QueryText.Trim());
        NavView.SelectedItem = NavView.MenuItems[1];
    }

    private async void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var nextTheme = RootGrid.ActualTheme == ElementTheme.Dark ? "Light" : "Dark";
        ApplyTheme(nextTheme);

        var settings = App.Current.Services.GetRequiredService<AppSettings>();
        settings.Theme = nextTheme;
        try
        {
            await App.Current.Services.GetRequiredService<SettingsRepository>()
                .SaveAppSettingsAsync(settings);
        }
        catch (Exception)
        {
            // Theme remains applied for this session if persistence is unavailable.
        }
    }
}
