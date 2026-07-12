using System.Globalization;
using System.Windows.Data;

namespace 点胶机.Converters;

/// <summary>bool → 文本(默认:True→"已回零",False→"未回零")</summary>
public class BoolToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "已回零";
    public string FalseText { get; set; } = "未回零";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        return b ? TrueText : FalseText;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>bool → 颜色(True 绿,False 灰,用于指示灯)</summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        return b ? System.Windows.Media.Brushes.LawnGreen : System.Windows.Media.Brushes.DimGray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
