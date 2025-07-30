using System;
using System.Threading.Tasks;
using ExileCore;
using SharpDX;
using AreWeThereYet2.Movement.Pathfinding;
using AreWeThereYet2.Utils;
using AreWeThereYet2.Settings;

namespace AreWeThereYet2.Movement;

/// <summary>
/// Real movement execution system that replaces placeholder movement
/// Integrates pathfinding with human-like mouse movement
/// </summary>
public class MovementExecutor : IDisposable
{
    private readonly GameController _gameController;
    private readonly IPathfinding _pathfinding;
    private readonly Action<string> _debugLog;
    private readonly AreWeThereYet2Settings _settings;
    
    private PathfindingResult? _currentPath;
    private int _currentPathIndex = 0;
    private DateTime _lastMovementTime = DateTime.MinValue;
    private bool _disposed = false;

    // Movement constants - FIXED: More aggressive movement to prevent falling behind
    private const float MovementThreshold = 20f; // Minimum distance to trigger movement
    private const float WaypointThreshold = 50f; // FIXED: Larger threshold to prevent micro-movements
    private const int MovementTimeout = 8000; // FIXED: Longer timeout for bigger movements

    public MovementExecutor(
        GameController gameController,
        IPathfinding pathfinding,
        Action<string> debugLog,
        AreWeThereYet2Settings settings)
    {
        _gameController = gameController;
        _pathfinding = pathfinding;
        _debugLog = debugLog;
        _settings = settings;
    }

    /// <summary>
    /// Execute movement to target position using pathfinding and mouse movement
    /// This replaces the placeholder ExecuteBasicMovement
    /// </summary>
    public async Task<bool> ExecuteMovementToPosition(Vector3 targetPosition)
    {
        if (_disposed) return false;

        try
        {
            var player = _gameController?.Player;
            if (player == null)
            {
                _debugLog("MOVEMENT: Player is NULL");
                return false;
            }

            var currentPos = player.Pos;
            var distance = Vector3.Distance(currentPos, targetPosition);
            
            _debugLog($"MOVEMENT: Start execution to {targetPosition}, distance: {distance:F1}");

            // Check if we're close enough (task completion)
            var followDistance = _settings?.MaxFollowDistance?.Value ?? 30f;
            if (distance <= followDistance)
            {
                _debugLog($"MOVEMENT: Already close enough ({distance:F1} <= {followDistance:F1})");
                return true; // Task complete - we're close enough
            }

            // Check if we need to find a new path
            if (_currentPath == null || !IsPathStillValid(currentPos, targetPosition))
            {
                _debugLog("MOVEMENT: Finding new path...");
                _currentPath = _pathfinding.FindPath(currentPos, targetPosition);
                _currentPathIndex = 0;

                if (!_currentPath.Success)
                {
                    _debugLog($"MOVEMENT: Pathfinding failed - {_currentPath.ErrorMessage}");
                    return false; // Can't find path - task fails
                }

                _debugLog($"MOVEMENT: Path found with {_currentPath.SimplifiedPath.Count} waypoints");
            }

            // Execute movement along path
            var moved = await ExecuteNextMovementStep(currentPos);
            
            if (moved)
            {
                _lastMovementTime = DateTime.Now;
                _debugLog("MOVEMENT: Step executed successfully");
                return false; // Task continues (movement in progress)
            }
            else
            {
                // Check for timeout
                if (DateTime.Now - _lastMovementTime > TimeSpan.FromMilliseconds(MovementTimeout))
                {
                    _debugLog("MOVEMENT: Timeout - clearing path and retrying");
                    _currentPath = null; // Force new path on next execution
                    return false; // Task continues with retry
                }

                _debugLog("MOVEMENT: No movement this step, continuing...");
                return false; // Task continues
            }
        }
        catch (Exception ex)
        {
            _debugLog($"MOVEMENT ERROR: {ex.Message}");
            return false; // Task continues after error
        }
    }

