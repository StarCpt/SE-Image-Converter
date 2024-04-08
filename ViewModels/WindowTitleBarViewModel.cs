using CommunityToolkit.Mvvm.Input;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ImageConverterPlus.ViewModels
{
    public class WindowTitleBarViewModel : ReactiveObject
    {
        [Reactive]
        public bool IsMaximized { get; set; }
        [Reactive]
        public bool CanMaximize { get; set; }

        public ICommand LoadedCommand { get; }
        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand OpenLogsCommand { get; }
        public ICommand OpenAppDirectoryCommand { get; }

        private Window parentWindow = App.Current.MainWindow;

        public WindowTitleBarViewModel()
        {
            LoadedCommand = new RelayCommand<Window>(ExecuteLoaded!, win => win != null);
            MinimizeCommand = new RelayCommand(ExecuteMinimize);
            MaximizeCommand = new RelayCommand(ExecuteMaximize);
            RestoreCommand = new RelayCommand(ExecuteRestore);
            CloseCommand = new RelayCommand(ExecuteClose);
            OpenLogsCommand = new RelayCommand(ExecuteOpenLogs);
            OpenAppDirectoryCommand = new RelayCommand(ExecuteOpenAppDirectory);
        }

        private void ExecuteLoaded(Window win)
        {
            parentWindow = win;
            win.StateChanged += ParentWindow_StateChanged;
            CanMaximize = win.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

            DependencyPropertyDescriptor.FromProperty(Window.ResizeModeProperty, typeof(Window))
                .AddValueChanged(win, ParentWindow_ResizeModeChanged);
        }

        private void ParentWindow_ResizeModeChanged(object? sender, EventArgs e)
        {
            if (sender is Window win)
            {
                CanMaximize = win.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
            }
        }

        private void ParentWindow_StateChanged(object? sender, EventArgs e)
        {
            if (sender is Window win)
            {
                IsMaximized = win.WindowState == WindowState.Maximized;
            }
        }

        private void ExecuteMinimize()
        {
            parentWindow.WindowState = WindowState.Minimized;
        }

        private void ExecuteMaximize()
        {
            parentWindow.WindowState = WindowState.Maximized;
        }

        private void ExecuteRestore()
        {
            parentWindow.WindowState = WindowState.Normal;
        }

        private void ExecuteClose()
        {
            parentWindow.Close();
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
