using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImageConverterPlus.Services
{
    public sealed class LogService
    {
        public string LogPath { get; }

        private readonly Channel<string> _logBuffer;
        private readonly string _dateTimeFormat;
        private readonly TimeZoneInfo _timeZone;
        private readonly Task _bufferConsumeTask;

        public LogService(string path, string dateTimeFormat, TimeZoneInfo timeZone, bool overwriteExistingFile)
        {
            LogPath = path;
            _logBuffer = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true,
            });
            _dateTimeFormat = dateTimeFormat;
            _timeZone = timeZone;

            if (Path.GetDirectoryName(LogPath) is not string dir || string.IsNullOrWhiteSpace(dir))
            {
                throw new ArgumentException(null, nameof(path));
            }
            Directory.CreateDirectory(dir);
            _bufferConsumeTask = ConsumeBuffer(overwriteExistingFile ? File.CreateText(LogPath) : File.AppendText(LogPath));

            Log("Log Started");
            Log(timeZone.ToString());
            Log($"Version {App.AppVersion}");
        }

        public T Log<T>(T e, bool dialog = false) where T : Exception
        {
            Log(e.ToString(), dialog);
            return e;
        }

        public async void Log(string str, bool dialog = false)
        {
            string logStr = FormLogString(str);
            await _logBuffer.Writer.WriteAsync(logStr);
        }

        private string FormLogString(string str)
        {
            return $"{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone).ToString(_dateTimeFormat)}   Thread {Environment.CurrentManagedThreadId,3}  {str}";
        }

        async Task ConsumeBuffer(StreamWriter writer)
        {
            ChannelReader<string> reader = _logBuffer.Reader;
            await foreach (string entry in reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    Console.WriteLine(entry);
                    await writer.WriteLineAsync(entry).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log(e);
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }
            writer.Flush();
            writer.Dispose();
        }

        public void OpenLogFile()
        {
            if (!File.Exists(LogPath))
                File.CreateText(LogPath);

            Process.Start("notepad.exe", LogPath);
        }

        public void Close()
        {
            Log("Log Closed");
            _logBuffer.Writer.Complete();
            _bufferConsumeTask.Wait();
        }
    }
}
