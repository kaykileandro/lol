﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace LeagueSandbox.GameServer.Core.Logic
{
    public class Logger
    {
        private LogWriter _logWriter;
        private const string _logName = "LeagueSandbox.txt";

        public Logger(ServerContext serverContext)
        {
            var directory = serverContext.ExecutingDirectory;
            _logWriter = new LogWriter(directory, _logName);

            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }
        
        public void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is InvalidCastException || e.Exception is System.Collections.Generic.KeyNotFoundException)
                return;
            _logWriter.Log("A first chance exception was thrown", "EXCEPTION");
            _logWriter.Log(e.Exception.Message, "EXCEPTION");
            _logWriter.Log(e.ToString(), "EXCEPTION");
            _logWriter.Log(e.Exception.StackTrace, "EXCEPTION");
        }

        public void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs x)
        {
            _logWriter.Log("An unhandled exception was thrown", "UNHANDLEDEXCEPTION");
            var ex = (Exception)x.ExceptionObject;
            _logWriter.Log(ex.Message, "UNHANDLEDEXCEPTION");
            _logWriter.Log(ex.ToString(), "UNHANDLEDEXCEPTION");
            _logWriter.Log(ex.StackTrace, "UNHANDLEDEXCEPTION");
        }

        public void Log(string line, string type = "LOG")
        {
            _logWriter.Log(line, type);
        }

        public void LogCoreInfo(string line)
        {
            Log(line, "CORE_INFO");
        }

        public void LogCoreInfo(string format, params object[] args)
        {
            LogCoreInfo(string.Format(format, args));
        }

        public void LogCoreWarning(string line)
        {
            Log(line, "CORE_WARNING");
        }

        public void LogCoreWarning(string format, params object[] args)
        {
            LogCoreWarning(string.Format(format, args));
        }

        public void LogCoreError(string line)
        {
            Log(line, "CORE_ERROR");
        }

        public void LogCoreError(string format, params object[] args)
        {
            LogCoreError(string.Format(format, args));
        }

        public void Flush()
        {
            _logWriter.Flush();
        }

        public void LogFatalError(string line)
        {
            Log(line, "FATAL_ERROR");
            Flush();
        }

        public void LogFatalError(string format, params object[] args)
        {
            LogFatalError(string.Format(format, args));
        }

        private class LogWriter : IDisposable
        {
            public string _logFileName;
            private FileStream _logFile;
            private StringBuilder _stringBuilder;
            private const double REFRESH_RATE = 1000.0 / 10.0; //10fps
            private System.Timers.Timer _refreshTimer;

            public LogWriter(string executingDirectory, string logFileName)
            {
                CreateLogFile(executingDirectory, logFileName);

                _stringBuilder = new StringBuilder();
                //Start refresh loop
                _refreshTimer = new System.Timers.Timer(REFRESH_RATE)
                {
                    AutoReset = true,
                    Enabled = true
                };
                _refreshTimer.Elapsed += (_, _2) => RefreshLoop();
                _refreshTimer.Start();
            }

            public void Flush()
            {
                RefreshLoop();
            }

            //Can get called by different threads
            private void RefreshLoop()
            {
                string text = null;
                lock (_stringBuilder)
                {
                    if (_stringBuilder.Length > 0)
                    {
                        text = _stringBuilder.ToString();
                        _stringBuilder.Clear();
                    }
                }
                if (text != null)
                {
                    WriteTextToLogFile(text + Environment.NewLine);
                    Console.Write(text);
                }
            }

            //Can get called by different threads
            private void WriteTextToLogFile(string text)
            {
                byte[] info = new UTF8Encoding(true).GetBytes(text);
                lock (_logFile)
                {
                    _logFile.WriteAsync(info, 0, info.Length);
                }
            }

            public void Log(string lines, string type = "LOG")
            {
                var text = string.Format(
                    "({0}) [{1}]: {2}",
                    DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss.fff tt"),
                    type.ToUpper(),
                    lines
                );
                lock (_stringBuilder)
                {
                    _stringBuilder.AppendLine(text);
                }
            }

            public void CreateLogFile(string directory, string name)
            {
                if (!string.IsNullOrEmpty(_logFileName))
                {
                    return;
                }

                var path = Path.Combine(directory, "Logs");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var logName = string.Format(
                    "{0}-{1}",
                    DateTime.Now.ToString("yyyyMMdd-HHMM"),
                    name
                );
                _logFileName = Path.Combine(path, logName);

                _logFile = File.Create(_logFileName);
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls
            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        Flush();
                        _logFile.Flush();
                        _logFile.Close();
                    }
                    disposedValue = true;
                }
            }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                Dispose(true);
            }
            #endregion
        }
    }
}
