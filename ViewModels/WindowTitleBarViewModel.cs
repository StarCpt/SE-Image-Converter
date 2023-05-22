using ImageConverterPlus.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ImageConverterPlus.ViewModels
{
    public class WindowTitleBarViewModel : NotifyPropertyChangedBase
    {
        public bool IsMaximized { get => isMaximized; set { SetValue(ref isMaximized, value); } }
        public bool CanMaximize { get => canMaximize; set => SetValue(ref canMaximize, value); }

        public ICommand LoadedCommand { get; }
        public ICommand MinimizeCommand { get; }
        public ICommand MaximizeCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand CloseCommand { get; }

        private Window parentWindow;
        private bool isMaximized = false;
        private bool canMaximize = false;

        public WindowTitleBarViewModel()
        {
            LoadedCommand = new ButtonCommand(ExecuteLoadedCommand);
            MinimizeCommand = new ButtonCommand(ExecuteMinimizeCommand);
            MaximizeCommand = new ButtonCommand(ExecuteMaximizeCommand);
            RestoreCommand = new ButtonCommand(ExecuteRestoreCommand);
            CloseCommand = new ButtonCommand(ExecuteCloseCommand);
        }

        private void ExecuteLoadedCommand(object? param)
        {
            if (param is Window win)
            {
                parentWindow = win;
                win.StateChanged += ParentWindow_StateChanged;
                CanMaximize = win.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;

                DependencyPropertyDescriptor.FromProperty(Window.ResizeModeProperty, typeof(Window))
                    .AddValueChanged(win, ParentWindow_ResizeModeChanged);
            }
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

        private void ExecuteMinimizeCommand(object? param)
        {
            parentWindow.WindowState = WindowState.Minimized;
        }

        private void ExecuteMaximizeCommand(object? param)
        {
            parentWindow.WindowState = WindowState.Maximized;
        }

        private void ExecuteRestoreCommand(object? param)
        {
            parentWindow.WindowState = WindowState.Normal;
        }

        private void ExecuteCloseCommand(object? param)
        {
            parentWindow.Close();
        }
    }
}
