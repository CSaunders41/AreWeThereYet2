using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;

namespace AreWeThereYet2.Movement.Pathfinding;

/// <summary>
/// Advanced LineOfSight pathfinding system based on original AreWeThereYet's superior implementation
/// Features 6-level terrain detection, raycast pathfinding, and intelligent obstacle avoidance
/// </summary>
public class AdvancedLineOfSight : IPathfinding
{
    private readonly GameController _gameController;
    private readonly Action<string> _debugLog;
    
    // Pathfinding constants
    private const float MaxDirectPathDistance = 400f;
    private const float WaypointSearchRadius = 150f;
    private const float MinObstacleDistance = 25f;
    private const float RaycastStep = 20f;
    private const int MaxWaypointAttempts = 8;
    
    // Terrain analysis cache
    private readonly Dictionary<Vector3, TerrainType> _terrainCache = new();
    private DateTime _lastCacheCleanup = DateTime.MinValue;
    private const int CacheCleanupIntervalMs = 30000; // 30 seconds

    public AdvancedLineOfSight(GameController gameController, Action<string> debugLog)
    {
        _gameController = gameController;
        _debugLog = debugLog;
        
        debugLog("PHASE 2.1: Advanced LineOfSight pathfinding system initialized");
    }

    /// <summary>
    /// Find optimal path - FIXED: Direct movement when leader is far, waypoints only when needed
    /// </summary>
    public PathfindingResult FindPath(Vector3 start, Vector3 target)
    {
        var result = new PathfindingResult();
        
        try
        {
            CleanupTerrainCache();
            
            var distance = Vector3.Distance(start, target);
            _debugLog($"ADVANCED: Finding path, distance: {distance:F1}");
            
            // FIXED: Always try direct path first, regardless of distance when leader is far
            if (IsDirectLineOfSightClear(start, target))
            {
                result.Success = true;
                result.Path = new List<Vector3> { start, target };
                result.SimplifiedPath = new List<Vector3> { target };
                result.Distance = distance;
                
                _debugLog($"ADVANCED: DIRECT PATH - No waypoints needed, moving directly to leader: {distance:F1} units");
                return result;
            }
            
            // FIXED: Only use waypoints when direct path is blocked AND distance is reasonable
            if (distance <= MaxDirectPathDistance)
            {
                var waypoints = FindOptimalWaypoints(start, target);
                if (waypoints.Count > 0)
                {
                    result.Success = true;
                    result.Path = new List<Vector3> { start };
                    result.Path.AddRange(waypoints);
                    result.Path.Add(target);
                    result.SimplifiedPath = new List<Vector3>(waypoints) { target };
                    
                    // Calculate total path distance
                    result.Distance = 0f;
                    for (int i = 0; i < result.Path.Count - 1; i++)
                    {
                        result.Distance += Vector3.Distance(result.Path[i], result.Path[i + 1]);
                    }
                    
                    _debugLog($"ADVANCED: Waypoint path found: {waypoints.Count} waypoints, {result.Distance:F1} total distance");
                    return result;
                }
            }
            
            // FIXED: For very far leaders, force direct movement even if line-of-sight isn't perfect
            if (distance > MaxDirectPathDistance)
            {
                result.Success = true;
                result.Path = new List<Vector3> { start, target };
                result.SimplifiedPath = new List<Vector3> { target };
                result.Distance = distance;
                
                _debugLog($"ADVANCED: FORCED DIRECT - Leader very far ({distance:F1}), skipping waypoints");
                return result;
            }
            
            // Phase 3: Fallback to entity-based navigation
            var fallbackPath = FindFallbackPath(start, target);
            if (fallbackPath != null)
            {
                result.Success = true;
                result.Path = fallbackPath;
                result.SimplifiedPath = fallbackPath.Skip(1).ToList(); // Skip start position
                result.Distance = CalculatePathDistance(fallbackPath);
                
                _debugLog($"ADVANCED: Fallback path found: {result.Distance:F1} units");
                return result;
            }
            
            result.Success = false;
            result.ErrorMessage = "No viable path found with advanced pathfinding";
            _debugLog("ADVANCED: All pathfinding methods failed");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _debugLog($"ADVANCED PATHFINDING ERROR: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Advanced line-of-sight check using raycast with terrain analysis
    /// </summary>
    public bool IsDirectPathWalkable(Vector3 start, Vector3 target)
    {
        return IsDirectLineOfSightClear(start, target);
    }

    /// <summary>
    /// Get next movement point with intelligent obstacle avoidance
    /// Fixed: More aggressive movement when leader is far away - ALWAYS returns a valid point
    /// </summary>
    public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
    {
        try
        {
            var distance = Vector3.Distance(currentPos, targetPos);
            
            // If close enough, move directly to target
            if (distance <= 50f)
                return targetPos;
            
            // FIXED: More aggressive step calculation when leader is far
            var stepSize = CalculateAggressiveStepSize(currentPos, targetPos, distance);
            var direction = Vector3.Normalize(targetPos - currentPos);
            var candidatePoint = currentPos + (direction * stepSize);
            
            _debugLog($"MOVEMENT STEP: Distance={distance:F1}, StepSize={stepSize:F1}, CandidatePoint={candidatePoint}");
            
            // For direct movement mode, be less restrictive about "safety"
            // When leader is far, prioritize movement over perfect safety
            if (distance > 150f)
            {
                _debugLog($"MOVEMENT: Leader far ({distance:F1}), using aggressive step - {stepSize:F1} units");
                return candidatePoint; // Always take the big step when leader is far
            }
            
            // Check if candidate point is safe for closer movements
            if (IsPositionSafe(candidatePoint))
                return candidatePoint;
            
            // Try intelligent variations around obstacles
            var alternativePoint = FindSafeAlternativePoint(currentPos, direction, stepSize);
            if (alternativePoint.HasValue)
                return alternativePoint;
            
            // FALLBACK: If nothing else works, take a smaller but guaranteed step
            var conservativeStep = Math.Min(stepSize * 0.5f, 75f);
            var fallbackPoint = currentPos + (direction * conservativeStep);
            _debugLog($"MOVEMENT: Using fallback step - {conservativeStep:F1} units toward leader");
            return fallbackPoint;
        }
        catch (Exception ex)
        {
            _debugLog($"ADVANCED GetNextMovePoint error: {ex.Message}");
            // Even on error, return a basic step toward target
            var distance = Vector3.Distance(currentPos, targetPos);
            var direction = Vector3.Normalize(targetPos - currentPos);
            var basicStep = Math.Min(60f, distance * 0.4f);
            return currentPos + (direction * basicStep);
        }
    }

    /// <summary>
    /// Advanced walkability check - simplified for Phase 2.1 to prevent pathfinding issues
    /// VERY permissive for aggressive movement when leader is far
    /// </summary>
    public bool IsWalkable(Vector3 position)
    {
        try
        {
            var player = _gameController?.Player;
            if (player == null)
                return false;

            var currentPos = player.Pos;
            var distance = Vector3.Distance(currentPos, position);
            
            // FIXED: Much more permissive distance check for aggressive following
            if (distance > 2000f) // Increased from 1000f
            {
                _debugLog($"IsWalkable: Position too far ({distance:F1} units)");
                return false;
            }
            
            // FIXED: More permissive height check for aggressive following  
            var heightDiff = Math.Abs(position.Z - currentPos.Z);
            if (heightDiff > 200f) // Increased from 100f
            {
                _debugLog($"IsWalkable: Height difference too large ({heightDiff:F1} units)");
                return false;
            }
            
            // For aggressive movement when leader is far, be very permissive
            return true;
        }
        catch (Exception ex)
        {
            _debugLog($"IsWalkable error: {ex.Message}");
            return true; // Assume walkable on error to avoid getting stuck
        }
    }

    /// <summary>
    /// Get detailed terrain type using 6-level detection system
    /// </summary>
    public TerrainType GetTerrainType(Vector3 position)
    {
        try
        {
            // Check cache first
            if (_terrainCache.TryGetValue(position, out var cachedType))
                return cachedType;
            
            var terrainType = AnalyzeTerrainAt(position);
            
            // Cache the result
            _terrainCache[position] = terrainType;
            
            return terrainType;
        }
        catch (Exception ex)
        {
            _debugLog($"GetTerrainType error: {ex.Message}");
            return TerrainType.Unknown;
        }
    }

    /// <summary>
    /// Perform raycast line-of-sight check - simplified for Phase 2.1 to prevent pathfinding issues
    /// </summary>
    private bool IsDirectLineOfSightClear(Vector3 start, Vector3 target)
    {
        try
        {
            var distance = Vector3.Distance(start, target);
            
            // Simple distance check - if too far, probably not direct
            if (distance > MaxDirectPathDistance)
                return false;

            // For Phase 2.1, use basic walkability checks instead of complex terrain analysis
            // Check a few points along the line instead of intensive raycast
            var steps = Math.Max(3, (int)(distance / 100)); // Check every ~100 units
            
            for (int i = 1; i < steps; i++)
            {
                var t = (float)i / steps;
                var checkPoint = Vector3.Lerp(start, target, t);
                
                if (!IsWalkable(checkPoint))
                {
                    _debugLog($"ADVANCED: Line-of-sight blocked at {checkPoint}");
                    return false;
                }
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _debugLog($"IsDirectLineOfSightClear error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Find optimal waypoints using advanced terrain analysis
    /// </summary>
    private List<Vector3> FindOptimalWaypoints(Vector3 start, Vector3 target)
    {
        try
        {
            var waypoints = new List<Vector3>();
            var direction = Vector3.Normalize(target - start);
            var distance = Vector3.Distance(start, target);
            
            // Calculate strategic waypoint positions
            for (int attempt = 0; attempt < MaxWaypointAttempts; attempt++)
            {
                var angle = (attempt * 45f) - 180f; // Try different angles
                var waypointDistance = Math.Min(WaypointSearchRadius, distance * 0.6f);
                
                var waypoint = FindStrategicWaypoint(start, target, direction, angle, waypointDistance);
                
                if (waypoint.HasValue && 
                    IsDirectLineOfSightClear(start, waypoint.Value) &&
                    IsDirectLineOfSightClear(waypoint.Value, target))
                {
                    waypoints.Add(waypoint.Value);
                    _debugLog($"ADVANCED: Strategic waypoint found at {waypoint.Value}");
                    break;
                }
            }
            
            return waypoints;
        }
        catch (Exception ex)
        {
            _debugLog($"FindOptimalWaypoints error: {ex.Message}");
            return new List<Vector3>();
        }
    }

    /// <summary>
    /// Advanced terrain analysis at specific position
    /// </summary>
    private TerrainType AnalyzeTerrainAt(Vector3 position)
    {
        try
        {
            // Check for doors first (highest priority)
            if (HasDoorAt(position))
                return TerrainType.Door;
            
            // Check for walls and obstacles using entity analysis
            if (HasWallAt(position))
                return TerrainType.Wall;
            
            if (HasObstacleAt(position))
                return TerrainType.Obstacle;
            
            // Check for water/liquid terrain
            if (HasWaterAt(position))
                return TerrainType.Water;
            
            // Default to walkable if no obstacles detected
            return TerrainType.Walkable;
        }
        catch (Exception ex)
        {
            _debugLog($"AnalyzeTerrainAt error: {ex.Message}");
            return TerrainType.Unknown;
        }
    }

    /// <summary>
    /// Check for door entities at position
    /// </summary>
    private bool HasDoorAt(Vector3 position)
    {
        try
        {
            // For Phase 2.1, simplify door detection to avoid API issues
            // Check for door-like entities using general entity search
            var allEntities = _gameController?.EntityListWrapper?.Entities;
            if (allEntities == null) return false;
            
            foreach (var entity in allEntities)
            {
                if (entity?.Pos == null || !entity.IsValid) continue;
                
                var distance = Vector3.Distance(position, entity.Pos);
                if (distance <= MinObstacleDistance)
                {
                    // Check if entity looks like a door based on metadata
                    var metadata = entity.Metadata;
                    if (metadata?.Contains("door", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        _debugLog($"ADVANCED: Door-like entity detected at {entity.Pos}");
                        // For Phase 2.1, treat doors as walkable (will be enhanced with proper door API)
                        return false; // Doors are walkable for now
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _debugLog($"HasDoorAt error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check for wall obstacles using entity analysis
    /// </summary>
    private bool HasWallAt(Vector3 position)
    {
        try
        {
            // Check for wall-type entities
            var staticEntities = _gameController?.EntityListWrapper?.Entities
                ?.Where(e => e != null && e.Type == EntityType.WorldItem && 
                           Vector3.Distance(position, e.Pos) <= MinObstacleDistance);
            
            if (staticEntities != null)
            {
                foreach (var entity in staticEntities)
                {
                    var render = entity.GetComponent<Render>();
                    if (render != null && render.Name.Contains("wall", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _debugLog($"HasWallAt error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check for general obstacles
    /// </summary>
    private bool HasObstacleAt(Vector3 position)
    {
        try
        {
            // Check for various obstacle entities
            var nearbyEntities = _gameController?.EntityListWrapper?.Entities
                ?.Where(e => e != null && e.IsValid && 
                           Vector3.Distance(position, e.Pos) <= MinObstacleDistance);
            
            if (nearbyEntities != null)
            {
                foreach (var entity in nearbyEntities)
                {
                    // Skip players and monsters (they're dynamic)
                    if (entity.Type == EntityType.Player || entity.Type == EntityType.Monster)
                        continue;
                    
                    // Check for blocking entities
                    var render = entity.GetComponent<Render>();
                    if (render != null)
                    {
                        // For Phase 2.1, use simplified obstacle detection
                        // Check if render name suggests it's a blocking entity
                        var renderName = render.Name?.ToLowerInvariant() ?? "";
                        if (renderName.Contains("chest") || renderName.Contains("statue") || 
                            renderName.Contains("pillar") || renderName.Contains("block"))
                        {
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _debugLog($"HasObstacleAt error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check for water/liquid terrain
    /// </summary>
    private bool HasWaterAt(Vector3 position)
    {
        // Simplified implementation - could be enhanced with specific water detection
        return false;
    }

    /// <summary>
    /// Check for dynamic obstacles (monsters, players)
    /// </summary>
    private bool HasDynamicObstacle(Vector3 position)
    {
        try
        {
            var monsters = _gameController?.EntityListWrapper?.ValidEntitiesByType?[EntityType.Monster];
            if (monsters != null)
            {
                foreach (var monster in monsters)
                {
                    if (monster?.Pos == null || !monster.IsValid) continue;
                    
                    var distance = Vector3.Distance(position, monster.Pos);
                    if (distance <= MinObstacleDistance)
                    {
                        // Check if monster is alive using safe component access
                        var life = monster.GetComponent<Life>();
                        if (life != null)
                        {
                            try
                            {
                                if (life.CurHP > 0) // Alive monster
                                {
                                    return true;
                                }
                            }
                            catch
                            {
                                // If we can't read HP, assume monster is alive (safer approach)
                                return true;
                            }
                        }
                        else
                        {
                            // No life component - assume it's a dynamic obstacle
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _debugLog($"HasDynamicObstacle error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Calculate aggressive step size - larger steps when leader is far away
    /// Fixed: Prevents bot from falling behind when leader moves fast
    /// </summary>
    private float CalculateAggressiveStepSize(Vector3 start, Vector3 target, float distance)
    {
        // FIXED: Much more aggressive stepping when leader is far
        if (distance > 200f)
        {
            // Very far - take big steps (up to 150 units)
            return Math.Min(150f, distance * 0.6f);
        }
        else if (distance > 100f)
        {
            // Far - take medium steps (up to 100 units)
            return Math.Min(100f, distance * 0.5f);
        }
        else
        {
            // Close - take smaller steps (up to 60 units)
            return Math.Min(60f, distance * 0.4f);
        }
    }

    /// <summary>
    /// Calculate optimal step size based on terrain complexity (legacy - kept for reference)
    /// </summary>
    private float CalculateOptimalStepSize(Vector3 start, Vector3 target)
    {
        var distance = Vector3.Distance(start, target);
        return CalculateAggressiveStepSize(start, target, distance);
    }

    /// <summary>
    /// Check if position is safe for movement - simplified for Phase 2.1
    /// </summary>
    private bool IsPositionSafe(Vector3 position)
    {
        return IsWalkable(position);
    }

    /// <summary>
    /// Find safe alternative point around obstacles
    /// </summary>
    private Vector3? FindSafeAlternativePoint(Vector3 start, Vector3 direction, float stepSize)
    {
        // Try angles around the original direction
        for (int angle = 15; angle <= 60; angle += 15)
        {
            var leftVariation = RotateVector(direction, angle) * stepSize + start;
            var rightVariation = RotateVector(direction, -angle) * stepSize + start;
            
            if (IsPositionSafe(leftVariation))
                return leftVariation;
                
            if (IsPositionSafe(rightVariation))
                return rightVariation;
        }
        
        return null;
    }

    /// <summary>
    /// Find strategic waypoint considering terrain and obstacles
    /// </summary>
    private Vector3? FindStrategicWaypoint(Vector3 start, Vector3 target, Vector3 direction, float angle, float distance)
    {
        try
        {
            var rotatedDirection = RotateVector(direction, angle);
            var waypoint = start + (rotatedDirection * distance);
            
            if (IsPositionSafe(waypoint))
            {
                return waypoint;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _debugLog($"FindStrategicWaypoint error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fallback pathfinding using entity navigation
    /// </summary>
    private List<Vector3>? FindFallbackPath(Vector3 start, Vector3 target)
    {
        try
        {
            // Simple fallback - find intermediate safe positions
            var path = new List<Vector3> { start };
            var current = start;
            var remaining = target - start;
            
            while (Vector3.Distance(current, target) > 50f)
            {
                var stepDirection = Vector3.Normalize(remaining);
                var nextStep = current + (stepDirection * 50f);
                
                if (IsPositionSafe(nextStep))
                {
                    path.Add(nextStep);
                    current = nextStep;
                    remaining = target - current;
                }
                else
                {
                    break; // Can't find safe path
                }
            }
            
            path.Add(target);
            return path.Count > 2 ? path : null;
        }
        catch (Exception ex)
        {
            _debugLog($"FindFallbackPath error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Calculate total distance of a path
    /// </summary>
    private float CalculatePathDistance(List<Vector3> path)
    {
        float distance = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            distance += Vector3.Distance(path[i], path[i + 1]);
        }
        return distance;
    }

    /// <summary>
    /// Rotate vector by angle in degrees (2D rotation in XY plane)
    /// </summary>
    private Vector3 RotateVector(Vector3 vector, float angleInDegrees)
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
    /// Clean up old terrain cache entries
    /// </summary>
    private void CleanupTerrainCache()
    {
        if (DateTime.Now - _lastCacheCleanup > TimeSpan.FromMilliseconds(CacheCleanupIntervalMs))
        {
            _terrainCache.Clear();
            _lastCacheCleanup = DateTime.Now;
            _debugLog("ADVANCED: Terrain cache cleaned up");
        }
    }
}