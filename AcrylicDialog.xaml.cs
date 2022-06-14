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
using System.Windows.Shapes;
using System.Threading;

namespace SEImageToLCD_15BitColor
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class AcrylicDialog : Window
    {
        public AcrylicDialog(Window parent, string message)
        {
            InitializeComponent();
            AcrylicDialogWindow.Owner = parent;
            DialogMessage.Content = message;
            MainWindow.Logging.Log($"AcrylicDialog: {message}");
        }

        private void DialogCloseBtn_Click(object sender, RoutedEventArgs e)
        {
            AcrylicDialogWindow.Close();
            AcrylicDialogWindow.Owner.Focus();
        }
    }
}
