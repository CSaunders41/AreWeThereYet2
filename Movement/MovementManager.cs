using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using System.Numerics;
using AreWeThereYet2.Core;
using AreWeThereYet2.Party;
using AreWeThereYet2.Settings;

namespace AreWeThereYet2.Movement;

/// <summary>
/// Handles basic movement and following logic
/// </summary>
public class MovementManager : IDisposable
{
    private readonly GameController _gameController;
    private readonly TaskManager _taskManager;
    private readonly PartyManager _partyManager;
    private readonly ErrorManager _errorManager;
    private readonly AreWeThereYet2Settings _settings;
    private DateTime _lastMovementCheck;
    private bool _disposed;

    // Movement constants
    private const int MovementCheckIntervalMs = 500; // Check every 500ms
    private const float DefaultFollowDistance = 30f;
    private const float MaxFollowDistance = 100f;

    public MovementManager(
        GameController gameController,
        TaskManager taskManager,
        PartyManager partyManager,
        ErrorManager errorManager,
        AreWeThereYet2Settings settings)
    {
        _gameController = gameController;
        _taskManager = taskManager;
        _partyManager = partyManager;
        _errorManager = errorManager;
        _settings = settings;
        _lastMovementCheck = DateTime.MinValue;
    }

    /// <summary>
    /// Update movement logic - should be called regularly
    /// </summary>
    public void Update()
    {
        if (_disposed) return;

        try
        {
            // Throttle movement checks
            if (DateTime.UtcNow - _lastMovementCheck < TimeSpan.FromMilliseconds(MovementCheckIntervalMs))
                return;

            _lastMovementCheck = DateTime.UtcNow;

            // Only process movement if we're in game and have a leader
            if (!IsReadyForMovement())
                return;

            // Check if we need to follow the leader
            ProcessLeaderFollowing();
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.Update", ex);
        }
    }

    /// <summary>
    /// Check if we're ready to process movement
    /// </summary>
    private bool IsReadyForMovement()
    {
        try
        {
            // Must be in game
            if (!_gameController?.Game?.IngameState?.InGame == true)
                return false;

            // Must have a valid player
            if (_gameController?.Player == null)
                return false;

            // Must have a party leader to follow
            var leader = _partyManager?.GetPartyLeader();
            if (leader == null)
                return false;

            // Settings must allow following
            if (!_settings?.Enable?.Value == true)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.IsReadyForMovement", ex);
            return false;
        }
    }

    /// <summary>
    /// Process following the party leader
    /// </summary>
    private void ProcessLeaderFollowing()
    {
        try
        {
            var player = _gameController.Player;
            var leader = _partyManager.GetPartyLeader();

            if (player == null || leader == null)
                return;

            // Calculate distance to leader
            var playerPos = player.Pos;
            var leaderPos = leader.Pos;
            var distance = CalculateDistance(playerPos, leaderPos);

            // Get follow distance from settings
            var followDistance = _settings?.MaxFollowDistance?.Value ?? DefaultFollowDistance;

            // If we're too far from leader, create a movement task
            if (distance > followDistance)
            {
                CreateFollowTask(leaderPos, distance);
            }
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.ProcessLeaderFollowing", ex);
        }
    }

    /// <summary>
    /// Create a task to follow the leader
    /// </summary>
    private void CreateFollowTask(Vector3 leaderPosition, float currentDistance)
    {
        try
        {
            // Don't create duplicate follow tasks
            if (_taskManager.HasTask("follow_leader"))
                return;

            // Create follow task
            var taskDescription = $"Follow leader (distance: {currentDistance:F1})";
            
            _taskManager.AddTask(
                "follow_leader",
                TaskPriority.Movement,
                taskDescription,
                () => ExecuteMovementToPosition(leaderPosition),
                () => CanExecuteMovement(),
                TimeSpan.FromSeconds(10), // 10 second timeout
                maxRetries: 2
            );
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.CreateFollowTask", ex);
        }
    }

    /// <summary>
    /// Execute movement to a specific position
    /// </summary>
    private bool ExecuteMovementToPosition(Vector3 targetPosition)
    {
        try
        {
            var player = _gameController?.Player;
            if (player == null)
                return false;

            var currentPos = player.Pos;
            var distance = CalculateDistance(currentPos, targetPosition);

            // If we're close enough, consider the task complete
            var followDistance = _settings?.MaxFollowDistance?.Value ?? DefaultFollowDistance;
            if (distance <= followDistance)
                return true;

            // TODO: In Phase 2, we'll integrate with AreWeThereYet's pathfinding
            // For now, we'll use basic mouse movement simulation
            return ExecuteBasicMovement(targetPosition);
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.ExecuteMovementToPosition", ex);
            return false;
        }
    }

    /// <summary>
    /// Execute basic movement (placeholder for pathfinding integration)
    /// </summary>
    private bool ExecuteBasicMovement(Vector3 targetPosition)
    {
        try
        {
            // PLACEHOLDER: This is where we'll integrate AreWeThereYet's superior pathfinding
            // For Phase 1, we'll return true to simulate movement
            // This prevents infinite task creation while we implement proper pathfinding
            
            // Log the movement attempt for debugging
            var player = _gameController?.Player;
            if (player != null)
            {
                var currentPos = player.Pos;
                var distance = CalculateDistance(currentPos, targetPosition);
                
                // Consider movement successful if we're reasonably close
                // In real implementation, this would trigger actual pathfinding
                return distance < MaxFollowDistance;
            }

            return false;
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.ExecuteBasicMovement", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if we can execute movement
    /// </summary>
    private bool CanExecuteMovement()
    {
        try
        {
            // Basic checks for movement capability
            if (!IsReadyForMovement())
                return false;

            // Don't move if we're in combat (Phase 2 will handle this better)
            // For now, always allow movement
            return true;
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.CanExecuteMovement", ex);
            return false;
        }
    }

    /// <summary>
    /// Calculate distance between two positions
    /// </summary>
    private float CalculateDistance(Vector3 pos1, Vector3 pos2)
    {
        return (float)Math.Sqrt(
            Math.Pow(pos1.X - pos2.X, 2) +
            Math.Pow(pos1.Y - pos2.Y, 2));
    }

    /// <summary>
    /// Get current follow distance to leader
    /// </summary>
    public float? GetDistanceToLeader()
    {
        try
        {
            var player = _gameController?.Player;
            var leader = _partyManager?.GetPartyLeader();
            
            if (player == null || leader == null)
                return null;

            return CalculateDistance(player.Pos, leader.Pos);
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.GetDistanceToLeader", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if currently following
    /// </summary>
    public bool IsFollowing()
    {
        return _taskManager?.HasTask("follow_leader") == true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Cancel any active movement tasks
        _taskManager?.RemoveTask("follow_leader");
        
        _disposed = true;
    }
} 