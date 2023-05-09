using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ImageConverterPlus
{
    public class Logging
    {
        private readonly Timer LogTimer;
        private readonly StringBuilder LogBuffer = new StringBuilder();

        public readonly string LogFilePath;
        private const string LoggingDateTimeFormat = "MM/dd/yyyy HH:mm:ss.fff";

        public Logging(string FilePath, string FileNameWithExtension, int WriteIntervalInMilliseconds, bool DeleteOldLog)
        {
            LogTimer = new Timer(WriteIntervalInMilliseconds);
            LogTimer.Elapsed += OnTimerElapsed;
            LogTimer.AutoReset = true;
            LogTimer.Enabled = true;
            LogFilePath = Path.Combine(FilePath, FileNameWithExtension);

            if (DeleteOldLog)
            {
                File.CreateText(LogFilePath);
            }

            Log("Started logging.");
        }

        /// <summary>
        /// Adds text to the buffer to be written later.
        /// </summary>
        /// <param name="text"></param>
        public void Log(string text)
        {
            LogBuffer.AppendLine(DateTime.Now.ToString(LoggingDateTimeFormat) + "    " + text);
        }

        private void OnTimerElapsed(object source, ElapsedEventArgs e) => WriteBufferToDiskAsync();

        public async Task WriteBufferToDiskAsync()
        {
            if (LogBuffer.Length > 0)
            {
                try
                {
                    using (StreamWriter file = new StreamWriter(LogFilePath, true, Encoding.UTF8))
                    {
                        await file.WriteAsync(LogBuffer.ToString());
                        file.Close();
                    }
                    LogBuffer.Clear();
                }
                catch (IOException e)
                {
                    Log(e.ToString());
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="editor">Editor to use to open the log file. Uses notepad by default.</param>
        public async Task OpenLogFileAsync(string editor = "notepad.exe")
        {
            await WriteBufferToDiskAsync();
            if (!File.Exists(LogFilePath))
            {
                File.CreateText(LogFilePath);
            }
            Process.Start(editor, LogFilePath);
        }
    }

}
