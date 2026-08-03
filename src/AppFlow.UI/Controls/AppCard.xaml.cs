namespace AppFlow.UI.Controls;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

public sealed partial class AppCard : UserControl
{
    public static readonly DependencyProperty PackageNameProperty = DependencyProperty.Register("PackageName", typeof(string), typeof(AppCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty PublisherProperty = DependencyProperty.Register("Publisher", typeof(string), typeof(AppCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty VersionProperty = DependencyProperty.Register("Version", typeof(string), typeof(AppCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SourceIdProperty = DependencyProperty.Register("SourceId", typeof(string), typeof(AppCard), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsInstalledProperty = DependencyProperty.Register("IsInstalled", typeof(bool), typeof(AppCard), new PropertyMetadata(false));
    public static readonly DependencyProperty HasUpdateProperty = DependencyProperty.Register("HasUpdate", typeof(bool), typeof(AppCard), new PropertyMetadata(false));

    public string PackageName
    {
        get => (string)GetValue(PackageNameProperty);
        set => SetValue(PackageNameProperty, value);
    }
    public string Publisher
    {
        get => (string)GetValue(PublisherProperty);
        set => SetValue(PublisherProperty, value);
    }
    public string Version
    {
        get => (string)GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }
    public string SourceId
    {
        get => (string)GetValue(SourceIdProperty);
        set => SetValue(SourceIdProperty, value);
    }
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

    public AppCard()
    {
        this.InitializeComponent();
    }
}
