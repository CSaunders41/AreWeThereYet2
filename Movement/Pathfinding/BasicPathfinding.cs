using System;
using System.Collections.Generic;
using ExileCore;
using SharpDX;

namespace AreWeThereYet2.Movement.Pathfinding;

/// <summary>
/// Basic pathfinding implementation for Phase 2
/// Will be enhanced with LineOfSight integration in future phases
/// </summary>
public class BasicPathfinding : IPathfinding
{
    private readonly GameController _gameController;
    private readonly Action<string> _debugLog;

    public BasicPathfinding(GameController gameController, Action<string> debugLog)
    {
        _gameController = gameController;
        _debugLog = debugLog;
    }

    /// <summary>
    /// Find path from start to target (basic implementation)
    /// </summary>
    public PathfindingResult FindPath(Vector3 start, Vector3 target)
    {
        var result = new PathfindingResult();
        
        try
        {
            // For now, use direct path if walkable, otherwise try waypoint
            if (IsDirectPathWalkable(start, target))
            {
                result.Success = true;
                result.Path = new List<Vector3> { start, target };
                result.SimplifiedPath = new List<Vector3> { target };
                result.Distance = Vector3.Distance(start, target);
                
                _debugLog($"Direct path found: {result.Distance:F1} units");
                return result;
            }
            
            // Try simple waypoint pathfinding
            var waypoint = FindWaypoint(start, target);
            if (waypoint.HasValue)
            {
                result.Success = true;
                result.Path = new List<Vector3> { start, waypoint.Value, target };
                result.SimplifiedPath = new List<Vector3> { waypoint.Value, target };
                result.Distance = Vector3.Distance(start, waypoint.Value) + Vector3.Distance(waypoint.Value, target);
                
                _debugLog($"Waypoint path found: {result.Distance:F1} units via waypoint");
                return result;
            }
            
            result.Success = false;
            result.ErrorMessage = "No path found";
            _debugLog($"Pathfinding failed: No walkable path to target");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _debugLog($"Pathfinding error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Check if direct line to target is walkable (simplified)
    /// </summary>
    public bool IsDirectPathWalkable(Vector3 start, Vector3 target)
    {
        try
        {
            // Simple distance check - if too far, probably not direct
            var distance = Vector3.Distance(start, target);
            if (distance > 500f) // Reasonable maximum direct path distance
                return false;

            // Check for basic obstacles by sampling points along the line
            var steps = Math.Max(5, (int)(distance / 50)); // Sample every ~50 units
            
            for (int i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var samplePoint = Vector3.Lerp(start, target, t);
                
                if (!IsWalkable(samplePoint))
                {
                    _debugLog($"Direct path blocked at {samplePoint}");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _debugLog($"IsDirectPathWalkable error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get next movement point along path
    /// </summary>
    public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
    {
        try
        {
            var distance = Vector3.Distance(currentPos, targetPos);
            
            // If close enough, move directly to target
            if (distance <= 100f)
                return targetPos;
            
            // Move in steps toward target
            var direction = Vector3.Normalize(targetPos - currentPos);
            var stepSize = Math.Min(100f, distance * 0.3f); // 30% of distance or 100 units max
            var nextPoint = currentPos + (direction * stepSize);
            
            // Ensure next point is walkable
            if (IsWalkable(nextPoint))
                return nextPoint;
            
            // Try slight variations if direct point isn't walkable
            for (int angle = 15; angle <= 45; angle += 15)
            {
                var leftVariation = RotateVector(direction, angle) * stepSize + currentPos;
                var rightVariation = RotateVector(direction, -angle) * stepSize + currentPos;
                
                if (IsWalkable(leftVariation))
                    return leftVariation;
                    
                if (IsWalkable(rightVariation))
                    return rightVariation;
            }
            
            return null; // No walkable next point found
        }
        catch (Exception ex)
        {
            _debugLog($"GetNextMovePoint error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Check if position is walkable (basic terrain detection)
    /// </summary>
    public bool IsWalkable(Vector3 position)
    {
        try
        {
            // Basic walkability check using ExileCore terrain data
            // This will be enhanced with LineOfSight integration later
            
            var terrain = _gameController?.Game?.IngameState?.Data?.Terrain;
            if (terrain == null)
                return true; // Assume walkable if no terrain data

            // Convert world position to terrain coordinates
            var terrainPos = terrain.WorldToTerrain(position);
            
            // Check if within terrain bounds
            if (terrainPos.X < 0 || terrainPos.Y < 0 || 
                terrainPos.X >= terrain.NumCellsX || terrainPos.Y >= terrain.NumCellsY)
                return false;

            // Get terrain data at position
            var terrainData = terrain.TerrainData[terrainPos.Y * terrain.NumCellsX + terrainPos.X];
            
            // Basic walkability: not a wall or obstacle
            return (terrainData & 1) == 0; // Bit 0 = walkable
        }
        catch (Exception ex)
        {
            _debugLog($"IsWalkable error: {ex.Message}");
            return true; // Assume walkable on error to avoid getting stuck
        }
    }

    /// <summary>
    /// Find waypoint for indirect pathfinding
    /// </summary>
    private Vector3? FindWaypoint(Vector3 start, Vector3 target)
    {
        try
        {
            // Try waypoints at 90-degree angles from direct line
            var direction = Vector3.Normalize(target - start);
            var distance = Vector3.Distance(start, target);
            var waypointDistance = distance * 0.5f; // Waypoint at halfway point
            
            // Try perpendicular waypoints
            var perpendicular = new Vector3(-direction.Y, direction.X, direction.Z);
            
            for (float offset = 50f; offset <= 200f; offset += 50f)
            {
                var leftWaypoint = start + (direction * waypointDistance) + (perpendicular * offset);
                var rightWaypoint = start + (direction * waypointDistance) - (perpendicular * offset);
                
                if (IsWalkable(leftWaypoint) && IsDirectPathWalkable(start, leftWaypoint) && IsDirectPathWalkable(leftWaypoint, target))
                    return leftWaypoint;
                    
                if (IsWalkable(rightWaypoint) && IsDirectPathWalkable(start, rightWaypoint) && IsDirectPathWalkable(rightWaypoint, target))
                    return rightWaypoint;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _debugLog($"FindWaypoint error: {ex.Message}");
            return null;
        }
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
}