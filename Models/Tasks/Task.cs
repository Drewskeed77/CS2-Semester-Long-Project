using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StudyHelper.Models.Tasks
{
    /// <summary>
    /// Base task class that serves as a foundation for the different task types.
    /// </summary>
    [JsonObject(IsReference = false)]
    public abstract class Task
    {
        /// <summary>
        /// Gets or sets the title of the task.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the priority level of the task.
        /// </summary>
        public string Priority { get; set; }

        /// <summary>
        /// Gets the type of the task.
        /// </summary>
        [JsonProperty("Type")] // Explicit JSON property name
        public abstract string TaskType { get; }
    }

    /// <summary>
    /// Represents a work-related task.
    /// </summary>
    public class WorkTask : Task
    {
        /// <summary>
        /// Gets the task type identifier.
        /// Always returns "Work" to identify this task type.
        /// </summary>
        public override string TaskType => "Work";
    }

    /// <summary>
    /// Represents a personal task.
    /// </summary>
    public class PersonalTask : Task
    {
        /// <summary>
        /// Gets the task type identifier.
        /// Always returns "Personal" to identify this task type.
        /// </summary>
        public override string TaskType => "Personal";
    }

    /// <summary>
    /// Factory class that creates different task types.
    /// </summary>
    public static class TaskFactory
    {
        /// <summary>
        /// Creates a new task of the specified type.
        /// </summary>
        /// <param name="isWorkTask">When true, creates a work task; otherwise creates a personal task.</param>
        /// <returns>A new task instance of the correct type.</returns>
        public static Task CreateTask(bool isWorkTask)
        {
            return isWorkTask ? new WorkTask() : new PersonalTask();
        }

        /// <summary>
        /// Creates a new task based on a task type string.
        /// </summary>
        /// <param name="taskType">String identifier of the task type ("Work" or "Personal").</param>
        /// <returns>A new task instance of the appropriate derived type.</returns>
        public static Task CreateTaskFromType(string taskType)
        {
            return taskType.Equals("Work", StringComparison.OrdinalIgnoreCase)
                ? new WorkTask()
                : new PersonalTask();
        }
    }
}