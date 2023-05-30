using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImageConverterPlus
{
    public sealed class LogService
    {
        public string LogPath { get; }

        Channel<string> logBuffer;
        readonly string dateTimeFormat;
        readonly TimeZoneInfo timeZone;

        public LogService(string path, string dateTimeFormat, TimeZoneInfo timeZone, bool overwriteExistingFile)
        {
            if (overwriteExistingFile)
            {
                File.CreateText(path).Close();
            }
            
            this.LogPath = path;
            this.logBuffer = Channel.CreateUnbounded<string>();
            this.dateTimeFormat = dateTimeFormat;
            this.timeZone = timeZone;

            Log("Log Started");
            Log(timeZone.ToString());

            Task.Run(BufferConsumerLoop);
        }

        public void Log(Exception e) => Log(e.ToString());
        public async void Log(string str)
        {
            string logStr = $"{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).ToString(dateTimeFormat)}  {Environment.CurrentManagedThreadId,3}  {str}";
            await logBuffer.Writer.WriteAsync(logStr);
        }

        async Task BufferConsumerLoop()
        {
            ChannelReader<string> reader = logBuffer.Reader;
            while (await reader.WaitToReadAsync())
            {
                try
                {
                    while (reader.TryRead(out string? logEntry))
                    {
                        WriteLine(logEntry);
                    }
                }
                catch (Exception e)
                {
                    Log(e);
                    await Task.Delay(5000);
                }
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

        public void Close()
        {
            string logStr = $"{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).ToString(dateTimeFormat)}  {Environment.CurrentManagedThreadId,3}  ";
            WriteLine(logStr + "Log Closed");
            logBuffer.Writer.Complete();
        }
    }
}
