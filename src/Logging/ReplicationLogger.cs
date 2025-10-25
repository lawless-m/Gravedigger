using System;
using System.IO;

namespace Gravedigger.Logging
{
    /// <summary>
    /// Logger for replication operations
    /// </summary>
    public class ReplicationLogger
    {
        private readonly string _logPath;
        private readonly LogLevel _logLevel;
        private readonly string _logFilePath;

        public enum LogLevel
        {
            Debug = 0,
            Information = 1,
            Warning = 2,
            Error = 3,
            Critical = 4
        }

        public ReplicationLogger(string logPath, string logLevel = "Information")
        {
            _logPath = logPath;
            _logLevel = ParseLogLevel(logLevel);

            // Create log directory if it doesn't exist
            if (!Directory.Exists(_logPath))
            {
                Directory.CreateDirectory(_logPath);
            }

            // Create log file with timestamp
            string fileName = $"replication_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            _logFilePath = Path.Combine(_logPath, fileName);

            // Write header
            LogInformation($"=== Gravedigger Replication Session Started ===");
            LogInformation($"Log Level: {_logLevel}");
            LogInformation($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogInformation($"Machine: {Environment.MachineName}");
            LogInformation($"User: {Environment.UserName}");
            LogInformation("===================================================");
        }

        private LogLevel ParseLogLevel(string level)
        {
            if (Enum.TryParse<LogLevel>(level, true, out var result))
                return result;
            return LogLevel.Information;
        }

        private void Log(LogLevel level, string message, Exception ex = null)
        {
            if (level < _logLevel)
                return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] [{level.ToString().ToUpper()}] {message}";

            // Write to file
            try
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);

                if (ex != null)
                {
                    File.AppendAllText(_logFilePath, $"Exception: {ex.Message}{Environment.NewLine}");
                    File.AppendAllText(_logFilePath, $"Stack Trace: {ex.StackTrace}{Environment.NewLine}");
                }
            }
            catch
            {
                // If we can't write to log file, write to console
                Console.WriteLine($"LOGGING ERROR: Could not write to log file: {_logFilePath}");
            }

            // Also write to console for immediate feedback
            var originalColor = Console.ForegroundColor;
            switch (level)
            {
                case LogLevel.Debug:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogLevel.Information:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }

            Console.WriteLine(logEntry);
            if (ex != null)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                if (level >= LogLevel.Error)
                {
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                }
            }

            Console.ForegroundColor = originalColor;
        }

        public void LogDebug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public void LogInformation(string message)
        {
            Log(LogLevel.Information, message);
        }

        public void LogWarning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public void LogError(string message, Exception ex = null)
        {
            Log(LogLevel.Error, message, ex);
        }

        public void LogCritical(string message, Exception ex = null)
        {
            Log(LogLevel.Critical, message, ex);
        }

        public void LogSessionEnd(bool success, string summary)
        {
            LogInformation("===================================================");
            LogInformation($"Session Status: {(success ? "SUCCESS" : "FAILURE")}");
            LogInformation($"Summary: {summary}");
            LogInformation($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogInformation("=== Gravedigger Replication Session Ended ===");
        }

        /// <summary>
        /// Cleans up old log files based on retention policy
        /// </summary>
        public void CleanupOldLogs(int retentionDays)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var logFiles = Directory.GetFiles(_logPath, "replication_*.log");

                int deletedCount = 0;
                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(logFile);
                        deletedCount++;
                    }
                }

                if (deletedCount > 0)
                {
                    LogInformation($"Cleaned up {deletedCount} old log file(s)");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to cleanup old logs: {ex.Message}");
            }
        }

        public string GetLogFilePath()
        {
            return _logFilePath;
        }
    }
}
