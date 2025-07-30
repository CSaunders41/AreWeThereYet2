using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using SharpDX;

namespace AreWeThereYet2.Movement.Pathfinding;

/// <summary>
/// Smart predictive movement system that anticipates leader behavior
/// Replaces the over-engineered AdvancedLineOfSight system with focused intelligence
/// Key insight: Move to where the leader WILL BE, not where they ARE
/// </summary>
public class PredictiveMovement : IPathfinding
{
    private readonly GameController _gameController;
    private readonly Action<string> _debugLog;
    
    // Leader tracking for prediction
    private readonly Queue<LeaderSnapshot> _leaderHistory = new();
    private DateTime _lastUpdate = DateTime.MinValue;
    private Vector3 _lastPredictedPosition = Vector3.Zero;
    
    // Direct Leader Detection - EXPERIMENT 5
    private Entity _currentLeader = null;
    private DateTime _lastLeaderUpdate = DateTime.MinValue;
    private Vector3 _lastKnownLeaderPos = Vector3.Zero;
    
    // Movement constants - Aggressive like original AreWeThereYet
    private const float MinStepSize = 80f;           // Never take tiny steps that cause stuck scenarios
    private const float StandardStepSize = 120f;    // Default large step size
    private const float AggressiveStepSize = 180f;  // For far leaders or fast movement
    private const float PredictionTime = 2.0f;      // Predict 2 seconds ahead
    private const int MaxHistorySize = 10;          // Keep last 10 positions for analysis
    private const float AvoidanceAngle = 30f;       // Simple left/right avoidance
    
    // Direct Leader Clicking constants - EXPERIMENT 5
    private const float DirectClickRange = 200f;    // Range for direct leader clicking
    private const float LeaderDetectionRange = 1500f; // Maximum range to detect leader
    private const float MaxLeaderAge = 5.0f;        // Max seconds since last leader update
    private const float ConfidentStepSize = 150f;   // Aggressive step size when leader detected
    
    public PredictiveMovement(GameController gameController, Action<string> debugLog)
    {
        _gameController = gameController;
        _debugLog = debugLog;
        
        _debugLog("PREDICTIVE: Intelligent predictive movement system initialized");
    }

