using CommunityToolkit.Mvvm.DependencyInjection;
using ImageConverterPlus.Services;
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

        public static event RoutedPropertyChangedEventHandler<bool>? DebugStateChanged;

        public static LogService Log => Ioc.Default.GetRequiredService<LogService>();
        public bool Debug
        {
            get => debug;
            set
            {
                if (debug != value)
                {
                    debug = value;
                    DebugStateChanged?.Invoke(this, new RoutedPropertyChangedEventArgs<bool>(!value, value));
                }
            }
        }

        private bool debug = false;

        public App()
        {
            ConfigureServices();
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

            container.AddSingleton<MainWindowViewModel>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Close();

            base.OnExit(e);
        }
    }
}