    /// <summary>
    /// Execute the next movement step along the current path
    /// </summary>
    private async Task<bool> ExecuteNextMovementStep(Vector3 currentPos)
    {
        try
        {
            if (_currentPath?.SimplifiedPath == null || _currentPath.SimplifiedPath.Count == 0)
                return false;

            // Get current waypoint target
            if (_currentPathIndex >= _currentPath.SimplifiedPath.Count)
            {
                _debugLog("MOVEMENT: Reached end of path");
                _currentPath = null; // Path completed
                return false;
            }

            var targetWaypoint = _currentPath.SimplifiedPath[_currentPathIndex];
            var distanceToWaypoint = Vector3.Distance(currentPos, targetWaypoint);

            // Check if we've reached current waypoint - FIXED: Dynamic threshold based on total distance
            var totalDistance = Vector3.Distance(_gameController.Player.Pos, _currentPath.SimplifiedPath[_currentPath.SimplifiedPath.Count - 1]);
            var dynamicThreshold = totalDistance > 200f ? WaypointThreshold * 1.5f : WaypointThreshold;
            
            if (distanceToWaypoint <= dynamicThreshold)
            {
                _currentPathIndex++;
                _debugLog($"MOVEMENT: Waypoint {_currentPathIndex} reached (threshold: {dynamicThreshold:F1}), advancing to next");
                return true; // We moved (progressed to next waypoint)
            }

            // Convert world position to screen coordinates for mouse movement
            var screenPos = Mouse.WorldToScreen(targetWaypoint, _gameController);
            if (screenPos == Vector2.Zero)
            {
                _debugLog("MOVEMENT: Failed to convert world to screen coordinates");
                return false;
            }

            // Execute mouse movement and click
            _debugLog($"MOVEMENT: Moving mouse to {screenPos} (world: {targetWaypoint})");
            await Mouse.ClickAt(screenPos, 200); // 200ms movement duration

            return true; // Movement command executed
        }
        catch (Exception ex)
        {
            _debugLog($"MOVEMENT STEP ERROR: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if current path is still valid
    /// </summary>
    private bool IsPathStillValid(Vector3 currentPos, Vector3 targetPos)
    {
        if (_currentPath == null) return false;

        try
        {
            // Check if target has moved significantly - FIXED: More tolerant when leader is far
            var originalTarget = _currentPath.SimplifiedPath[_currentPath.SimplifiedPath.Count - 1];
            var targetMoved = Vector3.Distance(originalTarget, targetPos) > 80f; // FIXED: Larger tolerance

            if (targetMoved)
            {
                _debugLog("MOVEMENT: Target moved significantly, path invalid");
                return false;
            }

            // Check if we're far from our expected position on the path - FIXED: More tolerant
            if (_currentPathIndex < _currentPath.SimplifiedPath.Count)
            {
                var expectedWaypoint = _currentPath.SimplifiedPath[_currentPathIndex];
                var distanceFromPath = Vector3.Distance(currentPos, expectedWaypoint);

                if (distanceFromPath > 150f) // FIXED: More tolerant to prevent constant recalculation
                {
                    _debugLog("MOVEMENT: Too far from path, recalculating");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _debugLog($"PATH VALIDATION ERROR: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get movement status for debugging
    /// </summary>
    public string GetMovementStatus()
    {
        if (_currentPath == null)
            return "No active path";

        if (_currentPath.SimplifiedPath.Count == 0)
            return "Empty path";

        return $"Path: {_currentPathIndex + 1}/{_currentPath.SimplifiedPath.Count} waypoints";
    }

    /// <summary>
    /// Clear current path (force recalculation)
    /// </summary>
    public void ClearPath()
    {
        _currentPath = null;
        _currentPathIndex = 0;
        _debugLog("MOVEMENT: Path cleared");
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _currentPath = null;
        _disposed = true;
    }
}