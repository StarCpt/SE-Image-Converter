using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImageConverterPlus
{
    public sealed class LogService : IDisposable
    {
        public string LogPath { get; }

        ConcurrentQueue<string> logBuffer = new ConcurrentQueue<string>();
        System.Threading.Timer flushTimer;
        readonly string dateTimeFormat;
        readonly TimeZoneInfo timeZone;

        public LogService(string path, TimeSpan flushInterval, string dateTimeFormat, TimeZoneInfo timeZone, bool overwriteExistingFile)
        {
            if (overwriteExistingFile)
            {
                File.CreateText(path).Close();
            }

            this.LogPath = path;
            this.flushTimer = new System.Threading.Timer(FlushLogBuffer, null, flushInterval, flushInterval);
            this.dateTimeFormat = dateTimeFormat;
            this.timeZone = timeZone;

            Log("Log Started");
            Log(timeZone.ToString());
        }

        public void Log(string str)
        {
            string logStr = $"{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).ToString(dateTimeFormat)}  {Environment.CurrentManagedThreadId,3}  {str}";
            logBuffer.Enqueue(logStr);
        }
        public void Log(Exception e) => Log(e.ToString());

        void FlushLogBuffer(object? state)
        {
            try
            {
                while (logBuffer.TryDequeue(out string? result))
                {
                    WriteLine(result);
                }
            }
            catch (Exception e)
            {
                Log(e);
            }
        }

        void WriteLine(string message)
        {
            using (StreamWriter sw = new StreamWriter(LogPath, true, Encoding.UTF8))
            {
                sw.WriteLine(message);
                sw.Close();
            }
        }

        public void OpenLogFile()
        {
            if (!File.Exists(LogPath))
                File.CreateText(LogPath);

            Process.Start("notepad.exe", LogPath);
        }

        public void Dispose()
        {
            flushTimer.Dispose();
        }
    }
}