    /// <summary>
    /// Find path using predictive intelligence - anticipate leader movement
    /// </summary>
    public PathfindingResult FindPath(Vector3 start, Vector3 target)
    {
        var result = new PathfindingResult();
        
        try
        {
            // Step 1: Update leader tracking and predict where they'll be
            UpdateLeaderHistory(target);
            var predictedTarget = PredictLeaderPosition();
            
            // Step 2: Calculate optimal interception point
            var interceptionPoint = CalculateInterceptionPoint(start, predictedTarget);
            
            // Step 3: Choose movement strategy based on distance and context
            var movementPoint = ChooseMovementStrategy(start, interceptionPoint, target);
            
            // Step 4: Create simple, direct path
            result.Success = true;
            result.Path = new List<Vector3> { start, movementPoint };
            result.SimplifiedPath = new List<Vector3> { movementPoint };
            result.Distance = Vector3.Distance(start, movementPoint);
            
            var distance = Vector3.Distance(start, target);
            var predictedDistance = Vector3.Distance(start, predictedTarget);
            
            _debugLog($"PREDICTIVE: Target={target}, Predicted={predictedTarget}, " +
                     $"Movement={movementPoint}, Distance={distance:F1}, PredictedDist={predictedDistance:F1}");
            
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Predictive pathfinding error: {ex.Message}";
            _debugLog($"PREDICTIVE ERROR: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Check if direct path is walkable - NOW USES EXILECORE MEMORY for obstacle detection
    /// </summary>
    public bool IsDirectPathWalkable(Vector3 start, Vector3 target)
    {
        try
        {
            var distance = Vector3.Distance(start, target);
            var heightDiff = Math.Abs(target.Z - start.Z);
            
            // Basic distance/height checks first
            if (distance > 800f || heightDiff > 150f)
                return false;
                
            // SHORT PATH: For close targets, do minimal checking for speed
            if (distance < 80f)
                return !HasMajorObstacleAt(target);
                
            // MEDIUM/LONG PATH: Check waypoints along the path using ExileCore memory
            var numChecks = Math.Max(3, (int)(distance / 60f)); // Check every 60 units
            for (int i = 1; i < numChecks; i++)
            {
                var t = (float)i / numChecks;
                var checkPoint = Vector3.Lerp(start, target, t);
                
                // Use ExileCore memory to detect actual obstacles
                if (HasMajorObstacleAt(checkPoint))
                {
                    _debugLog($"PREDICTIVE: Path blocked by obstacle at {checkPoint}");
                    return false;
                }
            }
            
            return true; // Path is clear
        }
        catch (Exception ex)
        {
            _debugLog($"PREDICTIVE IsDirectPathWalkable error: {ex.Message}");
            return true; // Assume walkable on error - better to move than get stuck
        }
    }

    /// <summary>
    /// EXPERIMENT 5: Direct Leader Clicking like Original AreWeThereYet
    /// </summary>
    public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
    {
        try
        {
            // STEP 1: DETECT LEADER FROM EXILECORE MEMORY
            var leaderResult = DetectLeaderFromMemory(currentPos);
            
            if (leaderResult.Found)
            {
                _debugLog($"DIRECT LEADER DETECTED: Distance={leaderResult.Distance:F1}, InRange={leaderResult.InDirectClickRange}");
                
                // DIRECT LEADER CLICKING - Like original AreWeThereYet!
                if (leaderResult.InDirectClickRange)
                {
                    _debugLog($"DIRECT LEADER CLICK: Clicking directly on leader position!");
                    return leaderResult.Position; // Click EXACTLY where leader is - no waypoints!
                }
                else
                {
                    // Leader detected but distant - take CONFIDENT large steps toward them
                    _debugLog($"CONFIDENT STEP: Large step toward detected leader");
                    var direction = Vector3.Normalize(leaderResult.Position - currentPos);
                    var stepSize = ConfidentStepSize; // 150f - aggressive movement
                    return currentPos + (direction * Math.Min(stepSize, leaderResult.Distance));
                }
            }
            
            // STEP 2: NO LEADER DETECTED - FALLBACK TO TRADITIONAL PATHFINDING
            _debugLog($"NO LEADER DETECTED: Using traditional pathfinding to target position");
            
            var distance = Vector3.Distance(currentPos, targetPos);
            
            // If very close, move directly to target
            if (distance <= 40f) return targetPos;
            
            var direction = Vector3.Normalize(targetPos - currentPos);
            
            // Use standard steps when no leader detected
            float stepSize = CalculateAggressiveStepSize(distance);
            stepSize = Math.Min(stepSize, distance);
            
            var candidatePoint = currentPos + (direction * stepSize);
            
            _debugLog($"FALLBACK: Distance={distance:F1}, StepSize={stepSize:F1}");
            
            // USE EXILECORE MEMORY - Check for actual obstacles
            if (!HasMajorObstacleAt(candidatePoint))
            {
                return candidatePoint;
            }
            
            // Try simple obstacle avoidance
            float[] avoidanceAngles = { 30f, -30f, 45f, -45f };
            
            foreach (var angle in avoidanceAngles)
            {
                var avoidancePoint = currentPos + (RotateVector2D(direction, angle) * stepSize);
                if (!HasMajorObstacleAt(avoidancePoint))
                {
                    _debugLog($"FALLBACK: Using {angle}° avoidance angle");
                    return avoidancePoint;
                }
            }
            
            // Final fallback - small step forward
            var microStep = Math.Min(MinStepSize * 0.5f, 30f);
            var microPoint = currentPos + (direction * microStep);
            _debugLog($"FALLBACK: Micro-step {microStep:F1} units");
            return microPoint;
        }
        catch (Exception ex)
        {
            _debugLog($"DIRECT LEADER CLICK ERROR: {ex.Message}");
            return targetPos; // Fallback to direct target
        }
    }

    /// <summary>
    /// Check if position is walkable - NOW USES EXILECORE MEMORY for obstacle detection
    /// </summary>
    public bool IsWalkable(Vector3 position)
    {
        try
        {
            var player = _gameController?.Player;
            if (player == null) return false;

            var currentPos = player.Pos;
            var distance = Vector3.Distance(currentPos, position);
            var heightDiff = Math.Abs(position.Z - currentPos.Z);
            
            // Basic distance/height checks
            if (distance > 1200f || heightDiff > 200f)
                return false;
                
            // Use ExileCore memory to check for actual obstacles
            return !HasMajorObstacleAt(position);
        }
        catch (Exception ex)
        {
            _debugLog($"PREDICTIVE IsWalkable error: {ex.Message}");
            return true; // Assume walkable on error
        }
    }

    /// <summary>
    /// Update leader position history for predictive analysis
    /// </summary>
    private void UpdateLeaderHistory(Vector3 currentLeaderPos)
    {
        var now = DateTime.UtcNow;
        
        // Only update if enough time has passed to avoid noise
        if ((now - _lastUpdate).TotalMilliseconds < 100) return; // Max 10 updates per second
        
        var snapshot = new LeaderSnapshot
        {
            Position = currentLeaderPos,
            Timestamp = now
        };
        
        _leaderHistory.Enqueue(snapshot);
        
        // Keep history size manageable
        while (_leaderHistory.Count > MaxHistorySize)
        {
            _leaderHistory.Dequeue();
        }
        
        _lastUpdate = now;
    }

    /// <summary>
    /// Predict where the leader will be based on movement history
    /// KEY INTELLIGENCE: This is what makes the system smart
    /// </summary>
    private Vector3 PredictLeaderPosition()
    {
        if (_leaderHistory.Count < 2)
        {
            // Not enough history - use current position
            return _leaderHistory.LastOrDefault()?.Position ?? Vector3.Zero;
        }
        
        try
        {
            var recent = _leaderHistory.ToArray();
            var latest = recent[recent.Length - 1];
            var previous = recent[recent.Length - 2];
            
            // Calculate velocity from recent movement
            var timeDelta = (float)(latest.Timestamp - previous.Timestamp).TotalSeconds;
            if (timeDelta <= 0) return latest.Position;
            
            var velocity = (latest.Position - previous.Position) / timeDelta;
            
            // Simple physics prediction: current position + velocity * time
            var predictedPosition = latest.Position + velocity * PredictionTime;
            
            _lastPredictedPosition = predictedPosition;
            
            var speed = velocity.Length();
            _debugLog($"PREDICTIVE: Leader velocity={speed:F1} units/sec, predicted offset={Vector3.Distance(latest.Position, predictedPosition):F1}");
            
            return predictedPosition;
        }
        catch (Exception ex)
        {
            _debugLog($"PREDICTIVE: Prediction error: {ex.Message}");
            return _leaderHistory.LastOrDefault()?.Position ?? Vector3.Zero;
        }
    }

    /// <summary>
    /// Calculate optimal interception point - where to move to meet the leader
    /// </summary>
    private Vector3 CalculateInterceptionPoint(Vector3 followerPos, Vector3 predictedLeaderPos)
    {
        var directDistance = Vector3.Distance(followerPos, predictedLeaderPos);
        
        // For close targets, move directly to predicted position
        if (directDistance < 200f)
        {
            return predictedLeaderPos;
        }
        
        // For distant targets, move to a point that intercepts the leader's path
        // This is a simplified interception calculation
        var direction = Vector3.Normalize(predictedLeaderPos - followerPos);
        var interceptDistance = Math.Min(directDistance * 0.8f, AggressiveStepSize);
        
        return followerPos + (direction * interceptDistance);
    }

    /// <summary>
    /// Choose movement strategy based on distance and context
    /// </summary>
    private Vector3 ChooseMovementStrategy(Vector3 start, Vector3 interceptionPoint, Vector3 actualTarget)
    {
        var interceptionDistance = Vector3.Distance(start, interceptionPoint);
        var directDistance = Vector3.Distance(start, actualTarget);
        
        // If interception point is much closer, use it (leader is moving toward us)
        if (interceptionDistance < directDistance * 0.7f)
        {
            _debugLog($"PREDICTIVE: Using INTERCEPTION strategy");
            return interceptionPoint;
        }
        
        // Otherwise, move toward predicted position but not too aggressively
        _debugLog($"PREDICTIVE: Using DIRECT CHASE strategy");
        var direction = Vector3.Normalize(interceptionPoint - start);
        var stepSize = CalculateAggressiveStepSize(interceptionDistance);
        
        return start + (direction * stepSize);
    }

    /// <summary>
    /// Calculate aggressive step size based on distance - like original AreWeThereYet
    /// </summary>
    private float CalculateAggressiveStepSize(float distance)
    {
        float calculated;
        
        // Use larger steps for distant targets, but never tiny steps
        if (distance > 300f) 
        {
            calculated = AggressiveStepSize;  // 180 units for very far
            _debugLog($"STEP CALC: Distance {distance:F1} > 300 -> AGGRESSIVE {calculated}");
        }
        else if (distance > 150f) 
        {
            calculated = StandardStepSize;    // 120 units for medium
            _debugLog($"STEP CALC: Distance {distance:F1} > 150 -> STANDARD {calculated}");
        }
        else 
        {
            calculated = Math.Max(MinStepSize, distance * 0.6f);   // At least 80 units for close
            _debugLog($"STEP CALC: Distance {distance:F1} <= 150 -> MIN({MinStepSize}, {distance * 0.6f:F1}) = {calculated}");
        }
        
        return calculated;
    }

    /// <summary>
    /// Very simple safety check - minimal overhead, maximum permissiveness
    /// </summary>
    private bool IsBasicallySafe(Vector3 position)
    {
        try
        {
            var player = _gameController?.Player;
            if (player?.Pos == null) return true;

            var distance = Vector3.Distance(player.Pos, position);
            var heightDiff = Math.Abs(position.Z - player.Pos.Z);
            
            // Very permissive limits - only block truly unreachable positions
            return distance <= 3000f && heightDiff <= 400f;
        }
        catch
        {
            // If any error, assume it's safe - better to move than get stuck
            return true;
        }
    }

    /// <summary>
    /// Simple 2D vector rotation for obstacle avoidance
    /// </summary>
    private Vector3 RotateVector2D(Vector3 vector, float angleInDegrees)
    {
        var radians = angleInDegrees * (Math.PI / 180.0);
        var cos = (float)Math.Cos(radians);
        var sin = (float)Math.Sin(radians);
        
        return new Vector3(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos,
            vector.Z
        );
    }

    /// <summary>
    /// Leader position snapshot for tracking history
    /// </summary>
    private class LeaderSnapshot
    {
        public Vector3 Position { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// EXPERIMENT 5: Leader detection result from ExileCore memory
    /// </summary>
    private class LeaderDetectionResult
    {
        public bool Found { get; set; }
        public Vector3 Position { get; set; }
        public float Distance { get; set; }
        public bool InDirectClickRange { get; set; }
        public Entity Entity { get; set; }
    }
    
    /// <summary>
    /// EXPERIMENT 5: Detect leader from ExileCore memory - smart leader detection
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

            // Look for party leader or nearest player entity
            Entity bestLeaderCandidate = null;
            float closestDistance = float.MaxValue;
            
            foreach (var entity in entities)
            {
                if (entity == null || !entity.IsValid) continue;
                
                // Check if this is a player entity (potential leader)
                var playerComponent = entity.GetComponent<Player>();
                if (playerComponent == null) continue;
                
                // Skip self
                if (entity.Id == player.Id) continue;
                
                var entityPos = entity.Pos;
                var distance = Vector3.Distance(currentPos, entityPos);
                
                // Must be within detection range
                if (distance > LeaderDetectionRange) continue;
                
                // Prefer closer entities as leader candidates
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
                    InDirectClickRange = distance <= DirectClickRange,
                    Entity = bestLeaderCandidate
                };
            }
            
            // Check if we have recent leader data (within age limit)
            var timeSinceLastUpdate = (DateTime.UtcNow - _lastLeaderUpdate).TotalSeconds;
            if (_currentLeader != null && timeSinceLastUpdate <= MaxLeaderAge)
            {
                var distance = Vector3.Distance(currentPos, _lastKnownLeaderPos);
                _debugLog($"USING CACHED LEADER: Age {timeSinceLastUpdate:F1}s, Distance {distance:F1}");
                
                return new LeaderDetectionResult
                {
                    Found = true,
                    Position = _lastKnownLeaderPos,
                    Distance = distance,
                    InDirectClickRange = distance <= DirectClickRange,
                    Entity = _currentLeader
                };
            }
            
            return new LeaderDetectionResult { Found = false };
        }
        catch (Exception ex)
        {
            _debugLog($"DetectLeaderFromMemory error: {ex.Message}");
            return new LeaderDetectionResult { Found = false };
        }
    }
    
    /// <summary>
    /// NEW: Check for major obstacles using ExileCore memory APIs
    /// This is the key fix - actually use the game's memory data!
    /// </summary>
    private bool HasMajorObstacleAt(Vector3 position)
    {
        try
        {
            var entities = _gameController?.EntityListWrapper?.Entities;
            if (entities == null) return false;
            
            const float obstacleRadius = 35f; // Check area around position
            
            foreach (var entity in entities)
            {
                if (entity?.Pos == null || !entity.IsValid) continue;
                
                var distance = Vector3.Distance(position, entity.Pos);
                if (distance > obstacleRadius) continue;
                
                // Check for BLOCKING obstacles using entity types and components
                if (IsEntityBlocking(entity))
                {
                    _debugLog($"PREDICTIVE: Obstacle detected - {entity.Type} at {entity.Pos}");
                    return true;
                }
            }
            
            return false; // No blocking obstacles found
        }
        catch (Exception ex)
        {
            _debugLog($"PREDICTIVE HasMajorObstacleAt error: {ex.Message}");
            return false; // Assume no obstacles on error
        }
    }
    
    /// <summary>
    /// NEW: Determine if an entity is a blocking obstacle using ExileCore components
    /// </summary>
    private bool IsEntityBlocking(Entity entity)
    {
        try
        {
            // 1. Check entity type for obvious obstacles
            switch (entity.Type)
            {
                case EntityType.WorldItem:
                    // Most world items (chests, statues) are obstacles
                    return true;
                    
                case EntityType.Monster:
                    // Only block for large/stationary monsters
                    var life = entity.GetComponent<Life>();
                    if (life?.CurHP > 0) // Alive monsters
                    {
                        // Block only for large monsters or bosses
                        var render = entity.GetComponent<Render>();
                        return render?.Name?.Contains("boss", StringComparison.OrdinalIgnoreCase) == true;
                    }
                    return false; // Dead monsters don't block
                    
                case EntityType.Player:
                    return false; // Other players don't block movement
                    
                default:
                    break;
            }
            
            // 2. Check render component for walls/structures
            var renderComp = entity.GetComponent<Render>();
            if (renderComp?.Name != null)
            {
                var name = renderComp.Name.ToLowerInvariant();
                
                // Block for walls, doors (closed), large structures
                if (name.Contains("wall") || name.Contains("barrier") || 
                    name.Contains("pillar") || name.Contains("column"))
                {
                    return true;
                }
                
                // Doors - assume closed doors block (simple approach)
                if (name.Contains("door"))
                {
                    // TODO: Proper door state detection later
                    return false; // For now, assume doors are passable
                }
            }
            
            // 3. Check metadata for additional obstacle types
            var metadata = entity.Metadata;
            if (metadata?.Contains("obstacle", StringComparison.OrdinalIgnoreCase) == true ||
                metadata?.Contains("wall", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
            
            return false; // Entity doesn't block movement
        }
        catch
        {
            return false; // Assume non-blocking on error
        }
    }
}