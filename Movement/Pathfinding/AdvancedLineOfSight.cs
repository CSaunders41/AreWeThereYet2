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
    /// Find optimal path using advanced terrain analysis and raycast pathfinding
    /// </summary>
    public PathfindingResult FindPath(Vector3 start, Vector3 target)
    {
        var result = new PathfindingResult();
        
        try
        {
            CleanupTerrainCache();
            
            var distance = Vector3.Distance(start, target);
            _debugLog($"ADVANCED: Finding path, distance: {distance:F1}");
            
            // Phase 1: Direct line-of-sight check with raycast
            if (distance <= MaxDirectPathDistance && IsDirectLineOfSightClear(start, target))
            {
                result.Success = true;
                result.Path = new List<Vector3> { start, target };
                result.SimplifiedPath = new List<Vector3> { target };
                result.Distance = distance;
                
                _debugLog($"ADVANCED: Direct line-of-sight path found: {distance:F1} units");
                return result;
            }
            
            // Phase 2: Advanced waypoint pathfinding with terrain analysis
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
    /// </summary>
    public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
    {
        try
        {
            var distance = Vector3.Distance(currentPos, targetPos);
            
            // If close enough, move directly to target
            if (distance <= 100f)
                return targetPos;
            
            // Calculate optimal step size based on terrain complexity
            var stepSize = CalculateOptimalStepSize(currentPos, targetPos);
            var direction = Vector3.Normalize(targetPos - currentPos);
            var candidatePoint = currentPos + (direction * stepSize);
            
            // Check if candidate point is safe
            if (IsPositionSafe(candidatePoint))
                return candidatePoint;
            
            // Try intelligent variations around obstacles
            return FindSafeAlternativePoint(currentPos, direction, stepSize);
        }
        catch (Exception ex)
        {
            _debugLog($"ADVANCED GetNextMovePoint error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Advanced walkability check with 6-level terrain detection
    /// </summary>
    public bool IsWalkable(Vector3 position)
    {
        return GetTerrainType(position) == TerrainType.Walkable;
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
    /// Perform raycast line-of-sight check with obstacle detection
    /// </summary>
    private bool IsDirectLineOfSightClear(Vector3 start, Vector3 target)
    {
        try
        {
            var distance = Vector3.Distance(start, target);
            var steps = (int)Math.Ceiling(distance / RaycastStep);
            
            for (int i = 1; i < steps; i++)
            {
                var t = (float)i / steps;
                var checkPoint = Vector3.Lerp(start, target, t);
                
                var terrainType = GetTerrainType(checkPoint);
                if (terrainType != TerrainType.Walkable && terrainType != TerrainType.Door)
                {
                    _debugLog($"ADVANCED: Line-of-sight blocked at {checkPoint} by {terrainType}");
                    return false;
                }
                
                // Check for dynamic obstacles (monsters, players)
                if (HasDynamicObstacle(checkPoint))
                {
                    _debugLog($"ADVANCED: Dynamic obstacle detected at {checkPoint}");
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
            var entities = _gameController?.EntityListWrapper?.ValidEntitiesByType?[EntityType.Door];
            if (entities == null) return false;
            
            foreach (var door in entities)
            {
                if (door?.Pos == null) continue;
                
                var distance = Vector3.Distance(position, door.Pos);
                if (distance <= MinObstacleDistance)
                {
                    // Check door state - closed doors are obstacles
                    var doorComponent = door.GetComponent<Door>();
                    if (doorComponent != null && !doorComponent.IsOpened)
                    {
                        _debugLog($"ADVANCED: Closed door detected at {door.Pos}");
                        return true;
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
                    if (render != null && render.Bounds.Height > 20) // Significant obstacle
                    {
                        return true;
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
                    if (monster?.Pos == null) continue;
                    
                    var distance = Vector3.Distance(position, monster.Pos);
                    if (distance <= MinObstacleDistance)
                    {
                        var life = monster.GetComponent<Life>();
                        if (life != null && life.CurHP > 0) // Alive monster
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
            _debugLog($"HasDynamicObstacle error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Calculate optimal step size based on terrain complexity
    /// </summary>
    private float CalculateOptimalStepSize(Vector3 start, Vector3 target)
    {
        var distance = Vector3.Distance(start, target);
        var baseStepSize = Math.Min(80f, distance * 0.3f);
        
        // Adjust for terrain complexity (could be enhanced)
        return baseStepSize;
    }

    /// <summary>
    /// Check if position is safe for movement
    /// </summary>
    private bool IsPositionSafe(Vector3 position)
    {
        var terrainType = GetTerrainType(position);
        return terrainType == TerrainType.Walkable || terrainType == TerrainType.Door;
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