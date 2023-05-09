using ImageConverterPlus.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterPlus.ViewModels
{
    public class MainWindowViewModel : NotifyPropertyChangedBase
    {
        public bool InstantChanges { get => instantChanges; set => SetValue(ref instantChanges, value); }

        private bool instantChanges = true;
    }
}
