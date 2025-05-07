using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace StudyHelper.Services.Logging
{
    /// <summary>
    /// File logger extension methods for the ILoggingBuilder.
    /// Implements the Builder design pattern for logger configuration.
    /// </summary>
    public static class FileLoggerExtensions
    {
        /// <summary>
        /// Adds a file-based logger provider to the logging builder.
        /// </summary>
        /// <param name="builder">The logging builder to extend.</param>
        /// <param name="filePath">The file path to write log entries to.</param>
        /// <returns>The updated logging builder.</returns>
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
        {
            builder.AddProvider(new FileLoggerProvider(filePath));
            return builder;
        }
    }

    /// <summary>
    /// Provides an instance of FileLogger for logging to a file.
    /// Part of the Strategy pattern for interchangeable logging implementations.
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;

        /// <summary>
        /// Creates a new FileLoggerProvider with the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the log file.</param>
        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>
        /// Creates a FileLogger for a specific category.
        /// </summary>
        /// <param name="categoryName">The category name (ignored in this implementation).</param>
        /// <returns>A new FileLogger instance.</returns>
        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_filePath);
        }

        /// <summary>
        /// Required method to inherit from ILogger.
        /// </summary>
        public void Dispose()
        {
            // No resources to dispose
        }
    }

    /// <summary>
    /// A simple file-based implementation of ILogger.
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _filePath;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes a new FileLogger with the specified file path.
        /// </summary>
        /// <param name="filePath">The file path to write log entries to.</param>
        public FileLogger(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>
        /// Not implemented. Returns null.
        /// </summary>
        /// <typeparam name="TState">The type of the state object.</typeparam>
        /// <param name="state">The state object.</param>
        /// <returns>Always returns null.</returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            return null; // Not implemented for this simple logger
        }

        /// <summary>
        /// Determines whether logging is enabled for the specified log level.
        /// </summary>
        /// <param name="logLevel">The log level to check.</param>
        /// <returns>True if logging is enabled for the specified level; otherwise, false.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        /// <summary>
        /// Logs a message to the specified log file with the provided log level, event ID, state, and optional exception.
        /// This method formats the log entry with a timestamp, log level, and message, and appends it to the log file.
        /// </summary>
        /// <typeparam name="TState">The type of the state object used in the logging.</typeparam>
        /// <param name="logLevel">The severity level of the log.</param>
        /// <param name="eventId">The identifier of the log event.</param>
        /// <param name="state">The content to be logged.</param>
        /// <param name="exception">Optional exception related to the log entry.</param>
        /// <param name="formatter">Function that converts the state and exception into a string message.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] - {message}";

            if (exception != null)
                logEntry += Environment.NewLine + exception.ToString();

            lock (_lock)
            {
                File.AppendAllText(_filePath, logEntry + Environment.NewLine);
            }
        }
    }
}