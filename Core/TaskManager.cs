using System.Collections.Concurrent;

namespace AreWeThereYet2.Core;

/// <summary>
/// Manages task execution with priority queuing and lifecycle handling
/// </summary>
public class TaskManager : IDisposable
{
    private readonly ErrorManager _errorManager;
    private readonly SortedSet<TaskNode> _taskQueue;
    private readonly Dictionary<string, TaskNode> _taskLookup;
    private readonly ConcurrentQueue<TaskNode> _completedTasks;
    private readonly object _queueLock = new();
    private TaskNode? _currentTask;
    private bool _disposed;

    public TaskManager(ErrorManager errorManager)
    {
        _errorManager = errorManager;
        _taskQueue = new SortedSet<TaskNode>();
        _taskLookup = new Dictionary<string, TaskNode>();
        _completedTasks = new ConcurrentQueue<TaskNode>();
    }

    /// <summary>
    /// Add a new task to the queue
    /// </summary>
    public void AddTask(TaskNode task)
    {
        if (_disposed) return;

        lock (_queueLock)
        {
            try
            {
                // Remove existing task with same ID if present
                if (_taskLookup.TryGetValue(task.Id, out var existingTask))
                {
                    _taskQueue.Remove(existingTask);
                    _taskLookup.Remove(task.Id);
                }

                // Add new task
                _taskQueue.Add(task);
                _taskLookup[task.Id] = task;
            }
            catch (Exception ex)
            {
                _errorManager.HandleError($"TaskManager.AddTask({task.Id})", ex);
            }
        }
    }

    /// <summary>
    /// Create and add a simple task
    /// </summary>
    public void AddTask(
        string id,
        TaskPriority priority,
        string description,
        Func<bool> action,
        Func<bool>? canExecute = null,
        TimeSpan? timeout = null,
        int maxRetries = 3)
    {
        var task = new TaskNode(id, priority, description, action, canExecute, timeout, maxRetries);
        AddTask(task);
    }

    /// <summary>
    /// Remove a task by ID
    /// </summary>
    public bool RemoveTask(string taskId)
    {
        if (_disposed) return false;

        lock (_queueLock)
        {
            try
            {
                if (_taskLookup.TryGetValue(taskId, out var task))
                {
                    task.Cancel();
                    _taskQueue.Remove(task);
                    _taskLookup.Remove(taskId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _errorManager.HandleError($"TaskManager.RemoveTask({taskId})", ex);
            }
        }

        return false;
    }

    /// <summary>
    /// Update task manager - should be called regularly
    /// </summary>
    public void Update()
    {
        if (_disposed) return;

        try
        {
            CleanupCompletedTasks();
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("TaskManager.Update", ex);
        }
    }

    /// <summary>
    /// Process the next highest priority task
    /// </summary>
    public bool ProcessNextTask()
    {
        if (_disposed) return false;

        lock (_queueLock)
        {
            try
            {
                // If currently executing a task, don't start another
                if (_currentTask != null && !_currentTask.IsCompleted() && !_currentTask.IsFailed())
                {
                    bool success = _currentTask.Execute();
                    
                    if (_currentTask.IsCompleted() || _currentTask.IsFailed())
                    {
                        // Task finished, move to completed queue
                        _completedTasks.Enqueue(_currentTask);
                        _taskQueue.Remove(_currentTask);
                        _taskLookup.Remove(_currentTask.Id);
                        _currentTask = null;
                    }
                    
                    return success;
                }

                // Get next task from queue
                if (_taskQueue.Count == 0)
                    return false;

                var nextTask = _taskQueue.Min;
                if (nextTask == null)
                    return false;

                // Check if task can be executed
                if (nextTask.CanExecute != null && !nextTask.CanExecute())
                {
                    return false;
                }

                // Execute the task
                _currentTask = nextTask;
                bool taskSuccess = _currentTask.Execute();

                // Handle immediate completion/failure
                if (_currentTask.IsCompleted() || _currentTask.IsFailed())
                {
                    _completedTasks.Enqueue(_currentTask);
                    _taskQueue.Remove(_currentTask);
                    _taskLookup.Remove(_currentTask.Id);
                    _currentTask = null;
                }

                return taskSuccess;
            }
            catch (Exception ex)
            {
                _errorManager.HandleError("TaskManager.ProcessNextTask", ex);
                
                // Clean up current task on error
                if (_currentTask != null)
                {
                    _currentTask.Cancel();
                    _completedTasks.Enqueue(_currentTask);
                    _taskQueue.Remove(_currentTask);
                    _taskLookup.Remove(_currentTask.Id);
                    _currentTask = null;
                }
                
                return false;
            }
        }
    }

    /// <summary>
    /// Get the number of active (pending + running) tasks
    /// </summary>
    public int GetActiveTaskCount()
    {
        lock (_queueLock)
        {
            return _taskQueue.Count + (_currentTask != null ? 1 : 0);
        }
    }

    /// <summary>
    /// Get the currently executing task
    /// </summary>
    public TaskNode? GetCurrentTask()
    {
        return _currentTask;
    }

    /// <summary>
    /// Get all pending tasks (ordered by priority)
    /// </summary>
    public List<TaskNode> GetPendingTasks()
    {
        lock (_queueLock)
        {
            return _taskQueue.ToList();
        }
    }

    /// <summary>
    /// Check if a specific task exists
    /// </summary>
    public bool HasTask(string taskId)
    {
        lock (_queueLock)
        {
            return _taskLookup.ContainsKey(taskId);
        }
    }

    /// <summary>
    /// Get task by ID
    /// </summary>
    public TaskNode? GetTask(string taskId)
    {
        lock (_queueLock)
        {
            _taskLookup.TryGetValue(taskId, out var task);
            return task;
        }
    }

    /// <summary>
    /// Clear all tasks
    /// </summary>
    public void ClearAllTasks()
    {
        lock (_queueLock)
        {
            foreach (var task in _taskQueue)
            {
                task.Cancel();
            }
            
            _taskQueue.Clear();
            _taskLookup.Clear();
            
            if (_currentTask != null)
            {
                _currentTask.Cancel();
                _currentTask = null;
            }
        }
    }

    /// <summary>
    /// Remove completed/failed tasks from memory
    /// </summary>
    private void CleanupCompletedTasks()
    {
        const int maxCompletedTasks = 50;
        
        while (_completedTasks.Count > maxCompletedTasks)
        {
            _completedTasks.TryDequeue(out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        ClearAllTasks();
        _disposed = true;
    }
} 