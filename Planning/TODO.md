# AreWeThereYet2 Development TODO
## Focused Predictive Movement System

**Goal**: Replace over-engineered pathfinding with smart, predictive movement that anticipates leader behavior.

**Key Insight**: The original AreWeThereYet's superiority comes from large movement steps + predictive positioning, not complex pathfinding.

---

## Phase 0: Foundation Complete ✅
*Validate existing architecture before proceeding*

### 🚀 Core Infrastructure (COMPLETE)
- [x] **Project Structure Setup**
  - [x] Create folder structure (`Core/`, `Movement/`, `Utils/`, etc.)
  - [x] Set up `.csproj` file with ExileCore references
  - [x] Create basic `AreWeThereYet2.cs` plugin shell
  - [x] Add required using statements and namespaces

- [x] **Enhanced TaskNode System**
  - [x] Design `TaskPriority` enum (Emergency, Combat, Movement, Maintenance)
  - [x] Create enhanced `TaskNode` class with priority support
  - [x] Implement basic `TaskManager` class structure
  - [x] Add task queuing and priority sorting logic
  - [x] Test task creation and priority ordering

- [x] **Auto Party Leader Detection**
  - [x] Investigate ExileCore `PartyElement` API
  - [x] Create `PartyManager` class skeleton
  - [x] Implement basic party member detection
  - [x] Add auto-leader identification logic
  - [x] Test with party and solo scenarios

### 🧪 Phase 0 Final Validation
**⚠️ Testing Workflow**: Development on macOS, testing on Windows machine with ExileCore. All changes must be pushed to GitHub for testing.

- [ ] **Final System Test**
  - [ ] Plugin loads without errors in ExileCore
  - [ ] TaskManager creates and prioritizes tasks correctly
  - [ ] PartyManager detects leader accurately
  - [ ] No performance degradation from baseline
  - [ ] Clean error-free operation for 10+ minutes

**✅ SUCCESS CRITERIA**: If validation passes, proceed to Phase 1. Current system architecture is solid.

---

## Phase 1: Intelligent Predictive Movement (2-3 weeks)
*Replace the problematic 1,287-line AdvancedLineOfSight.cs with smart prediction*

### 🎯 Core Problem Analysis
**Current Issue**: `AdvancedLineOfSight.cs` is over-engineered (1,287 lines) with:
- Complex terrain analysis causing performance issues
- Over-analysis leading to stuck scenarios  
- Multiple failure points from complex logic

**Solution**: Replace with focused predictive movement that anticipates leader behavior.

### 🧠 Predictive Movement System
- [ ] **Create PredictiveMovement.cs**
  - [ ] Implement `IPathfinding` interface (maintain compatibility)
  - [ ] Add leader position history tracking (`Queue<Vector3>`)
  - [ ] Implement velocity estimation from position history
  - [ ] Add acceleration detection for turning leaders
  - [ ] Create physics-based position prediction (2-3 seconds ahead)

- [ ] **Implement Interception Logic**
  - [ ] Calculate optimal interception point (where to meet leader)
  - [ ] Add interception vs. direct following decision logic
  - [ ] Handle fast-moving vs. slow-moving leader scenarios
  - [ ] Add fallback to direct following when prediction fails

- [ ] **Large Step Movement (Original AreWeThereYet Style)**
  - [ ] Implement aggressive step sizes (120-180 units)
  - [ ] Add dynamic step sizing based on distance to predicted position
  - [ ] Ensure steps are never smaller than 80 units (prevent micro-movements)
  - [ ] Add direct line preference with minimal safety checks

- [ ] **Basic Obstacle Avoidance**
  - [ ] Implement simple left/right angle avoidance (30-degree attempts)
  - [ ] Add "basically safe" position checking (minimal entity scanning)
  - [ ] Remove complex terrain analysis and caching
  - [ ] Focus on "good enough" obstacle handling

### 🔄 Integration & Testing
- [ ] **Replace Current System**
  - [ ] Update `MovementManager` to use `PredictiveMovement` instead of `AdvancedLineOfSight`
  - [ ] Remove or archive `AdvancedLineOfSight.cs` (save complex logic for future reference)
  - [ ] Maintain existing `MovementExecutor` and `Mouse` utilities
  - [ ] Keep all existing interfaces for compatibility

- [ ] **Performance Testing**
  - [ ] Measure performance improvement vs. current system
  - [ ] Test with various leader movement patterns (walking, running, teleporting)
  - [ ] Validate stuck event reduction (target: 70% fewer stuck events)
  - [ ] Test with different follow distances and scenarios

- [ ] **Debug & Refinement**
  - [ ] Add comprehensive logging for prediction accuracy
  - [ ] Track leader prediction success rate
  - [ ] Monitor movement efficiency metrics
  - [ ] Fine-tune prediction timeframes and step sizes

### ✅ Phase 1 Success Criteria
- [ ] **Predictive following works correctly** (anticipates leader movement)
- [ ] **70% reduction in stuck events** compared to current system
- [ ] **Performance improvement** (faster pathfinding calculations)
- [ ] **Large movement steps** prevent micro-movement issues
- [ ] **Clean, maintainable code** under 300 lines total

