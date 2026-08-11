using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Fx3Flasher.Core.Logging;

namespace Fx3Flasher.App.Converters
{
    /// <summary>Maps a log severity to a display brush.</summary>
    public sealed class SeverityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogSeverity severity)
            {
                switch (severity)
                {
                    case LogSeverity.Error:
                        return Brushes.Firebrick;
                    case LogSeverity.Warning:
                        return Brushes.DarkGoldenrod;
                    case LogSeverity.Success:
                        return Brushes.ForestGreen;
                    case LogSeverity.Debug:
                        return Brushes.Gray;
                    default:
                        return Brushes.Black;
                }
            }

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
