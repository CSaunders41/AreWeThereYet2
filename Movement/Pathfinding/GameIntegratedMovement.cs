using System;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using SharpDX;
using AreWeThereYet2.Utils;

namespace AreWeThereYet2.Movement.Pathfinding;

/// <summary>
/// BREAKTHROUGH: Game-Integrated Movement System
/// Based on research findings - work WITH Path of Exile's systems instead of against them
/// Key insight: Original AreWeThereYet doesn't pathfind - it lets the GAME do the pathfinding!
/// </summary>
public class GameIntegratedMovement : IPathfinding
{
    private readonly GameController _gameController;
    private readonly Action<string> _debugLog;
    
    // Leader tracking for hover behavior
    private Entity _currentLeader = null;
    private DateTime _lastLeaderUpdate = DateTime.MinValue;
    private Vector3 _lastKnownLeaderPos = Vector3.Zero;
    private Vector2 _lastLeaderScreenPos = Vector2.Zero;
    
    // Game integration constants
    private const float LeaderDetectionRange = 1500f;
    private const float HoverRange = 300f;      // Range to hover over leader
    private const float ClickRange = 800f;      // Range to click toward leader
    private const float MaxLeaderAge = 3.0f;    // Max seconds since last leader update
    
    // Update frequency - like original (high frequency updates)
    private DateTime _lastUpdate = DateTime.MinValue;
    private const float UpdateIntervalMs = 100f; // 10 updates per second
    
    public GameIntegratedMovement(GameController gameController, Action<string> debugLog)
    {
        _gameController = gameController;
        _debugLog = debugLog;
        
        _debugLog("GAME INTEGRATED: Revolutionary movement system initialized - works WITH Path of Exile!");
    }
    
    /// <summary>
    /// BREAKTHROUGH: Instead of complex pathfinding, use game's click-to-move system
    /// </summary>
    public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
    {
        try
        {
            // High-frequency updates like original
            if ((DateTime.UtcNow - _lastUpdate).TotalMilliseconds < UpdateIntervalMs)
                return null; // Don't spam updates
                
            _lastUpdate = DateTime.UtcNow;
            
            // STEP 1: Detect leader from game memory
            var leaderResult = DetectLeaderFromMemory(currentPos);
            
            if (leaderResult.Found)
            {
                _debugLog($"GAME INTEGRATED: Leader detected at distance {leaderResult.Distance:F1}");
                
                // BREAKTHROUGH: Use smooth mouse movement and game's pathfinding
                _ = HandleLeaderInteraction(leaderResult, currentPos);
                
                // Return null - we handle movement through mouse, not waypoints
                return null;
            }
            
            // No leader detected - use target position with game's pathfinding
            _debugLog("GAME INTEGRATED: No leader detected, using target position");
            _ = HandleTargetMovement(targetPos);
            
            return null; // Game handles pathfinding through mouse clicks
        }
        catch (Exception ex)
        {
            _debugLog($"GAME INTEGRATED ERROR: {ex.Message}");
            return targetPos; // Fallback
        }
    }
    
