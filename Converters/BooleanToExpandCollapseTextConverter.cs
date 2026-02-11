using System;
using System.Globalization;
using System.Windows.Data;

namespace PortProcessManager.Converters;

public class BooleanToExpandCollapseTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var expanded = value is bool b && b;
        return expanded ? "全部折叠" : "全部展开";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
