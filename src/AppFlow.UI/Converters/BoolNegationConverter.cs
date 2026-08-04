namespace AppFlow.UI.Converters;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

public sealed class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not bool boolean) return value;
        var negated = !boolean;
        return targetType == typeof(Visibility)
            ? negated ? Visibility.Visible : Visibility.Collapsed
            : negated;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility)
            return visibility != Visibility.Visible;
        return value is bool boolean ? !boolean : value;
    }
}
