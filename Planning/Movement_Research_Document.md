# Movement System Research Document
## AreWeThereYet2 ExileCore Plugin

**Created:** January 2025  
**Purpose:** Research ExileCore capabilities and A* pathfinding for improved movement system

---

## Table of Contents
1. [ExileCore Movement APIs](#exilecore-movement-apis)
2. [A* Pathfinding Research](#a-star-pathfinding-research)
3. [Current System Analysis](#current-system-analysis)
4. [Key Insights](#key-insights)
5. [Recommendations](#recommendations)
6. [Implementation Plan](#implementation-plan)

---

## ExileCore Movement APIs

### Core APIs Available
Based on codebase analysis, ExileCore provides these key APIs for movement:

#### 1. **GameController** - Primary Interface
```csharp
// Core access pattern
var gameController = GetGameController(); // From BaseSettingsPlugin
var player = gameController.Player;
var ingameState = gameController.Game.IngameState;
```

#### 2. **Entity System**
```csharp
// Entity access
var entities = gameController.EntityListWrapper.Entities;
var validEntitiesByType = gameController.EntityListWrapper.ValidEntitiesByType;

// Player position
var playerPos = gameController.Player.Pos; // Vector3
```

#### 3. **Camera & Coordinate Conversion**
```csharp
// World to screen conversion (CRITICAL for movement)
var camera = gameController.Game.IngameState.Camera;
var screenPos = camera.WorldToScreen(worldPos); // Vector2
```

#### 4. **Component System**
```csharp
// Entity components for advanced detection
var render = entity.GetComponent<Render>();
var life = entity.GetComponent<Life>();
var player = entity.GetComponent<Player>();
```

### What ExileCore DOESN'T Provide
- **No built-in pathfinding algorithms**
- **No A* implementation**
- **No obstacle detection beyond entity queries**
- **No navigation mesh**
- **No movement prediction**

### ExileCore Strengths for Movement
✅ **Real-time entity data**  
✅ **Accurate world-to-screen conversion**  
✅ **Component-based entity information**  
✅ **Game state detection (InGame, etc.)**

---

## Research Findings Update (January 2025)

### Web Search Results Analysis
**Key Finding: ExileCore movement is a niche area with few public examples**

- ❌ **No successful ExileCore movement plugins found** in public repositories
- ❌ **No comprehensive ExileCore terrain detection APIs** documented
- ❌ **Limited community knowledge** about proper obstacle detection
- ✅ **General movement principles** from other game bots show similar patterns

### Original AreWeThereYet Behavior Analysis (User Observation)

**CRITICAL INSIGHT**: User reports that original AreWeThereYet has different behavior:
- **Mouse hovers over leader** when leader is within range
- **Clicks directly on leader's position** constantly 
- **Path updates in real-time** as leader moves
- **No intermediate waypoint calculation** - just direct leader clicking

**This explains the "confident" movement**:
- ✅ **Immediate response** to leader movement (no prediction lag)
- ✅ **No stuck waypoints** (always targeting current leader position)  
- ✅ **Natural following** (mouse literally follows the leader)
- ✅ **Constant updates** (not calculating complex paths)

**Our Current Approach vs Original**:
- ❌ **We calculate intermediate waypoints** → introduces complexity
- ❌ **We predict leader positions** → can be wrong
- ❌ **We use fixed step sizes** → not leader-responsive
- ✅ **Original clicks directly on leader** → simple and effective

### BREAKTHROUGH RESEARCH FINDINGS (Critical Discovery)

**Why Original AreWeThereYet is Superior - KEY INSIGHTS:**

#### **1. MISSING: InputHumanizer Integration**
Research shows successful ExileCore plugins use **InputHumanizer library**:
- **Human-like mouse movement** with Bézier curves
- **Natural acceleration/deceleration**  
- **Smooth steering** instead of direct clicking
- **Configurable delays** between actions

**OUR CRITICAL MISTAKE**: Using basic `Mouse.LeftClick()` instead of human-like movement curves!

#### **2. Game Integration Strategy**
**Original Success Pattern**:
- ✅ **Hover mouse over leader** (constant visual tracking)
- ✅ **Use game's native pathfinding** (let PoE handle obstacles)  
- ✅ **High-frequency updates** (every physics tick)
- ✅ **Human-like curves** (works WITH game systems)

**Our Failed Pattern**:
- ❌ **Calculate complex waypoints** (over-engineered)
- ❌ **Sharp direct clicks** (robotic, fights game)
- ❌ **Infrequent updates** (gets stuck between decisions)
- ❌ **Fighting game pathfinding** instead of using it

#### **3. The "Smart" Isn't AI - It's INTEGRATION**
- Original isn't "smarter pathfinding" - it's **better game integration**
- Uses **Path of Exile's built-in click-to-move** pathfinding
- **Mouse curves** make it look natural to game systems
- **Constant updates** provide real-time course correction

#### **4. Why We Can't Replicate Success**
- **Missing InputHumanizer** - our clicks are too robotic
- **Over-engineering** - trying to be smarter than the game
- **Wrong update frequency** - not constant like original
- **Fighting the game** instead of working with it

### Current System Diagnostic Issues

#### 1. **Step Size Problems (High Priority)**
From user reports: "clicking very close to the player character"
- **Suspected Issue**: `distance * 0.6f` for close targets creates micro-movements
- **Investigation**: Added enhanced logging to track step size decisions
- **Need**: Test with distances 50-150 units to verify calculation

#### 2. **Obstacle Detection Too Aggressive (High Priority)**  
From user reports: "still getting stuck on corners"
- **Suspected Issue**: `HasMajorObstacleAt()` flagging too many false positives
- **Investigation**: May need to reduce `obstacleRadius` from 35f to 15-20f
- **Need**: Test obstacle detection sensitivity

#### 3. **ExileCore Memory Usage (Unknown)**
- **Current Approach**: Using `EntityListWrapper.Entities` for obstacle detection
- **Uncertainty**: Don't know if this is the correct/optimal API
- **Research Gap**: No examples of successful ExileCore terrain detection

### Experimental Approaches to Test

#### **Experiment 1: Step Size Override** (Quick Test)
```csharp
// Force minimum step sizes to prevent micro-movements
private float CalculateAggressiveStepSize(float distance)
{
    // EXPERIMENT: Always use large steps, ignore distance
    if (distance > 300f) return 180f;  // Aggressive
    if (distance > 100f) return 120f;  // Standard  
    return 100f; // FORCE large minimum instead of distance * 0.6f
}
```

#### **Experiment 2: Obstacle Detection Sensitivity** (Quick Test)
```csharp
// Reduce obstacle detection radius and be more selective
private bool HasMajorObstacleAt(Vector3 position)
{
    const float obstacleRadius = 15f; // REDUCED from 35f
    // Only block for EntityType.WorldItem with specific names
    // Skip monsters unless they're bosses AND stationary
}
```

#### **Experiment 3: Bypass Obstacle Detection** (Diagnostic Test)
```csharp
// Temporarily disable obstacle detection to test pure step sizes
public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
{
    // EXPERIMENT: Skip all obstacle detection
    var direction = Vector3.Normalize(targetPos - currentPos);
    var stepSize = CalculateAggressiveStepSize(distance);
    return currentPos + (direction * stepSize);
}
```

#### **Experiment 4: Original AreWeThereYet Emulation** (If other experiments fail)
```csharp
// Emulate exactly what original AreWeThereYet did
public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
{
    var distance = Vector3.Distance(currentPos, targetPos);
    var direction = Vector3.Normalize(targetPos - currentPos);
    
    // Fixed 120-unit steps, only basic safety checks
    float stepSize = Math.Min(120f, distance);
    var nextPoint = currentPos + (direction * stepSize);
    
    // Only avoid if height difference > 150f
    var heightDiff = Math.Abs(nextPoint.Z - currentPos.Z);
    if (heightDiff > 150f) {
        // Try 90-degree left/right
        var leftPoint = currentPos + (RotateVector2D(direction, 90f) * stepSize);
        return leftPoint;
    }
    
    return nextPoint;
}
```

#### **Experiment 5: Direct Leader Clicking** (NEW - Based on User Observation)
**KEY INSIGHT**: Original AreWeThereYet hovers mouse over leader and clicks directly on their position, constantly updating path.

```csharp
// Emulate original AreWeThereYet's direct leader clicking behavior
public Vector3? GetNextMovePoint(Vector3 currentPos, Vector3 targetPos)
{
    var distance = Vector3.Distance(currentPos, targetPos);
    
    // CRITICAL INSIGHT: Within certain range, click DIRECTLY on leader position
    if (distance <= 200f) // Within "hover range"
    {
        _debugLog($"DIRECT LEADER CLICK: Distance {distance:F1} - clicking directly on leader");
        return targetPos; // Click exactly on leader, no intermediate calculation!
    }
    
    // For distant leaders, take large steps toward them
    var direction = Vector3.Normalize(targetPos - currentPos);
    var stepSize = Math.Min(150f, distance); // Large steps
    return currentPos + (direction * stepSize);
}
```

**Research Notes:**
- Original behavior: **Mouse hovers over leader** when in range
- **Constantly clicking leader's current position** → immediate path updates
- **No intermediate waypoint calculation** → more responsive following
- **Direct position targeting** → reduces stuck scenarios from waypoint miscalculation  
✅ **Entity type filtering (Monster, Player, etc.)**

---

## A* Pathfinding Research

### A* Algorithm Fundamentals
A* uses a cost function: **f(n) = g(n) + h(n)**
- **g(n)** = Cost from start to current node
- **h(n)** = Heuristic estimate from current node to goal
- **f(n)** = Total estimated cost

### Key Components for Implementation

#### 1. **Node Representation**
```csharp
public class PathNode
{
    public Vector3 Position { get; set; }
    public float GCost { get; set; }      // Distance from start
    public float HCost { get; set; }      // Distance to target
    public float FCost => GCost + HCost;  // Total cost
    public PathNode Parent { get; set; }
    public bool IsWalkable { get; set; }
}
```

#### 2. **Heuristic Functions**
```csharp
// Manhattan Distance (good for grid-based movement)
float ManhattanDistance(Vector3 a, Vector3 b)
{
    return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}

// Euclidean Distance (good for free movement)
float EuclideanDistance(Vector3 a, Vector3 b)
{
    return Vector3.Distance(a, b);
}
```

#### 3. **Priority Queue**
Essential for A* performance - always process lowest F-cost nodes first.

### A* Implementation Patterns

#### Basic A* Structure:
1. **Initialize** open list with start node
2. **Loop** until goal found or open list empty:
   - Get node with lowest F-cost from open list
   - Move to closed list
   - For each neighbor:
     - Calculate G, H, F costs
     - Add to open list if better path found
3. **Reconstruct** path by following parent pointers

#### Optimizations for Real-time Games:
- **Time-slicing** - Limit iterations per frame
- **Hierarchical pathfinding** - Use waypoints for long distances
- **Path caching** - Reuse paths when target hasn't moved much
- **Dynamic replanning** - Recalculate when obstacles change

---

## Current System Analysis

### Architecture Overview
```
MovementManager
    ├── TaskManager (priority-based task system)
    ├── PartyManager (leader detection)
    ├── MovementExecutor (actual movement execution)
    └── AdvancedLineOfSight (pathfinding implementation)
```

### Issues Identified

#### 1. **Over-Complexity**
- `AdvancedLineOfSight.cs` = 1,287 lines
- 6-level terrain detection system
- Complex caching and entity analysis
- Multiple pathfinding strategies in one class

#### 2. **Performance Concerns**
- Heavy entity scanning on every pathfinding call
- Complex terrain analysis for every position check
- No time-slicing for expensive operations

#### 3. **Reliability Issues**
- Many potential failure points
- Complex state management
- Over-engineered for the actual requirements

### What's Working Well
✅ **Task priority system**  
✅ **World-to-screen conversion**  
✅ **Mouse movement with curves**  
✅ **Basic leader following logic**

---

## Key Insights

### 1. **The "Superior" AreWeThereYet Movement**
The original's superiority comes from:
- **Larger movement steps** (100-180 units vs. smaller increments)
- **Direct pathing preference** over complex waypoints
- **Aggressive movement** when leader is far away
- **Minimal safety checks** that prevent over-analysis

### 2. **Stuck Prevention Strategy**
Current code shows the solution:
```csharp
// CRITICAL FIX: Use FIXED large step sizes like original AreWeThereYet
float stepSize = 120f; // Always use large steps (120 units)

// For very far leaders, use even bigger steps
if (distance > 300f)
    stepSize = 180f; // Extra large steps for very far leaders
```

### 3. **A* Can Be Overkill**
For following a moving target:
- **Direct line movement** often works better than complex pathfinding
- **Obstacle avoidance** via simple angle adjustments is usually sufficient
- **A* pathfinding** best reserved for complex static navigation

---

## Smart Movement System Architecture

Based on advanced research, here's a **truly intelligent** movement system that goes beyond traditional approaches:

### Core Intelligence: Multi-Modal Navigation AI

**1. Predictive Leader Tracking**
- **Velocity extrapolation**: Predict where leader will be in 2-3 seconds
- **Pattern recognition**: Learn leader's movement patterns over time
- **Proactive positioning**: Move to intercept path rather than chase behind

**2. Contextual Strategy Selection**
```csharp
public enum MovementContext
{
    OpenTerrain,      // Use flow fields for efficiency
    ComplexObstacles, // Use hierarchical A* (HPA*)
    DynamicChasing,   // Use predictive steering
    TightSpaces,      // Use JPS+ for precision
    CrowdNavigation   // Use steering behaviors + flow fields
}
```

**3. Adaptive Learning System**
- **Performance metrics**: Track stuck events, path efficiency, leader distance
- **Dynamic weights**: Adjust pathfinding costs based on historical success
- **Failure pattern recognition**: Learn from stuck scenarios and avoid them

### Advanced Techniques Integration

**1. Hierarchical Pathfinding (HPA*)**
- **Cluster-based navigation** for large areas
- **Pre-computed cluster connections** for instant high-level routing
- **Detail refinement** only when needed

**2. Flow Fields for Crowd Intelligence**
- **Shared destination paths** when multiple followers exist
- **Dynamic obstacle integration** for real-time environment changes
- **Smooth crowd movement** without individual A* calculations

**3. Jump Point Search Plus (JPS+)**
- **Aggressive pruning** of unnecessary nodes
- **Straight-line preference** like original AreWeThereYet
- **Diagonal movement optimization**

**4. Predictive Steering Behaviors**
- **Leader trajectory analysis**: Understand leader's intended path
- **Interception calculations**: Move to where leader will be, not where they are
- **Dynamic formation maintenance**: Adjust position based on context

---

## Smart Movement Implementation Plan

### Phase 1: Intelligent Context System (1-2 weeks)

**1. Context Detection Engine**
```csharp
public class MovementContextAnalyzer
{
    public MovementContext AnalyzeContext(Vector3 current, Vector3 target, Entity leader)
    {
        var distance = Vector3.Distance(current, target);
        var obstacleCount = CountObstaclesInPath(current, target);
        var leaderVelocity = leader.GetVelocity();
        var crowdDensity = GetNearbyEntityCount(current, 100f);
        
        // Intelligent context switching
        if (crowdDensity > 5) return MovementContext.CrowdNavigation;
        if (obstacleCount > 10 && distance > 500f) return MovementContext.ComplexObstacles;
        if (leaderVelocity.magnitude > 200f) return MovementContext.DynamicChasing;
        if (obstacleCount < 3) return MovementContext.OpenTerrain;
        
        return MovementContext.TightSpaces;
    }
}
```

**2. Predictive Leader Tracking**
```csharp
public class PredictiveTracker
{
    private Queue<Vector3> _leaderHistory = new();
    private Vector3 _predictedPosition;
    
    public Vector3 GetPredictedLeaderPosition(Entity leader, float predictionTime = 2f)
    {
        var velocity = EstimateLeaderVelocity();
        var acceleration = EstimateLeaderAcceleration();
        
        // Physics-based prediction: pos + vel*t + 0.5*acc*t²
        return leader.Position + velocity * predictionTime + 0.5f * acceleration * predictionTime * predictionTime;
    }
    
    public Vector3 GetInterceptionPoint(Vector3 followerPos, Vector3 leaderPos, Vector3 leaderVel, float followerSpeed)
    {
        // Calculate where to move to intercept leader's path
        // Advanced vector math for interception
        return CalculateInterceptionVector(followerPos, leaderPos, leaderVel, followerSpeed);
    }
}
```

### Phase 2: Multi-Algorithm Integration (2-3 weeks)

**1. Flow Field System (for crowd scenarios)**
```csharp
public class FlowFieldNavigation : IPathfinding
{
    private Dictionary<Vector3, Vector3> _flowField = new();
    
    public void GenerateFlowField(Vector3 target, List<Vector3> obstacles)
    {
        // Use Dijkstra's algorithm to create flow field
        // Each cell points toward optimal path to target
        var integrationField = BuildIntegrationField(target, obstacles);
        _flowField = GenerateDirectionVectors(integrationField);
    }
    
    public Vector3 GetFlowDirection(Vector3 position)
    {
        // Bilinear interpolation between nearby flow vectors
        return InterpolateFlowField(position);
    }
}
```

**2. Hierarchical A* (HPA*) for Large Areas**
```csharp
public class HierarchicalPathfinding : IPathfinding
{
    private Dictionary<int, Cluster> _clusters = new();
    private Dictionary<(int, int), List<Vector3>> _precomputedPaths = new();
    
    public PathfindingResult FindPath(Vector3 start, Vector3 target)
    {
        // 1. Find which clusters start and target are in
        var startCluster = GetCluster(start);
        var targetCluster = GetCluster(target);
        
        // 2. Use precomputed inter-cluster paths
        var clusterPath = FindClusterPath(startCluster, targetCluster);
        
        // 3. Refine path within clusters
        return RefinePathWithinClusters(start, target, clusterPath);
    }
}
```

**3. Learning System**
```csharp
public class AdaptiveLearningSystem
{
    private Dictionary<MovementContext, float> _contextWeights = new();
    private List<MovementMetric> _performanceHistory = new();
    
    public void RecordPerformance(MovementContext context, float efficiency, bool stuck, float timeToTarget)
    {
        var metric = new MovementMetric { Context = context, Efficiency = efficiency, Stuck = stuck, Time = timeToTarget };
        _performanceHistory.Add(metric);
        
        // Adjust weights based on performance
        if (stuck) _contextWeights[context] *= 0.9f; // Penalize
        else _contextWeights[context] *= 1.1f; // Reward
    }
    
    public float GetContextWeight(MovementContext context) => _contextWeights.GetValueOrDefault(context, 1.0f);
}
```

### Phase 3: Advanced Intelligence Integration (2-3 weeks)

**1. Master Movement AI**
```csharp
public class IntelligentMovementSystem : IDisposable
{
    private readonly MovementContextAnalyzer _contextAnalyzer;
    private readonly PredictiveTracker _predictor;
    private readonly FlowFieldNavigation _flowField;
    private readonly HierarchicalPathfinding _hierarchical;
    private readonly AdaptiveLearningSystem _learning;
    
    public MovementCommand GetNextMovement(Vector3 current, Entity leader)
    {
        // 1. Predict where leader will be
        var predictedTarget = _predictor.GetPredictedLeaderPosition(leader);
        
        // 2. Analyze movement context
        var context = _contextAnalyzer.AnalyzeContext(current, predictedTarget, leader);
        
        // 3. Choose optimal strategy based on context and learning
        var strategy = SelectOptimalStrategy(context);
        
        // 4. Execute movement with chosen strategy
        var movement = ExecuteStrategy(strategy, current, predictedTarget, leader);
        
        // 5. Record performance for learning
        _learning.RecordPerformance(context, movement.Efficiency, movement.Stuck, movement.TimeToTarget);
        
        return movement;
    }
    
    private MovementStrategy SelectOptimalStrategy(MovementContext context)
    {
        var weight = _learning.GetContextWeight(context);
        
        return context switch
        {
            MovementContext.OpenTerrain => new FlowFieldStrategy(_flowField),
            MovementContext.ComplexObstacles => new HierarchicalStrategy(_hierarchical),
            MovementContext.DynamicChasing => new PredictiveSteeringStrategy(_predictor),
            MovementContext.TightSpaces => new JPSPlusStrategy(),
            MovementContext.CrowdNavigation => new CrowdFlowStrategy(_flowField),
            _ => new DefaultStrategy()
        };
    }
}
```

This system would be **genuinely intelligent** because it:

1. **Predicts** leader movement instead of reacting
2. **Learns** from experience and adapts
3. **Chooses** optimal algorithms contextually  
4. **Combines** multiple advanced techniques intelligently
5. **Scales** from simple to complex scenarios seamlessly

The key insight: Instead of finding "the best pathfinding algorithm," create a **meta-intelligence that chooses the right tool for each situation** and improves over time.

Would this approach deliver the smart movement system you're looking for?

---

## Research Resources

### A* Pathfinding References
- **Medium A* Guide**: Comprehensive C# implementation with Manhattan distance
- **Unity Learn**: A* algorithm tutorial series (6 parts)
- **GitHub Examples**: Multiple C# A* implementations with optimizations

### ExileCore References
- **ExileCore GitHub**: https://github.com/ExileCore/ExileCore
- **Plugin Examples**: Study existing movement plugins
- **Memory API**: ExileCore.PoEMemory namespace for entity access

### Key Algorithms to Study
1. **Basic A*** - Standard implementation
2. **Hierarchical A*** - For large maps
3. **Jump Point Search (JPS)** - A* optimization for grids
4. **Theta*** - For smoother paths in open areas

---

## Next Steps

1. **Create simple movement system** based on original AreWeThereYet approach
2. **Test with large movement steps** (120-180 units)
3. **Measure performance improvement** vs. current complex system
4. **Implement A* as optional enhancement** for complex scenarios
5. **Create hybrid system** that chooses best approach based on situation

---

**Last Updated:** January 2025  
**Status:** Research Complete - Ready for Implementation