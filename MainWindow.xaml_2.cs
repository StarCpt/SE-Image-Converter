using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ImageConverterPlus
{
    public partial class MainWindow : Window
    {
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "InstantChanges":
                    InstantChangesPropertyChanged(sender, e);
                    break;
            }
        }

        private void InstantChangesPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Logging.Log($"Instant Changes {(viewModel.InstantChanges ? "en" : "dis")}abled");

            ConvertBtn.IsEnabled = (!viewModel.InstantChanges && ImageCache.Image != null);
            if (!ConvertBtn.IsEnabled)
            {
                UpdateCurrentConvertBtnToolTip("No images loaded", true);
            }

            RemoveImagePreviewBtn.IsEnabled = !viewModel.InstantChanges;

            //UpdatePreviewDelayed(false, 0);
            ApplyInstantChanges(false, 0);
        }
    }
}
