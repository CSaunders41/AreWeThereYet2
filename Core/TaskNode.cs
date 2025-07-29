using System.Numerics;

namespace AreWeThereYet2.Core;

/// <summary>
/// Enhanced task node with priority support and lifecycle management
/// </summary>
public class TaskNode : IComparable<TaskNode>
{
    public string Id { get; }
    public TaskPriority Priority { get; }
    public string Description { get; }
    public Func<bool> ExecuteAction { get; }
    public Func<bool>? CanExecute { get; }
    public DateTime CreatedAt { get; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public TimeSpan? Timeout { get; }
    public int MaxRetries { get; }
    public int RetryCount { get; private set; }
    public TaskStatus Status { get; private set; }
    public Vector3? TargetPosition { get; }
    public string? ErrorMessage { get; private set; }

    public TaskNode(
        string id,
        TaskPriority priority,
        string description,
        Func<bool> executeAction,
        Func<bool>? canExecute = null,
        TimeSpan? timeout = null,
        int maxRetries = 3,
        Vector3? targetPosition = null)
    {
        Id = id;
        Priority = priority;
        Description = description;
        ExecuteAction = executeAction;
        CanExecute = canExecute;
        CreatedAt = DateTime.UtcNow;
        Timeout = timeout ?? TimeSpan.FromSeconds(30); // Default 30 second timeout
        MaxRetries = maxRetries;
        Status = TaskStatus.Pending;
        RetryCount = 0;
        TargetPosition = targetPosition;
    }

    public bool Execute()
    {
        try
        {
            // Check if task can be executed
            if (CanExecute != null && !CanExecute())
            {
                return false;
            }

            // Check timeout
            if (IsTimedOut())
            {
                SetStatus(TaskStatus.TimedOut);
                return false;
            }

            // Mark as started if not already
            if (Status == TaskStatus.Pending)
            {
                SetStatus(TaskStatus.Running);
                StartedAt = DateTime.UtcNow;
            }

            // Execute the task
            bool success = ExecuteAction();
            
            if (success)
            {
                SetStatus(TaskStatus.Completed);
                CompletedAt = DateTime.UtcNow;
                return true;
            }
            else
            {
                // Handle retry logic
                RetryCount++;
                if (RetryCount >= MaxRetries)
                {
                    SetStatus(TaskStatus.Failed);
                    ErrorMessage = $"Task failed after {MaxRetries} retries";
                    return false;
                }
                
                // Reset to pending for retry
                SetStatus(TaskStatus.Pending);
                return false;
            }
        }
        catch (Exception ex)
        {
            RetryCount++;
            ErrorMessage = ex.Message;
            
            if (RetryCount >= MaxRetries)
            {
                SetStatus(TaskStatus.Failed);
                return false;
            }
            
            SetStatus(TaskStatus.Pending);
            return false;
        }
    }

    public bool IsTimedOut()
    {
        if (Timeout == null || StartedAt == null)
            return false;
            
        return DateTime.UtcNow - StartedAt.Value > Timeout.Value;
    }

    public bool IsCompleted()
    {
        return Status == TaskStatus.Completed;
    }

    public bool IsFailed()
    {
        return Status == TaskStatus.Failed || Status == TaskStatus.TimedOut;
    }

    public bool CanRetry()
    {
        return Status != TaskStatus.Completed && 
               Status != TaskStatus.Failed && 
               Status != TaskStatus.TimedOut &&
               RetryCount < MaxRetries;
    }

    public void Cancel()
    {
        SetStatus(TaskStatus.Cancelled);
    }

    private void SetStatus(TaskStatus newStatus)
    {
        Status = newStatus;
    }

    public int CompareTo(TaskNode? other)
    {
        if (other == null) return 1;
        
        // First compare by priority (lower number = higher priority)
        int priorityComparison = Priority.CompareTo(other.Priority);
        if (priorityComparison != 0)
            return priorityComparison;
            
        // If same priority, compare by creation time (earlier = higher priority)
        return CreatedAt.CompareTo(other.CreatedAt);
    }

    public override string ToString()
    {
        return $"[{Priority}] {Description} ({Status})";
    }
}

/// <summary>
/// Status of a task during its lifecycle
/// </summary>
public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    TimedOut,
    Cancelled
} 