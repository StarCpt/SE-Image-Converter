using ImageConverterPlus.Services;
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
        public const string AppVersion = "1.0 Beta2";
        public const string AppName = "SE Image Converter+";

        public static event RoutedPropertyChangedEventHandler<bool>? DebugStateChanged;

#pragma warning disable CS8618
        public static App Instance { get; private set; }
#pragma warning restore CS8618
        public LogService Log { get; }
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
            Instance = this;

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".log");
            TimeSpan logFlushInterval = TimeSpan.FromMilliseconds(500);
            string logDateTimeFormat = "yyyy-MM-dd H:mm:ss.fff";
            TimeZoneInfo timeZone = TimeZoneInfo.Local;
            bool overwriteExisting = true;

            Log = new LogService(logPath, logDateTimeFormat, timeZone, overwriteExisting);
            Log.Log($"Version {AppVersion}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Close();

            base.OnExit(e);
        }
    }
}
