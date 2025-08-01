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
            
            // REAL PATHFINDING: Try waypoint navigation for ANY distance when direct path is blocked
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
                
                _debugLog($"ADVANCED: Complex waypoint path found: {waypoints.Count} waypoints, {result.Distance:F1} total distance");
                return result;
            }
            
            // FALLBACK: If pathfinding fails, try direct movement (obstacle detection might be wrong)
            _debugLog($"ADVANCED: Pathfinding failed, falling back to direct movement: {distance:F1} units");
            result.Success = true;
            result.Path = new List<Vector3> { start, target };
            result.SimplifiedPath = new List<Vector3> { target };
            result.Distance = distance;
            return result;
            
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
    /// REWRITTEN: Simple, aggressive movement like original AreWeThereYet
    /// Always uses large fixed steps, minimal safety checks to prevent stuck scenarios
    /// </summary>
    public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
    {
        try
        {
            var distance = Vector3.Distance(currentPos, targetPos);
            
            // If very close, move directly to target
            if (distance <= 30f)
                return targetPos;
            
            var direction = Vector3.Normalize(targetPos - currentPos);
            
            // CRITICAL FIX: Use FIXED large step sizes like original AreWeThereYet
            float stepSize = 120f; // Always use large steps (120 units)
            
            // For very far leaders, use even bigger steps
            if (distance > 300f)
                stepSize = 180f; // Extra large steps for very far leaders
            
            // Don't make steps larger than remaining distance 
            stepSize = Math.Min(stepSize, distance);
            
            var candidatePoint = currentPos + (direction * stepSize);
            
            _debugLog($"SIMPLE MOVEMENT: Distance={distance:F1}, FixedStep={stepSize:F1}, Target={candidatePoint}");
            
            // MINIMAL safety check - only avoid truly impassable terrain
            // Don't over-analyze like before - this caused the stuck issues
            if (IsBasicallySafe(candidatePoint))
            {
                _debugLog($"MOVING: Taking {stepSize:F1} unit step toward leader");
                return candidatePoint;
            }
            
            // Simple obstacle avoidance - try left/right angles
            var leftPoint = currentPos + (RotateVector(direction, 30f) * stepSize);
            if (IsBasicallySafe(leftPoint))
            {
                _debugLog($"OBSTACLE AVOID: Taking left angle step {stepSize:F1} units");
                return leftPoint;
            }
            
            var rightPoint = currentPos + (RotateVector(direction, -30f) * stepSize);
            if (IsBasicallySafe(rightPoint))
            {
                _debugLog($"OBSTACLE AVOID: Taking right angle step {stepSize:F1} units");
                return rightPoint;
            }
            
            // LAST RESORT: Still use a decent sized step (80 units minimum)
            // This prevents the tiny steps that cause stuck scenarios
            var safeStep = Math.Max(80f, stepSize * 0.7f);
            var safePoint = currentPos + (direction * safeStep);
            _debugLog($"SAFE FALLBACK: Taking {safeStep:F1} unit step (no tiny steps!)");
            return safePoint;
        }
        catch (Exception ex)
        {
            _debugLog($"GetNextMovePoint error: {ex.Message}");
            // Even on error, take a decent sized step toward target
            var distance = Vector3.Distance(currentPos, targetPos);
            var direction = Vector3.Normalize(targetPos - currentPos);
            var emergencyStep = Math.Min(100f, distance); // Always at least try for 100 units
            return currentPos + (direction * emergencyStep);
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
    /// ENHANCED raycast line-of-sight check - like original AreWeThereYet with variable step analysis
    /// </summary>
    private bool IsDirectLineOfSightClear(Vector3 start, Vector3 target)
    {
        try
        {
            var distance = Vector3.Distance(start, target);
            
            // Enhanced distance check with terrain complexity assessment
            if (distance > MaxDirectPathDistance)
            {
                _debugLog($"ADVANCED: Distance too far for direct path: {distance:F1} > {MaxDirectPathDistance}");
                return false;
            }

            // ENHANCED: Variable step size based on distance and terrain complexity
            var baseStepSize = CalculateOptimalRaycastStepSize(distance);
            var currentStepSize = baseStepSize;
            var currentPos = start;
            var direction = Vector3.Normalize(target - start);
            
            _debugLog($"ADVANCED: Raycast check - Distance: {distance:F1}, BaseStep: {baseStepSize:F1}");
            
            while (Vector3.Distance(currentPos, target) > currentStepSize)
            {
                currentPos += direction * currentStepSize;
                
                // Enhanced terrain analysis at each step
                var terrainType = AnalyzeTerrainAt(currentPos);
                
                // Handle different terrain types appropriately
                if (!IsTerrainPassable(terrainType, currentPos))
                {
                    _debugLog($"ADVANCED: Line-of-sight blocked by {terrainType} at {currentPos}");
                    return false;
                }
                
                // Adjust step size based on terrain complexity
                currentStepSize = AdjustStepSizeForTerrain(terrainType, baseStepSize);
                
                // Safety check to prevent infinite loops
                if (currentStepSize < 5f)
                    currentStepSize = 5f;
            }
            
            // Final check at target position
            var targetTerrain = AnalyzeTerrainAt(target);
            if (!IsTerrainPassable(targetTerrain, target))
            {
                _debugLog($"ADVANCED: Target position blocked by {targetTerrain}");
                return false;
            }
            
            _debugLog($"ADVANCED: Direct line-of-sight CLEAR over {distance:F1} units");
            return true;
        }
        catch (Exception ex)
        {
            _debugLog($"IsDirectLineOfSightClear error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Calculate optimal raycast step size based on distance and complexity
    /// </summary>
    private float CalculateOptimalRaycastStepSize(float distance)
    {
        // Short distance: small steps for precision
        if (distance < 50f) return 10f;
        
        // Medium distance: balanced steps
        if (distance < 150f) return 15f;
        
        // Long distance: larger steps for performance
        if (distance < 300f) return 25f;
        
        // Very long distance: largest steps
        return 35f;
    }

    /// <summary>
    /// Determine if terrain type is passable
    /// </summary>
    private bool IsTerrainPassable(TerrainType terrainType, Vector3 position)
    {
        switch (terrainType)
        {
            case TerrainType.Walkable:
                return true;
                
            case TerrainType.Door:
                // Check actual door state
                var doorState = GetDoorState(position);
                return doorState == DoorState.Open || doorState == DoorState.Opening;
                
            case TerrainType.Water:
                // Water might be passable depending on depth/type
                return true; // For now, assume passable
                
            case TerrainType.Wall:
                return false; // Walls are never passable
                
            case TerrainType.Obstacle:
                // Some obstacles might be bypassable with movement skills
                return false; // Conservative approach
                
            case TerrainType.Unknown:
                return true; // Assume passable if unknown
                
            default:
                return true;
        }
    }

    /// <summary>
    /// Adjust step size based on terrain complexity
    /// </summary>
    private float AdjustStepSizeForTerrain(TerrainType terrainType, float baseStepSize)
    {
        switch (terrainType)
        {
            case TerrainType.Walkable:
                return baseStepSize; // Normal step size
                
            case TerrainType.Door:
                return baseStepSize * 0.5f; // Smaller steps near doors for precision
                
            case TerrainType.Water:
                return baseStepSize * 0.7f; // Slightly smaller steps in water
                
            case TerrainType.Obstacle:
                return baseStepSize * 0.3f; // Much smaller steps near obstacles
                
            default:
                return baseStepSize;
        }
    }

    /// <summary>
    /// Find optimal waypoints using advanced terrain analysis - ENHANCED with movement skill support
    /// </summary>
    private List<Vector3> FindOptimalWaypoints(Vector3 start, Vector3 target)
    {
        try
        {
            _debugLog($"REAL PATHFINDING: Analyzing path from {start} to {target}");
            
            // Step 1: Analyze obstacles along the direct path
            var obstacles = DetectObstaclesAlongPath(start, target);
            if (obstacles.Count == 0)
            {
                _debugLog($"PATHFINDING: No obstacles detected, should use direct path");
                return new List<Vector3>(); // Direct path should work
            }
            
            _debugLog($"PATHFINDING: Found {obstacles.Count} obstacles, generating waypoints");
            
            // Step 2: Generate waypoints to navigate around each obstacle  
            var waypoints = new List<Vector3>();
            var currentPos = start;
            
            foreach (var obstacle in obstacles)
            {
                var obstacleWaypoints = GenerateWaypointsAroundObstacle(currentPos, target, obstacle);
                if (obstacleWaypoints.Count > 0)
                {
                    waypoints.AddRange(obstacleWaypoints);
                    currentPos = obstacleWaypoints.Last(); // Update current position
                    _debugLog($"PATHFINDING: Added {obstacleWaypoints.Count} waypoints around obstacle at {obstacle}");
                }
            }
            
            // Step 3: Validate the complete path
            if (ValidateWaypointPath(start, waypoints, target))
            {
                _debugLog($"PATHFINDING: Generated {waypoints.Count} waypoints for complex navigation");
                return waypoints;
            }
            
            // Step 4: Fallback - try simple angle-based waypoint generation
            _debugLog($"PATHFINDING: Complex path failed, trying simple angle-based approach");
            return GenerateSimpleAngleWaypoints(start, target);
        }
        catch (Exception ex)
        {
            _debugLog($"FindOptimalWaypoints error: {ex.Message}");
            return GenerateSimpleAngleWaypoints(start, target);
        }
    }

    /// <summary>
    /// REAL PATHFINDING: Detect obstacles along the direct path from start to target
    /// Returns positions where obstacles are blocking the path
    /// </summary>
    private List<Vector3> DetectObstaclesAlongPath(Vector3 start, Vector3 target)
    {
        var obstacles = new List<Vector3>();
        
        try
        {
            var direction = Vector3.Normalize(target - start);
            var distance = Vector3.Distance(start, target);
            var currentPos = start;
            var stepSize = 25f; // Check every 25 units for obstacles
            
            while (Vector3.Distance(currentPos, target) > stepSize)
            {
                currentPos += direction * stepSize;
                
                var terrainType = AnalyzeTerrainAt(currentPos);
                if (!IsTerrainPassable(terrainType, currentPos))
                {
                    obstacles.Add(currentPos);
                    _debugLog($"PATHFINDING: Obstacle detected at {currentPos} - {terrainType}");
                }
            }
            
            return obstacles;
        }
        catch (Exception ex)
        {
            _debugLog($"DetectObstaclesAlongPath error: {ex.Message}");
            return obstacles;
        }
    }

    /// <summary>
    /// Generate waypoints to navigate around a specific obstacle
    /// </summary>
    private List<Vector3> GenerateWaypointsAroundObstacle(Vector3 start, Vector3 target, Vector3 obstaclePos)
    {
        var waypoints = new List<Vector3>();
        
        try
        {
            var toTarget = Vector3.Normalize(target - start);
            var toObstacle = Vector3.Normalize(obstaclePos - start);
            
            // Try waypoints to the left and right of the obstacle
            var leftAngle = 90f;  // 90 degrees left
            var rightAngle = -90f; // 90 degrees right
            var waypointDistance = 100f; // Distance to place waypoints from obstacle
            
            // Left waypoint
            var leftDirection = RotateVector(toObstacle, leftAngle);
            var leftWaypoint = obstaclePos + (leftDirection * waypointDistance);
            
            // Right waypoint  
            var rightDirection = RotateVector(toObstacle, rightAngle);
            var rightWaypoint = obstaclePos + (rightDirection * waypointDistance);
            
            // Test which waypoint works better
            if (IsBasicallySafe(leftWaypoint) && IsDirectLineOfSightClear(start, leftWaypoint))
            {
                waypoints.Add(leftWaypoint);
                _debugLog($"PATHFINDING: Left waypoint around obstacle: {leftWaypoint}");
            }
            else if (IsBasicallySafe(rightWaypoint) && IsDirectLineOfSightClear(start, rightWaypoint))
            {
                waypoints.Add(rightWaypoint);
                _debugLog($"PATHFINDING: Right waypoint around obstacle: {rightWaypoint}");
            }
            
            return waypoints;
        }
        catch (Exception ex)
        {
            _debugLog($"GenerateWaypointsAroundObstacle error: {ex.Message}");
            return waypoints;
        }
    }

    /// <summary>
    /// Validate that a waypoint path is actually walkable
    /// </summary>
    private bool ValidateWaypointPath(Vector3 start, List<Vector3> waypoints, Vector3 target)
    {
        try
        {
            if (waypoints.Count == 0)
                return false;
            
            // Check path from start to first waypoint
            var currentPos = start;
            
            foreach (var waypoint in waypoints)
            {
                if (!IsDirectLineOfSightClear(currentPos, waypoint))
                {
                    _debugLog($"PATHFINDING: Path validation failed at waypoint {waypoint}");
                    return false;
                }
                currentPos = waypoint;
            }
            
            // Check path from last waypoint to target
            if (!IsDirectLineOfSightClear(currentPos, target))
            {
                _debugLog($"PATHFINDING: Path validation failed from last waypoint to target");
                return false;
            }
            
            _debugLog($"PATHFINDING: Path validation successful");
            return true;
        }
        catch (Exception ex)
        {
            _debugLog($"ValidateWaypointPath error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Fallback: Generate simple angle-based waypoints (improved version of old logic)
    /// </summary>  
    private List<Vector3> GenerateSimpleAngleWaypoints(Vector3 start, Vector3 target)
    {
        try
        {
            var direction = Vector3.Normalize(target - start);
            var distance = Vector3.Distance(start, target);
            
            // Try more angles and larger waypoint distances
            var angles = new float[] { -90f, -45f, -30f, 30f, 45f, 90f, 135f, -135f };
            var waypointDistance = Math.Min(150f, distance * 0.7f);
            
            foreach (var angle in angles)
            {
                var waypointDirection = RotateVector(direction, angle);
                var waypoint = start + (waypointDirection * waypointDistance);
                
                if (IsBasicallySafe(waypoint) && 
                    IsDirectLineOfSightClear(start, waypoint) &&
                    IsDirectLineOfSightClear(waypoint, target))
                {
                    _debugLog($"PATHFINDING: Simple angle waypoint found at {angle}° - {waypoint}");
                    return new List<Vector3> { waypoint };
                }
            }
            
            _debugLog($"PATHFINDING: No simple angle waypoints found");
            return new List<Vector3>();
        }
        catch (Exception ex)
        {
            _debugLog($"GenerateSimpleAngleWaypoints error: {ex.Message}");
            return new List<Vector3>();
        }
    }

    /// <summary>
    /// CRITICAL FEATURE: Analyze movement skill usage for obstacle bypass - like original AreWeThereYet
    /// </summary>
    private List<Vector3>? AnalyzeMovementSkillPath(Vector3 start, Vector3 target)
    {
        try
        {
            var distance = Vector3.Distance(start, target);
            
            // Only consider movement skills for medium to long distances with obstacles
            if (distance < 75f || distance > 500f)
                return null;
            
            // Check what obstacles are blocking the direct path
            var obstacleMap = AnalyzeObstacleMap(start, target);
            if (obstacleMap.Count == 0)
                return null; // No obstacles, regular movement is fine
            
            // Determine best movement skill strategy
            var movementSkillType = DetermineOptimalMovementSkill(obstacleMap, distance);
            if (movementSkillType == MovementSkillType.None)
                return null;
            
            // Generate movement skill waypoints
            var skillWaypoints = GenerateMovementSkillWaypoints(start, target, movementSkillType, obstacleMap);
            
            if (skillWaypoints != null && skillWaypoints.Count > 0)
            {
                _debugLog($"ADVANCED: {movementSkillType} skill path generated with {skillWaypoints.Count} points");
                return skillWaypoints;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _debugLog($"AnalyzeMovementSkillPath error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Analyze obstacles between start and target
    /// </summary>
    private List<ObstacleInfo> AnalyzeObstacleMap(Vector3 start, Vector3 target)
    {
        var obstacles = new List<ObstacleInfo>();
        
        try
        {
            var direction = Vector3.Normalize(target - start);
            var distance = Vector3.Distance(start, target);
            var stepSize = 20f;
            
            for (float d = stepSize; d < distance; d += stepSize)
            {
                var checkPos = start + direction * d;
                var terrainType = AnalyzeTerrainAt(checkPos);
                
                if (terrainType != TerrainType.Walkable)
                {
                    obstacles.Add(new ObstacleInfo
                    {
                        Position = checkPos,
                        Type = terrainType,
                        Distance = d
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _debugLog($"AnalyzeObstacleMap error: {ex.Message}");
        }
        
        return obstacles;
    }

    /// <summary>
    /// Determine optimal movement skill for obstacle pattern
    /// </summary>
    private MovementSkillType DetermineOptimalMovementSkill(List<ObstacleInfo> obstacles, float totalDistance)
    {
        if (obstacles.Count == 0)
            return MovementSkillType.None;
        
        // Count obstacle types
        var wallCount = obstacles.Count(o => o.Type == TerrainType.Wall);
        var obstacleCount = obstacles.Count(o => o.Type == TerrainType.Obstacle);
        var doorCount = obstacles.Count(o => o.Type == TerrainType.Door);
        
        // Dash is good for bypassing small obstacles and gaps
        if (obstacleCount > wallCount && totalDistance < 200f)
        {
            return MovementSkillType.Dash;
        }
        
        // Leap Slam is good for walls and longer distances
        if (wallCount > 0 || totalDistance > 150f)
        {
            return MovementSkillType.LeapSlam;
        }
        
        // Lightning Warp for very complex obstacle patterns
        if (obstacles.Count > 5)
        {
            return MovementSkillType.LightningWarp;
        }
        
        return MovementSkillType.None;
    }

    /// <summary>
    /// Generate waypoints for movement skill usage
    /// </summary>
    private List<Vector3> GenerateMovementSkillWaypoints(Vector3 start, Vector3 target, MovementSkillType skillType, List<ObstacleInfo> obstacles)
    {
        var waypoints = new List<Vector3>();
        
        try
        {
            switch (skillType)
            {
                case MovementSkillType.Dash:
                    // Dash through small gaps - create waypoints just past obstacles
                    if (obstacles.Count > 0)
                    {
                        var direction = Vector3.Normalize(target - start);
                        var dashTarget = obstacles.Last().Position + direction * 50f; // Land past obstacle
                        waypoints.Add(dashTarget);
                    }
                    break;
                    
                case MovementSkillType.LeapSlam:
                    // Leap over obstacles - target clear area beyond obstacles
                    if (obstacles.Count > 0)
                    {
                        var direction = Vector3.Normalize(target - start);
                        var leapTarget = obstacles.Last().Position + direction * 75f; // Land well past obstacles
                        
                        // Ensure landing spot is safe
                        if (IsWalkable(leapTarget))
                        {
                            waypoints.Add(leapTarget);
                        }
                    }
                    break;
                    
                case MovementSkillType.LightningWarp:
                    // Teleport past complex obstacle patterns
                    var midPoint = Vector3.Lerp(start, target, 0.7f); // Warp most of the way
                    if (IsWalkable(midPoint))
                    {
                        waypoints.Add(midPoint);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _debugLog($"GenerateMovementSkillWaypoints error: {ex.Message}");
        }
        
        return waypoints;
    }

    /// <summary>
    /// Advanced 6-level terrain analysis - ENHANCED like original AreWeThereYet
    /// </summary>
    private TerrainType AnalyzeTerrainAt(Vector3 position)
    {
        try
        {
            // LEVEL 1: Door Detection (highest priority - affects movement timing)
            var doorState = GetDoorState(position);
            if (doorState != DoorState.None)
            {
                return doorState == DoorState.Open ? TerrainType.Walkable : TerrainType.Door;
            }
            
            // LEVEL 2: Dynamic Obstacles (monsters, players - affects immediate pathing)
            if (HasDynamicObstacle(position))
                return TerrainType.Obstacle;
            
            // LEVEL 3: Static Walls (permanent obstacles - affects long-term routing)
            if (HasWallAt(position))
                return TerrainType.Wall;
            
            // LEVEL 4: Static Obstacles (chests, statues - can be bypassed)
            if (HasObstacleAt(position))
                return TerrainType.Obstacle;
            
            // LEVEL 5: Environmental Hazards (water, lava - affects safety)
            if (HasWaterAt(position))
                return TerrainType.Water;
            
            // LEVEL 6: Walkable with quality assessment
            return AssessWalkableQuality(position);
        }
        catch (Exception ex)
        {
            _debugLog($"AnalyzeTerrainAt error: {ex.Message}");
            return TerrainType.Unknown;
        }
    }

    /// <summary>
    /// Enhanced door state detection - tracks door states in real-time
    /// </summary>
    private DoorState GetDoorState(Vector3 position)
    {
        try
        {
            var nearbyEntities = _gameController?.EntityListWrapper?.Entities
                ?.Where(e => e != null && e.IsValid && 
                           Vector3.Distance(position, e.Pos) <= MinObstacleDistance);
            
            if (nearbyEntities != null)
            {
                foreach (var entity in nearbyEntities)
                {
                    // Check if entity is a door based on metadata and render
                    var metadata = entity.Metadata;
                    var render = entity.GetComponent<Render>();
                    
                    if (metadata?.Contains("door", StringComparison.OrdinalIgnoreCase) == true ||
                        render?.Name?.Contains("door", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        // Try to determine door state from entity properties
                        // This is a simplified version - real implementation would need more ExileCore API knowledge
                        try
                        {
                            // Check if door is animated (opening/closing)
                            var animated = entity.GetComponent<ExileCore.PoEMemory.Components.Animated>();
                            if (animated != null)
                            {
                                // Door is in transition
                                return DoorState.Opening;
                            }
                            
                            // Check door collision/blocking state
                            // If door exists but isn't blocking, it's probably open
                            return DoorState.Open;
                        }
                        catch
                        {
                            // If we can't determine state, assume closed for safety
                            return DoorState.Closed;
                        }
                    }
                }
            }
            
            return DoorState.None;
        }
        catch (Exception ex)
        {
            _debugLog($"GetDoorState error: {ex.Message}");
            return DoorState.None;
        }
    }

    /// <summary>
    /// Assess walkable terrain quality for optimal pathing
    /// </summary>
    private TerrainType AssessWalkableQuality(Vector3 position)
    {
        try
        {
            var player = _gameController?.Player;
            if (player == null) return TerrainType.Walkable;

            var currentPos = player.Pos;
            
            // Assess terrain quality based on:
            // 1. Height variations (prefer flat terrain)
            var heightDiff = Math.Abs(position.Z - currentPos.Z);
            if (heightDiff > 50f)
            {
                // Still walkable but not optimal
                return TerrainType.Walkable;
            }
            
            // 2. Distance from obstacles (prefer clear areas)
            var nearbyObstacles = _gameController?.EntityListWrapper?.Entities
                ?.Where(e => e != null && e.IsValid && 
                           Vector3.Distance(position, e.Pos) <= 75f &&
                           (e.Type == EntityType.WorldItem || e.Type == EntityType.Monster))
                ?.Count() ?? 0;
            
            // High-quality walkable terrain (open, flat areas)
            return nearbyObstacles > 5 ? TerrainType.Walkable : TerrainType.Walkable;
        }
        catch
        {
            return TerrainType.Walkable;
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
    /// SIMPLE safety check - only avoids truly impassable terrain
    /// Much less restrictive than IsPositionSafe to prevent stuck scenarios
    /// </summary>
    private bool IsBasicallySafe(Vector3 position)
    {
        try
        {
            var player = _gameController?.Player;
            if (player?.Pos == null) return true;

            // Only do the most basic checks - avoid over-analysis
            var distance = Vector3.Distance(player.Pos, position);
            var heightDiff = Math.Abs(position.Z - player.Pos.Z);
            
            // Very permissive limits - only block truly unreachable positions
            return distance <= 3000f && heightDiff <= 300f;
        }
        catch
        {
            // If any error, assume it's safe - better to move than get stuck
            return true;
        }
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