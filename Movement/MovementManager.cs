using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;
using SharpDX;
using AreWeThereYet2.Core;
using AreWeThereYet2.Party;
using AreWeThereYet2.Settings;
using AreWeThereYet2.Movement.Pathfinding;
using System.Threading.Tasks;

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
    private readonly Action<string> _debugLog;
    private readonly IPathfinding _pathfinding;
    private readonly MovementExecutor _movementExecutor;
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
        AreWeThereYet2Settings settings,
        Action<string> debugLog)
    {
        _gameController = gameController;
        _taskManager = taskManager;
        _partyManager = partyManager;
        _errorManager = errorManager;
        _settings = settings;
        _debugLog = debugLog;
        _lastMovementCheck = DateTime.MinValue;
        
        // Initialize BREAKTHROUGH: Game-Integrated Movement System - Works WITH Path of Exile
        _pathfinding = new GameIntegratedMovement(gameController, debugLog);
        _movementExecutor = new MovementExecutor(gameController, _pathfinding, debugLog, settings);
        
        debugLog("BREAKTHROUGH: Game-Integrated Movement System initialized - Revolutionary approach!");
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
            if (!_settings?.EnableFollowing?.Value == true)
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
    /// Process following the party leader (simplified approach like original AreWeThereYet)
    /// </summary>
    private void ProcessLeaderFollowing()
    {
        try
        {
            var player = _gameController.Player;
            var leader = _partyManager.GetPartyLeader();

            if (player == null)
            {
                _debugLog("Player is NULL");
                return;
            }

            if (leader == null)
            {
                _debugLog("Leader is NULL");
                return;
            }

            // Calculate distance to leader
            var playerPos = player.Pos;
            var leaderPos = leader.Pos;
            var distance = CalculateDistance(playerPos, leaderPos);

            // Get follow distance from settings (lower default for testing)
            var followDistance = _settings?.MaxFollowDistance?.Value ?? 30f; // Lower default for testing

            // ENHANCED DEBUG INFO - to diagnose leader selection issue
            var playerName = player.GetComponent<Player>()?.PlayerName ?? "Unknown";
            var leaderName = leader.GetComponent<Player>()?.PlayerName ?? "Unknown";
            var isPlayerSameAsLeader = (player == leader);
            var isPlayerPosVsLeaderPos = (playerPos.Equals(leaderPos));
            
            _debugLog($"ENTITIES: Player='{playerName}' vs Leader='{leaderName}' | SameEntity={isPlayerSameAsLeader} | SamePos={isPlayerPosVsLeaderPos}");
            _debugLog($"Distance: {distance:F1}, Threshold: {followDistance:F1}, PlayerPos: {playerPos}, LeaderPos: {leaderPos}");

            // Simple follow logic - no complex task system
            if (distance > followDistance)
            {
                // Check if task already exists BEFORE creating
                bool hasTaskBefore = _taskManager.HasTask("follow_leader");
                
                if (!hasTaskBefore)
                {
                    _debugLog($"CREATING NEW TASK! Distance {distance:F1} > {followDistance:F1}");
                    
                    // Check if we can actually create the task
                    bool canMove = CanExecuteMovement();
                    bool isReady = IsReadyForMovement();
                    
                    _debugLog($"CanExecuteMovement={canMove}, IsReadyForMovement={isReady}");
                        
                    CreateFollowTask(leaderPos, distance);
                    
                    // Verify task was actually added
                    bool hasTaskAfter = _taskManager.HasTask("follow_leader");
                    int taskCount = _taskManager.GetActiveTaskCount();
                    _debugLog($"After CreateFollowTask: HasTask={hasTaskAfter}, TaskCount={taskCount}");
                }
                else
                {
                    // Task already exists - check its status
                    var currentTask = _taskManager.GetCurrentTask();
                    _debugLog($"TASK EXISTS: Current={currentTask?.Description ?? "None"}, Status={currentTask?.Status.ToString() ?? "None"}");
                }
            }
            else
            {
                // We're close enough - remove any existing follow task
                if (_taskManager.HasTask("follow_leader"))
                {
                    _taskManager.RemoveTask("follow_leader");
                    _debugLog("Removed follow task - close enough");
                }
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
            {
                // Debug: Task already exists
                return;
            }

            // Create follow task
            var taskDescription = $"Follow leader (distance: {currentDistance:F1})";
            
            // Log task creation attempt
            var followDistance = _settings?.MaxFollowDistance?.Value ?? DefaultFollowDistance;
            
            _debugLog($"Creating follow task: distance={currentDistance:F1}, threshold={followDistance:F1}");
            
            _taskManager.AddTask(
                "follow_leader",
                TaskPriority.Movement,
                taskDescription,
                () => ExecuteMovementToPosition(leaderPosition).GetAwaiter().GetResult(),
                () => CanExecuteMovement(),
                TimeSpan.FromSeconds(10), // 10 second timeout
                maxRetries: 2
            );
            
            // Verify task was created
            if (_taskManager.HasTask("follow_leader"))
            {
                _debugLog("Follow task created successfully");
            }
            else
            {
                _debugLog("Follow task creation FAILED");
            }
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("MovementManager.CreateFollowTask", ex);
        }
    }

    /// <summary>
    /// Execute movement to a specific position using GAME-INTEGRATED movement system
    /// BREAKTHROUGH: This now works WITH Path of Exile's pathfinding instead of against it
    /// </summary>
    private async Task<bool> ExecuteMovementToPosition(Vector3 targetPosition)
    {
        try
        {
            _debugLog("BREAKTHROUGH: Executing GAME-INTEGRATED movement with smooth mouse curves");
            
            // Use the MovementExecutor with GameIntegratedMovement for revolutionary following
            var result = await _movementExecutor.ExecuteMovementToPosition(targetPosition);
            
            _debugLog($"GAME-INTEGRATED MOVEMENT RESULT: {result}");
            return result;
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
            
            var player = _gameController?.Player;
            if (player == null)
                return false;

            var currentPos = player.Pos;
            var distance = CalculateDistance(currentPos, targetPosition);
            
            _debugLog($"BASIC MOVEMENT: Distance={distance:F1}, Target={targetPosition}");
            
            // TEMPORARY FIX FOR THRASHING: Return false to keep task "in progress"
            // This prevents the rapid create->complete->create cycle
            // Task will stay active showing "Follow Leader" instead of flashing
            
            // In real implementation, this would:
            // 1. Trigger actual pathfinding/movement
            // 2. Return false while moving (in progress)  
            // 3. Return true when close enough (complete)
            
            // For Phase 0 testing: Always return false (keeps task alive)
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

        try
        {
            // Cancel any active movement tasks
            _taskManager?.RemoveTask("follow_leader");
            
            // Dispose BREAKTHROUGH Game-Integrated Movement System  
            _movementExecutor?.Dispose();
            _debugLog("BREAKTHROUGH: Game-Integrated Movement System disposed");
        }
        catch (Exception ex)
        {
            _errorManager?.HandleError("MovementManager.Dispose", ex);
        }
        
        _disposed = true;
    }
} 