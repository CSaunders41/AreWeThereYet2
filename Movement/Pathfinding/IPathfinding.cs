using System.Collections.Generic;
using SharpDX;

namespace AreWeThereYet2.Movement.Pathfinding;

/// <summary>
/// Interface for pathfinding systems
/// Designed to be extensible for future LineOfSight integration
/// </summary>
public interface IPathfinding
{
    /// <summary>
    /// Find path from current position to target
    /// </summary>
    PathfindingResult FindPath(Vector3 start, Vector3 target);

    /// <summary>
    /// Check if direct line to target is walkable
    /// </summary>
    bool IsDirectPathWalkable(Vector3 start, Vector3 target);

    /// <summary>
    /// Get next movement point along path
    /// </summary>
    Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos);

    /// <summary>
    /// Check if position is walkable
    /// </summary>
    bool IsWalkable(Vector3 position);
}

/// <summary>
/// Result of pathfinding operation
/// </summary>
public class PathfindingResult
{
    public bool Success { get; set; }
    public List<Vector3> Path { get; set; } = new List<Vector3>();
    public float Distance { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Simplified path with only essential waypoints
    /// </summary>
    public List<Vector3> SimplifiedPath { get; set; } = new List<Vector3>();
}

/// <summary>
/// Terrain detection levels (based on AreWeThereYet 6-level system)
/// </summary>
public enum TerrainType
{
    Walkable = 0,
    Obstacle = 1,
    Water = 2,
    Wall = 3,
    Door = 4,
    Unknown = 5
}

/// <summary>
/// Door states for real-time door monitoring - ENHANCED pathfinding
/// </summary>
public enum DoorState
{
    None,       // No door present
    Open,       // Door is open and passable
    Closed,     // Door is closed and blocking
    Opening     // Door is in transition (opening/closing)
}

/// <summary>
/// Movement skill types for intelligent obstacle bypass - CRITICAL FEATURE from original AreWeThereYet
/// </summary>
public enum MovementSkillType
{
    None,           // No movement skill needed
    Dash,           // Quick dash through gaps
    LeapSlam,       // Leap over obstacles
    LightningWarp,  // Teleport past complex obstacles
    Blink,          // Short-range teleport
    FlameRush       // Fire-based movement skill
}

/// <summary>
/// Obstacle information for pathfinding analysis
/// </summary>
public class ObstacleInfo
{
    public Vector3 Position { get; set; }
    public TerrainType Type { get; set; }
    public float Distance { get; set; }
    public float Width { get; set; }
    public bool CanBypass { get; set; }
}