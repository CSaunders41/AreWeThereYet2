# AreWeThereYet2 Development TODO

## Phase 0: Proof of Concept (2-3 days)
*Validate core concepts before full Phase 1 implementation*

### 🚀 Core Infrastructure Setup
- [ ] **Project Structure Setup**
  - [ ] Create basic folder structure (`Core/`, `Movement/`, `Utils/`, etc.)
  - [ ] Set up `.csproj` file with ExileCore references
  - [ ] Create basic `AreWeThereYet2.cs` plugin shell
  - [ ] Add required using statements and namespaces

- [ ] **Enhanced TaskNode System**
  - [ ] Design `TaskPriority` enum (Emergency, Combat, Movement, Maintenance)
  - [ ] Create enhanced `TaskNode` class with priority support
  - [ ] Implement basic `TaskManager` class structure
  - [ ] Add task queuing and priority sorting logic
  - [ ] Test task creation and priority ordering

- [ ] **Auto Party Leader Detection**
  - [ ] Investigate ExileCore `PartyElement` API
  - [ ] Create `PartyManager` class skeleton
  - [ ] Implement basic party member detection
  - [ ] Add auto-leader identification logic
  - [ ] Test with party and solo scenarios

- [ ] **ExileCore API Integration Validation**
  - [ ] Verify `GameController.EntityListWrapper` access
  - [ ] Test `IngameState` integration
  - [ ] Validate settings system compatibility
  - [ ] Confirm coroutine system usage
  - [ ] Test basic plugin lifecycle (Enable/Disable/Update)

### 🧪 Phase 0 Testing & Validation
- [ ] **Basic Functionality Test**
  - [ ] Plugin loads without errors
  - [ ] Settings panel appears correctly
  - [ ] TaskManager creates and prioritizes tasks
  - [ ] PartyManager detects leader correctly

- [ ] **Integration Test**
  - [ ] Test with existing AreWeThereYet installation
  - [ ] Verify no conflicts with ExileCore
  - [ ] Check memory usage baseline
  - [ ] Validate performance impact

### 📋 Phase 0 Success Criteria
- [ ] Plugin successfully loads in ExileCore
- [ ] TaskManager correctly prioritizes different task types
- [ ] PartyManager auto-detects party leader (or falls back gracefully)
- [ ] No performance degradation from baseline
- [ ] Clean error-free operation for 10+ minutes

---

## Phase 1: Core Foundation (1-2 weeks)
*Build upon Phase 0 success*

### 🛡️ Enhanced Error Handling System
- [ ] **ErrorManager Implementation**
  - [ ] Create centralized `ErrorManager` class
  - [ ] Implement error counting and categorization
  - [ ] Add circuit breaker patterns for failed operations
  - [ ] Create diagnostic information collection
  - [ ] Implement failsafe mechanisms (portal to town, etc.)

- [ ] **Error Recovery Strategies**
  - [ ] Define error categories (Network, Pathfinding, Combat, Party, Input)
  - [ ] Implement per-category escalation paths
  - [ ] Add circuit breaker thresholds
  - [ ] Create emergency shutdown procedures

### 🎯 Enhanced Party Leader Detection
- [ ] **Auto-Detection Improvements**
  - [ ] Handle leader changes and disconnections
  - [ ] Implement fallback to manual configuration
  - [ ] Add leader validation and health checks
  - [ ] Create party status monitoring

### ⚙️ Structured Task Management
- [ ] **Task System Enhancement**
  - [ ] Complete priority-based task system
  - [ ] Implement task lifecycle management
  - [ ] Add task timeout and retry logic
  - [ ] Create clear task type separation (Movement, Combat, Portal, Recovery)

### 🧪 Phase 1 Testing
- [ ] **Error Handling Tests**
  - [ ] Test various error scenarios
  - [ ] Validate recovery strategies
  - [ ] Check failsafe mechanisms

- [ ] **Party Management Tests**
  - [ ] Test leader changes
  - [ ] Validate auto-detection accuracy
  - [ ] Check fallback mechanisms

---

## Phase 2: Combat Integration (1 week)
*Implement combat-aware following*

### ⚔️ Combat Distance Management
- [ ] Create `CombatManager` class
- [ ] Implement separate follow distances for combat vs non-combat
- [ ] Add pre-combat positioning logic
- [ ] Implement combat state detection
- [ ] Add leashing behavior during fights

### 🔧 Robust Failsafe Mechanisms
- [ ] Multi-level failure detection
- [ ] Escalating recovery strategies
- [ ] Stuck detection and recovery
- [ ] Emergency portal-to-town functionality

---

## Phase 3: Party & Travel Enhancement (1-2 weeks)
*Advanced party and zone management*

### 👥 Advanced Party Management
- [ ] Auto-join party invitations with whitelist
- [ ] Party status monitoring and handling
- [ ] Party member tracking and coordination

### 🌍 Enhanced Zone Travel
- [ ] Zone-specific portal detection and matching
- [ ] Multi-zone travel coordination
- [ ] Portal request system with randomized messages

---

## Phase 4: Advanced Features (2-3 weeks)
*Polish and advanced functionality*

### ✨ Aura/Buff Management System
- [ ] Comprehensive aura casting (26+ types from FollowBot)
- [ ] Golem management with health monitoring
- [ ] Dynamic skill management

### 💾 Object Caching System
- [ ] Monster, chest, shrine caching for performance
- [ ] Cache lifecycle management
- [ ] Smart cache invalidation

---

## Development Standards & Guidelines

### 📝 Code Quality
- [ ] Follow ExileCore plugin patterns and conventions
- [ ] Use modern C# 12 features and .NET 8.0 capabilities
- [ ] Implement comprehensive error handling and logging
- [ ] Include debug visualization where appropriate
- [ ] Write maintainable, well-documented code

### 🧪 Testing Strategy
- [ ] Test each phase incrementally before moving to next
- [ ] Validate against both solo and party gameplay scenarios
- [ ] Test failsafe mechanisms thoroughly
- [ ] Verify performance with caching systems

### ⚙️ Configuration Philosophy
- [ ] Provide sensible defaults that work out-of-the-box
- [ ] Allow granular customization for advanced users
- [ ] Maintain backward compatibility where possible
- [ ] Use ExileCore's settings validation patterns

---

## Project Status
- **Current Phase**: Phase 0 - Proof of Concept
- **Started**: [Date to be filled]
- **Target Completion**: 6-8 weeks total
- **Next Milestone**: Complete Phase 0 validation

## Notes
- Keep AreWeThereYet's superior movement system and pathfinding
- Adapt FollowBot's concepts to ExileCore APIs (don't copy directly)
- Test frequently with incremental changes
- Focus on reliability and error handling from the start