using CommunityToolkit.Mvvm.DependencyInjection;
using ImageConverterPlus.Data.Interfaces;
using ImageConverterPlus.Services;
using ImageConverterPlus.Services.interfaces;
using ImageConverterPlus.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ImageConverterPlus
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string AppVersion { get; } = "1.0 Beta2";
        public static string AppName { get; } = "SE Image Converter+";

        public static LogService Log => Ioc.Default.GetRequiredService<LogService>();

        public App()
        {
            ConfigureServices();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            MainWindow = new MainWindow
            {
                DataContext = Ioc.Default.GetRequiredService<MainWindowViewModel>(),
            };

            MainWindow.Show();
        }

        private static void ConfigureServices()
        {
            ServiceCollection services = new ServiceCollection();
            AddServices(services);
            Ioc.Default.ConfigureServices(services.BuildServiceProvider());
        }

        private static void AddServices(IServiceCollection container)
        {
            container.AddSingleton<LogService>(
                provider => new LogService(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".log"),
                    "yyyy-MM-dd H:mm:ss.fff",
                    TimeZoneInfo.Local,
                    true));
            container.AddSingleton<ConvertManagerService>();
            container.AddSingleton<IDialogService, AcrylicDialogService>();

            container.AddSingleton<MainWindowViewModel>();
            container.AddSingleton<WindowTitleBarViewModel>();

            container.AddTransient<IDialogPresenter, MainWindow>(
                provider => (MainWindow)App.Current.MainWindow);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Close();

            base.OnExit(e);
        }
    }
}
