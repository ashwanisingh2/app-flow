namespace AppFlow.UI.Controls;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

public sealed partial class ActionPanel : UserControl
{
    public static readonly DependencyProperty IsInstalledProperty = DependencyProperty.Register("IsInstalled", typeof(bool), typeof(ActionPanel), new PropertyMetadata(false));
    public static readonly DependencyProperty HasUpdateProperty = DependencyProperty.Register("HasUpdate", typeof(bool), typeof(ActionPanel), new PropertyMetadata(false));
    public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register("IsExecuting", typeof(bool), typeof(ActionPanel), new PropertyMetadata(false));

    public bool IsInstalled
    {
        get => (bool)GetValue(IsInstalledProperty);
        set => SetValue(IsInstalledProperty, value);
    }
    public bool HasUpdate
    {
        get => (bool)GetValue(HasUpdateProperty);
        set => SetValue(HasUpdateProperty, value);
    }
    public bool IsExecuting
    {
        get => (bool)GetValue(IsExecutingProperty);
        set => SetValue(IsExecutingProperty, value);
    }

    public event RoutedEventHandler InstallClicked;
    public event RoutedEventHandler UpdateClicked;
    public event RoutedEventHandler UninstallClicked;

    public ActionPanel()
    {
        this.InitializeComponent();
    }

    private void InstallBtn_Click(object sender, RoutedEventArgs e) => InstallClicked?.Invoke(this, e);
    private void UpdateBtn_Click(object sender, RoutedEventArgs e) => UpdateClicked?.Invoke(this, e);
    private void UninstallBtn_Click(object sender, RoutedEventArgs e) => UninstallClicked?.Invoke(this, e);
}
