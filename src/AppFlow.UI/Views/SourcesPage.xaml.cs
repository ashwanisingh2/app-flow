namespace AppFlow.UI.Views;

using AppFlow.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

public sealed partial class SourcesPage : Page
{
    public SourcesViewModel ViewModel { get; }

    public SourcesPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<SourcesViewModel>();
        DataContext = ViewModel;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) =>
        ViewModel.LoadCommand.Execute(null);

    private void SourceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch { DataContext: SourceItemViewModel source })
            ViewModel.UpdateSourceCommand.Execute(source);
    }
}