---

## Phase 2: Advanced Intelligence (Optional - 2-3 weeks)
*Add contextual intelligence if Phase 1 is successful*

### 🧠 Context-Aware Strategy Selection
- [ ] **Movement Context Detection**
  - [ ] Implement `MovementContext` enum (OpenTerrain, ComplexObstacles, DynamicChasing, TightSpaces)
  - [ ] Add context analysis logic (obstacle count, leader velocity, distance)
  - [ ] Create strategy selection based on detected context
  - [ ] Add context switching with hysteresis (prevent rapid switching)

- [ ] **Adaptive Learning System**
  - [ ] Track movement performance metrics (stuck events, efficiency, time-to-target)
  - [ ] Implement simple learning weights for different contexts
  - [ ] Add performance-based strategy adjustment
  - [ ] Store learned patterns for future sessions

### 🚀 Enhanced Pathfinding Options
- [ ] **Flow Field Integration (for crowd scenarios)**
  - [ ] Implement basic flow field generation for multiple followers
  - [ ] Add flow field following when 3+ party members exist
  - [ ] Integrate with predictive movement for hybrid approach

- [ ] **JPS+ for Tight Spaces**
  - [ ] Add Jump Point Search Plus for precision navigation
  - [ ] Use only when context detection identifies tight spaces
  - [ ] Maintain large-step preference when possible

### ✅ Phase 2 Success Criteria
- [ ] **Context-aware strategy selection** improves movement in different scenarios
- [ ] **Learning system** reduces failed movements over time
- [ ] **Maintains Phase 1 performance** while adding intelligence
- [ ] **No increase in stuck events** from added complexity

---

## Phase 3: Combat & Formation Enhancement (1-2 weeks)
*Integrate with combat awareness and formation maintenance*

### ⚔️ Combat-Aware Movement
- [ ] **Combat Context Detection**
  - [ ] Detect when leader is in combat vs. traveling
  - [ ] Adjust following distance based on combat state
  - [ ] Implement pre-combat positioning logic
  - [ ] Add leashing behavior during fights

- [ ] **Formation Maintenance**
  - [ ] Add basic formation positioning relative to leader
  - [ ] Implement formation adjustment based on terrain
  - [ ] Add collision avoidance between party members
  - [ ] Maintain formation while following predictive movement

### 🛡️ Enhanced Safety Systems
- [ ] **Stuck Detection & Recovery**
  - [ ] Implement stuck detection with time-based thresholds
  - [ ] Add automatic unstuck procedures (larger steps, alternative angles)
  - [ ] Implement emergency recovery (portal to town if critical)
  - [ ] Add stuck event reporting for learning system

### ✅ Phase 3 Success Criteria
- [ ] **Combat-aware following** improves tactical positioning
- [ ] **Formation maintenance** works during complex movement
- [ ] **Stuck detection** provides reliable recovery mechanisms
- [ ] **System remains stable** under all combat scenarios

---

## Development Standards

### 📝 Code Quality
- **Focus on simplicity**: Each class should have a single, clear purpose
- **Prefer composition over inheritance**: Use dependency injection for testability
- **Comprehensive error handling**: Every ExileCore API call should be wrapped
- **Performance first**: Profile and optimize hot paths
- **Maintainable architecture**: Code should be easy to understand and modify

### 🧪 Testing Strategy
- **Test each phase incrementally** before moving to the next
- **Validate on Windows/ExileCore** after each major change
- **Measure performance impact** of every new feature
- **Test edge cases**: Leader disconnection, rapid movement, obstacle scenarios
- **Regression testing**: Ensure new features don't break existing functionality

### ⚙️ Configuration Philosophy
- **Sensible defaults** that work out-of-the-box
- **Minimal required configuration** for basic functionality
- **Advanced options** for power users without overwhelming interface
- **Real-time adjustment** of key parameters for testing

---

## Project Status

- **Current Phase**: Phase 0 Complete → Starting Phase 1
- **Started**: January 2025
- **Target Completion**: 6-8 weeks total (3-4 weeks remaining)
- **Next Milestone**: Complete Phase 1 - Predictive Movement System
- **GitHub Repository**: https://github.com/CSaunders41/AreWeThereYet2
- **Development Approach**: Iterative development with frequent testing

## Key Decisions Made

- **Architecture**: Keep existing TaskManager, PartyManager, ErrorManager (they work well)
- **Pathfinding Strategy**: Replace complex analysis with predictive intelligence
- **Movement Style**: Large aggressive steps like original AreWeThereYet
- **Scope**: Focus on core following behavior, add complexity only if Phase 1 succeeds
- **Performance**: Prioritize responsiveness over algorithmic complexity

## Success Metrics

- **Primary**: 70% reduction in stuck events compared to current system
- **Secondary**: Faster pathfinding calculations (< 50ms per decision)
- **Tertiary**: Improved leader following (anticipatory vs. reactive movement)
- **Code Quality**: Maintainable, understandable, under 500 lines for core pathfinding