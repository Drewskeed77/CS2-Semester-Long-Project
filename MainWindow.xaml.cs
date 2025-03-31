using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace TaskManager
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<Task> tasks = new ObservableCollection<Task>();
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;

        public MainWindow()
        {
            InitializeComponent();

            // Set up logging
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFile($"{DateTime.Now:yyyy-MM-dd}.log");
            });

            _logger = _loggerFactory.CreateLogger<MainWindow>();

            lstTasks.ItemsSource = tasks;
            _logger.LogInformation("Application started");
            txtStatus.Text = "Application started";
        }

        // Base Task class demonstrating inheritance
        [JsonObject(IsReference = true)]
        public abstract class Task
        {
            public string Title { get; set; }
            public string Priority { get; set; }
            [JsonProperty("Type")] // Explicit JSON property name
            public abstract string TaskType { get; }
        }

        // Derived classes
        public class WorkTask : Task
        {
            public override string TaskType => "Work";
        }

        public class PersonalTask : Task
        {
            public override string TaskType => "Personal";
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTask.Text))
            {
                LogAndUpdateStatus("Error: Task title cannot be empty", LogLevel.Error);
                return;
            }

            string selectedPriority = (cmbPriority.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Medium";

            // Allow user to choose the task type (work/personal)
            Task newTask = MessageBox.Show("Is this a Work Task?", "Select Task Type",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
                ? new WorkTask()
                : new PersonalTask();

            newTask.Title = txtTask.Text;
            newTask.Priority = selectedPriority;

            tasks.Add(newTask);
            LogAndUpdateStatus($"Added task: {newTask.Title} ({newTask.TaskType}, {newTask.Priority})", LogLevel.Information);
        }

        private void SortByName_Click(object sender, RoutedEventArgs e)
        {
            var sortedTasks = tasks.OrderBy(t => t.Title).ToList();
            tasks.Clear();
            foreach (var task in sortedTasks)
                tasks.Add(task);

            LogAndUpdateStatus("Tasks sorted by name", LogLevel.Information);
        }

        private void SortByPriority_Click(object sender, RoutedEventArgs e)
        {
            var sortedTasks = tasks.OrderByDescending(t =>
                t.Priority == "High" ? 3 :
                t.Priority == "Medium" ? 2 : 1).ToList();

            tasks.Clear();
            foreach (var task in sortedTasks)
                tasks.Add(task);

            LogAndUpdateStatus("Tasks sorted by priority", LogLevel.Information);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            string searchTerm = txtSearch.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                lstTasks.ItemsSource = tasks; // Reset binding
                LogAndUpdateStatus("Search cleared, showing all tasks", LogLevel.Information);
                return;
            }

            var results = tasks.Where(t => t.Title.ToLower().Contains(searchTerm)).ToList();
            lstTasks.ItemsSource = results; // Temporarily override binding
            LogAndUpdateStatus($"Found {results.Count} tasks matching '{searchTerm}'", LogLevel.Information);
        }


        private void SaveTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "tasks.json");

                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    Formatting = Formatting.Indented
                };

                string json = JsonConvert.SerializeObject(tasks, settings);
                File.WriteAllText(path, json);

                _logger.LogInformation($"Tasks saved to: {path}");
                LogAndUpdateStatus($"Tasks saved to: {path}", LogLevel.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving tasks: {ex.Message}");
                LogAndUpdateStatus($"Error saving tasks: {ex.Message}", LogLevel.Error);
            }
        }


        private void LoadTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "tasks.json");

                if (File.Exists(path))
                {
                    // Deserialize to List<Task> first
                    var settings = new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All // Include type info for deserialization
                    };

                    string json = File.ReadAllText(path);
                    var loadedTasks = JsonConvert.DeserializeObject<List<Task>>(json, settings); // Deserialize to List<Task>

                    if (loadedTasks != null)
                    {
                        tasks.Clear();
                        foreach (var task in loadedTasks)
                        {
                            tasks.Add(task); // Add each deserialized task into the ObservableCollection
                        }

                        lstTasks.ItemsSource = tasks; // Reset list binding
                        LogAndUpdateStatus("Tasks loaded from file", LogLevel.Information);
                    }
                }
                else
                {
                    LogAndUpdateStatus("No saved tasks found", LogLevel.Warning);
                }
            }
            catch (JsonSerializationException ex)
            {
                // Catch serialization exceptions and display useful error messages
                _logger.LogError($"Serialization error: {ex.Message}");
                LogAndUpdateStatus($"Serialization error: {ex.Message}", LogLevel.Error);
            }
            catch (Exception ex)
            {
                // Catch any other general exceptions
                _logger.LogError($"Error loading tasks: {ex.Message}");
                LogAndUpdateStatus($"Error loading tasks: {ex.Message}", LogLevel.Error);
            }
        }




        private void LogAndUpdateStatus(string message, LogLevel logLevel)
        {
            // Update UI status
            txtStatus.Text = $"{DateTime.Now:T} - {message}";

            // Log to file with appropriate level
            switch (logLevel)
            {
                case LogLevel.Error:
                    _logger.LogError(message);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(message);
                    break;
                case LogLevel.Information:
                default:
                    _logger.LogInformation(message);
                    break;
            }
        }
    }

    // File logger provider extension
    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
        {
            builder.AddProvider(new FileLoggerProvider(filePath));
            return builder;
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;

        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_filePath);
        }

        public void Dispose()
        {
            // No resources to dispose
        }
    }

    public class FileLogger : ILogger
    {
        private readonly string _filePath;
        private static readonly object _lock = new object();

        public FileLogger(string filePath)
        {
            _filePath = filePath;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null; // Not implemented for this simple logger
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

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