using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImageConverterPlus.ViewModels
{
    public class WindowTitleBarViewModel : ReactiveObject
    {
        public ICommand OpenLogsCommand { get; }
        public ICommand OpenAppDirectoryCommand { get; }

        public WindowTitleBarViewModel()
        {
            OpenLogsCommand = new RelayCommand(ExecuteOpenLogs);
            OpenAppDirectoryCommand = new RelayCommand(ExecuteOpenAppDirectory);
        }

        private void ExecuteOpenLogs()
        {
            App.Log.OpenLogFile();
        }

        private void ExecuteOpenAppDirectory()
        {
            Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
