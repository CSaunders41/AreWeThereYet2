using System.Collections.Concurrent;

namespace AreWeThereYet2.Core;

/// <summary>
/// Centralized error handling and tracking system
/// </summary>
public class ErrorManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ErrorCategory> _errorCategories;
    private readonly ConcurrentQueue<ErrorEntry> _recentErrors;
    private readonly object _circuitBreakerLock = new();
    private bool _disposed;

    // Circuit breaker settings
    private const int MaxErrorsPerCategory = 10;
    private const int MaxErrorsPerMinute = 50;
    private const int CircuitBreakerTimeoutMinutes = 5;

    public ErrorManager()
    {
        _errorCategories = new ConcurrentDictionary<string, ErrorCategory>();
        _recentErrors = new ConcurrentQueue<ErrorEntry>();
    }

    /// <summary>
    /// Handle an error with context
    /// </summary>
    public void HandleError(string context, Exception exception)
    {
        if (_disposed) return;

        try
        {
            var errorEntry = new ErrorEntry
            {
                Context = context,
                Exception = exception,
                Timestamp = DateTime.UtcNow,
                Message = exception.Message
            };

            // Add to recent errors queue
            _recentErrors.Enqueue(errorEntry);

            // Get or create error category
            var category = _errorCategories.GetOrAdd(context, _ => new ErrorCategory(context));
            
            // Update category statistics
            lock (category.Lock)
            {
                category.ErrorCount++;
                category.LastError = DateTime.UtcNow;
                category.LastErrorMessage = exception.Message;

                // Check if we should trigger circuit breaker
                if (category.ErrorCount >= MaxErrorsPerCategory)
                {
                    category.IsCircuitBreakerOpen = true;
                    category.CircuitBreakerOpenTime = DateTime.UtcNow;
                }
            }

            // Cleanup old errors
            CleanupOldErrors();

            // Log the error (could be expanded to use proper logging framework)
            Console.WriteLine($"[ERROR] {context}: {exception.Message}");
        }
        catch
        {
            // Swallow errors in error handling to prevent infinite loops
        }
    }

    /// <summary>
    /// Check if a specific context is currently circuit-broken
    /// </summary>
    public bool IsCircuitBreakerOpen(string context)
    {
        if (!_errorCategories.TryGetValue(context, out var category))
            return false;

        lock (category.Lock)
        {
            // Check if circuit breaker timeout has expired
            if (category.IsCircuitBreakerOpen && 
                DateTime.UtcNow - category.CircuitBreakerOpenTime > TimeSpan.FromMinutes(CircuitBreakerTimeoutMinutes))
            {
                // Reset circuit breaker
                category.IsCircuitBreakerOpen = false;
                category.ErrorCount = 0;
                return false;
            }

            return category.IsCircuitBreakerOpen;
        }
    }

    /// <summary>
    /// Get total error count across all categories
    /// </summary>
    public int GetErrorCount()
    {
        return _errorCategories.Values.Sum(c => c.ErrorCount);
    }

    /// <summary>
    /// Get error count for a specific context
    /// </summary>
    public int GetErrorCount(string context)
    {
        if (!_errorCategories.TryGetValue(context, out var category))
            return 0;

        return category.ErrorCount;
    }

    /// <summary>
    /// Get recent errors (last 100)
    /// </summary>
    public List<ErrorEntry> GetRecentErrors()
    {
        return _recentErrors.ToList().TakeLast(100).ToList();
    }

    /// <summary>
    /// Get error rate per minute for system health monitoring
    /// </summary>
    public double GetErrorRatePerMinute()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-1);
        var recentErrorCount = _recentErrors.Count(e => e.Timestamp >= cutoffTime);
        return recentErrorCount;
    }

    /// <summary>
    /// Check if the system is experiencing high error rates
    /// </summary>
    public bool IsSystemUnhealthy()
    {
        return GetErrorRatePerMinute() > MaxErrorsPerMinute;
    }

    /// <summary>
    /// Reset error statistics for a specific context
    /// </summary>
    public void ResetErrorCategory(string context)
    {
        if (_errorCategories.TryGetValue(context, out var category))
        {
            lock (category.Lock)
            {
                category.ErrorCount = 0;
                category.IsCircuitBreakerOpen = false;
                category.LastError = null;
                category.LastErrorMessage = null;
            }
        }
    }

    /// <summary>
    /// Get all error categories for diagnostics
    /// </summary>
    public Dictionary<string, ErrorCategoryInfo> GetErrorCategories()
    {
        var result = new Dictionary<string, ErrorCategoryInfo>();
        
        foreach (var kvp in _errorCategories)
        {
            var category = kvp.Value;
            lock (category.Lock)
            {
                result[kvp.Key] = new ErrorCategoryInfo
                {
                    Context = category.Context,
                    ErrorCount = category.ErrorCount,
                    LastError = category.LastError,
                    LastErrorMessage = category.LastErrorMessage,
                    IsCircuitBreakerOpen = category.IsCircuitBreakerOpen
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Remove old errors to prevent memory leaks
    /// </summary>
    private void CleanupOldErrors()
    {
        const int maxStoredErrors = 1000;
        
        while (_recentErrors.Count > maxStoredErrors)
        {
            _recentErrors.TryDequeue(out _);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _errorCategories.Clear();
        while (_recentErrors.TryDequeue(out _)) { }
        
        _disposed = true;
    }
}

/// <summary>
/// Internal error category for tracking
/// </summary>
internal class ErrorCategory
{
    public string Context { get; }
    public int ErrorCount { get; set; }
    public DateTime? LastError { get; set; }
    public string? LastErrorMessage { get; set; }
    public bool IsCircuitBreakerOpen { get; set; }
    public DateTime CircuitBreakerOpenTime { get; set; }
    public readonly object Lock = new();

    public ErrorCategory(string context)
    {
        Context = context;
    }
}

/// <summary>
/// Public error category information
/// </summary>
public class ErrorCategoryInfo
{
    public string Context { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
    public DateTime? LastError { get; set; }
    public string? LastErrorMessage { get; set; }
    public bool IsCircuitBreakerOpen { get; set; }
}

/// <summary>
/// Individual error entry
/// </summary>
public class ErrorEntry
{
    public string Context { get; set; } = string.Empty;
    public Exception Exception { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
} 