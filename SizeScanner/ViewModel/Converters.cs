using System;
using System.Globalization;
using System.Windows.Data;

namespace SizeScanner {
    public class BytesToDirSizeConverter : IValueConverter {
        static readonly string[] UnitsName = {"ЭБ", "ПБ", "ТБ", "ГБ", "МБ", "КБ", "Б"};

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value == null) {
                return string.Empty;
            }
            long length = (long) value;
            int unitIndex = 0;
            long b = 1L << 60;
            while (length < b && b > 1) {
                b = b >> 10;
                unitIndex++;
            }
            double dirSize = Math.Max(length / (double) b, 0);
            return $"{dirSize:0.##} {UnitsName[unitIndex]}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return 1;
        }
    }
}
