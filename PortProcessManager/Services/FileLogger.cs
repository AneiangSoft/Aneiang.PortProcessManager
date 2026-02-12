using System;
using System.IO;

namespace PortProcessManager.Services;

public static class FileLogger
{
    private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "app.log");
    private static readonly object Lock = new object();

    public static void Log(string level, string message, Exception? ex = null)
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            if (ex != null)
            {
                logEntry += Environment.NewLine + ex.ToString();
            }

            lock (Lock)
            {
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
        }
        catch
        {
            // 忽略日志写入本身的错误，避免程序崩溃
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null) => Log("ERROR", message, ex);
}
