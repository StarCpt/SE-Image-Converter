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

namespace ImageConverterPlus.Views
{
    /// <summary>
    /// Interaction logic for WindowTitleBar.xaml
    /// </summary>
    public partial class WindowTitleBar : UserControl
    {
        public WindowTitleBar()
        {
            InitializeComponent();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Window win)
            {
                win.WindowState = WindowState.Minimized;
            }
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Window win)
            {
                win.WindowState = win.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Window win)
            {
                win.Close();
            }
        }
    }
}
