using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace MainApplication.View
{
    public class BedNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if((int)value>0)
            {
                return "床位号: "+value;
            }
            return "床位号:";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class SingleMonitor : UserControl
    {
        public SingleMonitor()
        {
            InitializeComponent();
        }
    }
}