    /// <summary>
    /// Handle leader interaction with hover and smooth clicking like original
    /// </summary>
    private async Task HandleLeaderInteraction(LeaderDetectionResult leader, Vector3 currentPos)
    {
        try
        {
            var leaderScreenPos = _gameController.Game.IngameState.Camera.WorldToScreen(leader.Position);
            
            if (leader.Distance <= HoverRange)
            {
                // ORIGINAL BEHAVIOR: Hover over leader constantly
                _debugLog($"GAME INTEGRATED: HOVERING over leader (like original AreWeThereYet)");
                _ = SmoothMouseMovement.HoverOver(leaderScreenPos);
            }
            else if (leader.Distance <= ClickRange)
            {
                // ORIGINAL BEHAVIOR: Smooth click toward leader, let game pathfind
                _debugLog($"GAME INTEGRATED: SMOOTH CLICK toward leader - game handles pathfinding");
                _ = SmoothMouseMovement.SmoothClick(leaderScreenPos);
            }
            else
            {
                // Leader too far - click in their general direction
                var direction = Vector3.Normalize(leader.Position - currentPos);
                var intermediatePos = currentPos + (direction * 400f); // Move 400 units toward leader
                var intermediateScreenPos = _gameController.Game.IngameState.Camera.WorldToScreen(intermediatePos);
                
                _debugLog($"GAME INTEGRATED: Moving toward distant leader");
                _ = SmoothMouseMovement.SmoothClick(intermediateScreenPos);
            }
        }
        catch (Exception ex)
        {
            _debugLog($"LEADER INTERACTION ERROR: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle movement to target position using game's pathfinding
    /// </summary>
    private async Task HandleTargetMovement(Vector3 targetPos)
    {
        try
        {
            var targetScreenPos = _gameController.Game.IngameState.Camera.WorldToScreen(targetPos);
            
            _debugLog("GAME INTEGRATED: Smooth click to target - letting game handle pathfinding");
            _ = SmoothMouseMovement.SmoothClick(targetScreenPos);
        }
        catch (Exception ex)
        {
            _debugLog($"TARGET MOVEMENT ERROR: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Detect leader from game memory - simplified version focused on integration
    /// </summary>
    private LeaderDetectionResult DetectLeaderFromMemory(Vector3 currentPos)
    {
        try
        {
            var player = _gameController?.Player;
            if (player == null)
                return new LeaderDetectionResult { Found = false };

            var entities = _gameController?.EntityListWrapper?.Entities;
            if (entities == null)
                return new LeaderDetectionResult { Found = false };

            // Find closest player entity (leader candidate)
            Entity bestLeaderCandidate = null;
            float closestDistance = float.MaxValue;
            
            foreach (var entity in entities)
            {
                if (entity == null || !entity.IsValid) continue;
                
                var playerComponent = entity.GetComponent<Player>();
                if (playerComponent == null) continue;
                
                // Skip self
                if (entity.Id == player.Id) continue;
                
                var entityPos = entity.Pos;
                var distance = Vector3.Distance(currentPos, entityPos);
                
                if (distance > LeaderDetectionRange) continue;
                
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestLeaderCandidate = entity;
                }
            }
            
            if (bestLeaderCandidate != null)
            {
                var leaderPos = bestLeaderCandidate.Pos;
                var distance = Vector3.Distance(currentPos, leaderPos);
                
                // Update leader tracking
                _currentLeader = bestLeaderCandidate;
                _lastLeaderUpdate = DateTime.UtcNow;
                _lastKnownLeaderPos = leaderPos;
                
                return new LeaderDetectionResult
                {
                    Found = true,
                    Position = leaderPos,
                    Distance = distance,
                    Entity = bestLeaderCandidate
                };
            }
            
            // Check cached leader data
            var timeSinceLastUpdate = (DateTime.UtcNow - _lastLeaderUpdate).TotalSeconds;
            if (_currentLeader != null && timeSinceLastUpdate <= MaxLeaderAge)
            {
                var distance = Vector3.Distance(currentPos, _lastKnownLeaderPos);
                return new LeaderDetectionResult
                {
                    Found = true,
                    Position = _lastKnownLeaderPos,
                    Distance = distance,
                    Entity = _currentLeader
                };
            }
            
            return new LeaderDetectionResult { Found = false };
        }
        catch (Exception ex)
        {
            _debugLog($"LEADER DETECTION ERROR: {ex.Message}");
            return new LeaderDetectionResult { Found = false };
        }
    }
    
    /// <summary>
    /// Simple walkability check - let the game handle complex pathfinding
    /// </summary>
    public bool IsWalkable(Vector3 position)
    {
        // Let Path of Exile handle walkability - we don't need complex analysis
        return true;
    }
    
    /// <summary>
    /// Simple path check - let the game handle complex pathfinding
    /// </summary>
    public bool IsDirectPathWalkable(Vector3 start, Vector3 target)
    {
        // Let Path of Exile handle pathfinding - we just provide the target
        return true;
    }
    
    /// <summary>
    /// Leader detection result
    /// </summary>
    private class LeaderDetectionResult
    {
        public bool Found { get; set; }
        public Vector3 Position { get; set; }
        public float Distance { get; set; }
        public Entity Entity { get; set; }
    }
}