using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
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
    
    // Movement constants - Aggressive like original AreWeThereYet
    private const float MinStepSize = 80f;           // Never take tiny steps that cause stuck scenarios
    private const float StandardStepSize = 120f;    // Default large step size
    private const float AggressiveStepSize = 180f;  // For far leaders or fast movement
    private const float PredictionTime = 2.0f;      // Predict 2 seconds ahead
    private const int MaxHistorySize = 10;          // Keep last 10 positions for analysis
    private const float AvoidanceAngle = 30f;       // Simple left/right avoidance
    
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
    /// Check if direct path is walkable - simplified version focused on major obstacles only
    /// </summary>
    public bool IsDirectPathWalkable(Vector3 start, Vector3 target)
    {
        try
        {
            var distance = Vector3.Distance(start, target);
            
            // For aggressive following, be very permissive about "walkable"
            // Only block truly impossible paths
            if (distance > 2000f) return false; // Ridiculously far
            
            var heightDiff = Math.Abs(target.Z - start.Z);
            if (heightDiff > 300f) return false; // Massive height difference
            
            // For predictive movement, assume most paths are walkable
            // Let obstacle avoidance handle specific issues
            return true;
        }
        catch
        {
            return true; // Assume walkable on error - better to move than get stuck
        }
    }

    /// <summary>
    /// Get next movement point with large, aggressive steps
    /// </summary>
    public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
    {
        try
        {
            var distance = Vector3.Distance(currentPos, targetPos);
            
            // If very close, move directly to target
            if (distance <= 40f) return targetPos;
            
            var direction = Vector3.Normalize(targetPos - currentPos);
            
            // Use large, aggressive steps like original AreWeThereYet
            float stepSize = CalculateAggressiveStepSize(distance);
            
            // Don't make steps larger than remaining distance
            stepSize = Math.Min(stepSize, distance);
            
            var candidatePoint = currentPos + (direction * stepSize);
            
            _debugLog($"PREDICTIVE: NextPoint Distance={distance:F1}, StepSize={stepSize:F1}, Target={candidatePoint}");
            
            // Simple safety check - minimal overhead
            if (IsBasicallySafe(candidatePoint))
            {
                return candidatePoint;
            }
            
            // Simple obstacle avoidance - try left/right angles
            var leftPoint = currentPos + (RotateVector2D(direction, AvoidanceAngle) * stepSize);
            if (IsBasicallySafe(leftPoint))
            {
                _debugLog($"PREDICTIVE: Using left avoidance angle");
                return leftPoint;
            }
            
            var rightPoint = currentPos + (RotateVector2D(direction, -AvoidanceAngle) * stepSize);
            if (IsBasicallySafe(rightPoint))
            {
                _debugLog($"PREDICTIVE: Using right avoidance angle");
                return rightPoint;
            }
            
            // Last resort: still use a decent sized step (minimum 80 units)
            var safeStepSize = Math.Max(MinStepSize, stepSize * 0.7f);
            var fallbackPoint = currentPos + (direction * safeStepSize);
            _debugLog($"PREDICTIVE: Fallback step size {safeStepSize:F1}");
            return fallbackPoint;
        }
        catch (Exception ex)
        {
            _debugLog($"PREDICTIVE: GetNextMovePoint error: {ex.Message}");
            // Even on error, take a decent sized step toward target
            var distance = Vector3.Distance(currentPos, targetPos);
            var direction = Vector3.Normalize(targetPos - currentPos);
            var emergencyStep = Math.Min(StandardStepSize, distance);
            return currentPos + (direction * emergencyStep);
        }
    }

    /// <summary>
    /// Simple walkability check - very permissive to avoid over-analysis
    /// </summary>
    public bool IsWalkable(Vector3 position)
    {
        return IsBasicallySafe(position);
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
        // Use larger steps for distant targets, but never tiny steps
        if (distance > 300f) return AggressiveStepSize;  // 180 units for very far
        if (distance > 150f) return StandardStepSize;    // 120 units for medium
        return Math.Max(MinStepSize, distance * 0.6f);   // At least 80 units for close
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
}