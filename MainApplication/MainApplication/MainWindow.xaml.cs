using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MainApplication.ViewModel;
using MainApplication.View;

namespace MainApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Closing += (a, b) => ViewModelLocator.Cleanup();
        }

        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            MonitorWindow monitor = new MonitorWindow();
            monitor.Owner = this;
            monitor.Show();
        }
    }
}
