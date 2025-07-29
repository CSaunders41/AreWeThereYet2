namespace AreWeThereYet2.Core;

/// <summary>
/// Defines priority levels for task execution
/// Lower numbers = higher priority
/// </summary>
public enum TaskPriority
{
    /// <summary>
    /// Emergency tasks: Portal to town, stuck recovery, character safety
    /// </summary>
    Emergency = 0,
    
    /// <summary>
    /// Combat tasks: Combat positioning, leashing, retreat behavior
    /// </summary>
    Combat = 1,
    
    /// <summary>
    /// Movement tasks: Following, pathfinding, zone travel
    /// </summary>
    Movement = 2,
    
    /// <summary>
    /// Maintenance tasks: Aura casting, buff management, routine checks
    /// </summary>
    Maintenance = 3,
    
    /// <summary>
    /// Background tasks: Caching, cleanup, optimization
    /// </summary>
    Background = 4
} 