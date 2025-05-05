using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace RoesleinAddIn
{
    /// <summary>
    /// Logger class for SolidWorks add-in
    /// </summary>
    public class Logger
    {
        private static readonly object lockObj = new object();
        private static Logger _instance;
        private string logFilePath;
        private bool isEnabled;
        private bool settingsEnabled = true; // Reflects setting from Settings.cs
        private bool debugEnabled = false; // Debug logging setting
        private int maxLogFileSizeBytes = 5242880; // Default 5MB

        private static readonly string DefaultDebugLogPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "RoesleinAddIn_Debug.log");
            
        private string debugLogPath = DefaultDebugLogPath;

        /// <summary>
        /// Gets the singleton instance of the Logger
        /// </summary>
        public static Logger Instance 
        {
            get 
            {
                if (_instance == null)
                {
                    string defaultPath = Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                        "RoesleinAddIn.log");
                    
                    _instance = new Logger(defaultPath);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Constructor with log file path
        /// </summary>
        /// <param name="logFilePath">Path to the log file</param>
        public Logger(string logFilePath)
        {
            this.logFilePath = logFilePath;
            this.isEnabled = true;
            
            // Create directory if it doesn't exist
            string directory = Path.GetDirectoryName(logFilePath);
            if (!Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    // If we can't create the directory, disable logging
                    this.isEnabled = false;
                    // Try to log to the Windows Application Event Log as a fallback
                    try
                    {
                        EventLog.WriteEntry("RoesleinAddIn", 
                            $"Failed to create log directory: {ex.Message}", 
                            EventLogEntryType.Error);
                    }
                    catch
                    {
                        // Silent fail - we've done our best
                    }
                }
            }
        }

        /// <summary>
        /// Log an informational message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public void LogInfo(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Log("INFO", message, callerName, Path.GetFileName(filePath), lineNumber);
        }

        /// <summary>
        /// Log a warning message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public void LogWarning(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Log("WARNING", message, callerName, Path.GetFileName(filePath), lineNumber);
        }

        /// <summary>
        /// Log an error message
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public void LogError(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Log("ERROR", message, callerName, Path.GetFileName(filePath), lineNumber);
        }

        /// <summary>
        /// Log an error message with exception details
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="ex">Exception object</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public void LogError(string message, Exception ex,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            StringBuilder sb = new StringBuilder(message);
            sb.AppendLine();
            sb.AppendLine($"Exception: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"Stack Trace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                sb.AppendLine("Inner Exception:");
                sb.AppendLine($"  Type: {ex.InnerException.GetType().Name}");
                sb.AppendLine($"  Message: {ex.InnerException.Message}");
                sb.AppendLine($"  Stack Trace: {ex.InnerException.StackTrace}");
            }
            
            Log("ERROR", sb.ToString(), callerName, Path.GetFileName(filePath), lineNumber);
        }

        /// <summary>
        /// Log a debug message (only if debug logging is enabled)
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public void LogDebug(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            // Only log if debug logging is enabled
            if (!debugEnabled) return;
            
            try
            {
                lock (lockObj)
                {
                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(debugLogPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Rotate debug log file if needed
                    RotateDebugLogFileIfNeeded();
                    
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    string logEntry = $"[{timestamp}] [DEBUG] [{Path.GetFileName(filePath)}:{lineNumber}] [{callerName}] {message}";
                    
                    // Append to debug log file
                    using (StreamWriter writer = new StreamWriter(debugLogPath, true, Encoding.UTF8))
                    {
                        writer.WriteLine(logEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                // Only output to debug console if logging fails - no need to disable main logger
                Debug.WriteLine($"[{DateTime.Now:u}] !!! DEBUG LOGGER FAILED !!! {ex.GetType().Name} - {ex.Message}");
            }
        }

        /// <summary>
        /// Static method to log info message using the singleton instance
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public static void Info(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.LogInfo(message, callerName, filePath, lineNumber);
        }

        /// <summary>
        /// Static method to log warning message using the singleton instance
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public static void Warning(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.LogWarning(message, callerName, filePath, lineNumber);
        }

        /// <summary>
        /// Static method to log error message using the singleton instance
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public static void Error(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.LogError(message, callerName, filePath, lineNumber);
        }

        /// <summary>
        /// Static method to log error with exception using the singleton instance
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="ex">Exception object</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public static void Error(string message, Exception ex,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.LogError(message, ex, callerName, filePath, lineNumber);
        }

        /// <summary>
        /// Static method to log debug message using the singleton instance
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="callerName">Name of the calling method (auto-filled)</param>
        /// <param name="filePath">Source file path (auto-filled)</param>
        /// <param name="lineNumber">Line number (auto-filled)</param>
        public static void DebugLog(string message,
            [CallerMemberName] string callerName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            Instance.LogDebug(message, callerName, filePath, lineNumber);
        }

        private void Log(string level, string message, string callerName, string fileName, int lineNumber)
        {
            // Check both internal enabled flag AND settings flag
            if (!isEnabled || !settingsEnabled) return;

            try
            {
                lock (lockObj)
                {
                    RotateLogFileIfNeeded();
                    
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    string logEntry = $"[{timestamp}] [{level}] [{fileName}:{lineNumber}] [{callerName}] {message}";
                    
                    // Append to log file
                    using (StreamWriter writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
                    {
                        writer.WriteLine(logEntry);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log failure robustly - Try Debug output ONLY
                string errorMsg = $"[{DateTime.Now:u}] !!! LOGGER FAILED TO WRITE TO FILE !!! {ex.GetType().Name} - {ex.Message}\nStackTrace: {ex.StackTrace}\n";
                Debug.WriteLine(errorMsg); // Output to debugger

                // Prevent further file write attempts in this session
                isEnabled = false; 
            }
        }

        /// <summary>
        /// Rotate log file if it exceeds the maximum size
        /// </summary>
        /// <param name="maxSizeBytes">Maximum size in bytes (default 5MB)</param>
        public void RotateLogFileIfNeeded(long? maxSizeBytes = null)
        {
            if (!isEnabled) return;
            
            try
            {
                if (File.Exists(logFilePath))
                {
                    FileInfo fileInfo = new FileInfo(logFilePath);
                    long maxSize = maxSizeBytes ?? maxLogFileSizeBytes;
                    
                    if (fileInfo.Length > maxSize)
                    {
                        // Create backup file name
                        string directory = Path.GetDirectoryName(logFilePath);
                        string fileName = Path.GetFileNameWithoutExtension(logFilePath);
                        string extension = Path.GetExtension(logFilePath);
                        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                        string backupPath = Path.Combine(directory, $"{fileName}_{timestamp}{extension}");
                        
                        // Rename existing file
                        File.Move(logFilePath, backupPath);
                        
                        // Log rotation message to the new file
                        LogInfo($"Log file rotated. Previous log saved to {backupPath}");
                    }
                }
            }
            catch
            {
                // Silent fail - if rotation fails, we'll just continue with the existing file
            }
        }

        /// <summary>
        /// Set the maximum log file size
        /// </summary>
        /// <param name="maxSizeBytes">Maximum size in bytes</param>
        public void SetMaxLogFileSize(int maxSizeBytes)
        {
            this.maxLogFileSizeBytes = maxSizeBytes;
        }

        /// <summary>
        /// Enable or disable logging
        /// </summary>
        /// <param name="enabled">True to enable, false to disable</param>
        public void SetEnabled(bool enabled)
        {
            this.isEnabled = enabled;
        }

        /// <summary>
        /// Check if logging is enabled
        /// </summary>
        /// <returns>True if logging is enabled</returns>
        public bool IsEnabled()
        {
            return isEnabled;
        }

        /// <summary>
        /// Get the current log file path
        /// </summary>
        /// <returns>The log file path</returns>
        public string GetLogFilePath()
        {
            return logFilePath;
        }

        /// <summary>
        /// Set a new log file path
        /// </summary>
        /// <param name="path">New log file path</param>
        public void SetLogFilePath(string path)
        {
            this.logFilePath = path;
            // Reset isEnabled when path changes, assuming new path might be writable
            this.isEnabled = true; 
        }

        /// <summary>
        /// Set the enabled status based on external settings
        /// </summary>
        public void SetSettingsEnabled(bool enabled)
        {
            this.settingsEnabled = enabled;
            // If settings re-enable logging, make sure internal flag is also reset 
            // unless a persistent error prevents writing.
            if (enabled) this.isEnabled = true; 
        }

        public void SetDebugEnabled(bool enabled)
        {
            debugEnabled = enabled;
        }
        
        public bool IsDebugEnabled()
        {
            return debugEnabled;
        }
        
        public void SetDebugLogPath(string path)
        {
            // Only update if path is not null or empty
            if (!string.IsNullOrEmpty(path))
            {
                debugLogPath = path;
            }
        }
        
        public string GetDebugLogPath()
        {
            return debugLogPath;
        }

        /// <summary>
        /// Rotate debug log file if it exceeds the maximum size
        /// </summary>
        /// <param name="maxSizeBytes">Maximum size in bytes (default 5MB)</param>
        public void RotateDebugLogFileIfNeeded(long? maxSizeBytes = null)
        {
            try
            {
                if (File.Exists(debugLogPath))
                {
                    FileInfo fileInfo = new FileInfo(debugLogPath);
                    long maxSize = maxSizeBytes ?? maxLogFileSizeBytes;
                    
                    if (fileInfo.Length > maxSize)
                    {
                        // Create backup file name
                        string directory = Path.GetDirectoryName(debugLogPath);
                        string fileName = Path.GetFileNameWithoutExtension(debugLogPath);
                        string extension = Path.GetExtension(debugLogPath);
                        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                        string backupPath = Path.Combine(directory, $"{fileName}_{timestamp}{extension}");
                        
                        // Rename existing file
                        File.Move(debugLogPath, backupPath);
                        
                        // Log rotation message to the new file
                        LogDebug($"Debug log file rotated. Previous log saved to {backupPath}");
                    }
                }
            }
            catch
            {
                // Silent fail - if rotation fails, we'll just continue with the existing file
            }
        }
    }
} 