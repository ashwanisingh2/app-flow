namespace AppFlow.UI.Controls;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

public sealed partial class SourceBadge : UserControl
{
    public static readonly DependencyProperty SourceIdProperty =
        DependencyProperty.Register("SourceId", typeof(string), typeof(SourceBadge), new PropertyMetadata(string.Empty, OnSourceIdChanged));

    public string SourceId
    {
        get => (string)GetValue(SourceIdProperty);
        set => SetValue(SourceIdProperty, value);
    }

    public SourceBadge()
    {
        this.InitializeComponent();
    }

    private static void OnSourceIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SourceBadge badge && e.NewValue is string sourceId)
        {
            badge.BadgeText.Text = sourceId.ToUpperInvariant();
            
            // Assign specific colors based on the source
            var color = sourceId.ToLowerInvariant() switch
            {
                "winget" => Windows.UI.Color.FromArgb(255, 0, 120, 215), // Blue
                "chocolatey" => Windows.UI.Color.FromArgb(255, 139, 69, 19), // SaddleBrown
                "scoop" => Windows.UI.Color.FromArgb(255, 155, 89, 182), // Purple
                "github" => Windows.UI.Color.FromArgb(255, 36, 41, 46), // Dark Gray
                "offline" => Windows.UI.Color.FromArgb(255, 128, 128, 128), // Gray
                _ => Windows.UI.Color.FromArgb(255, 100, 100, 100) // Default
            };
            
            badge.BadgeBorder.Background = new SolidColorBrush(color);
        }
    }
}
