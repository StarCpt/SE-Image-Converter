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
using ImageConverterPlus.Data.Interfaces;
using System.Reactive.Linq;
using ReactiveUI;

namespace ImageConverterPlus.Views
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class AcrylicDialog : Window
    {
        public AcrylicDialog(Window parent, IDialog dataContext)
        {
            this.DataContext = dataContext;

            InitializeComponent();

            this.Owner = parent;
            App.Log.Log($"AcrylicDialog Shown");

            Observable.FromAsync(() => dataContext.ResultTask)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Finally(() =>
                {
                    this.Close();
                    this.Owner.Focus();
                }).Subscribe();
        }
    }
}
