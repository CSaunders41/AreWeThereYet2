# AreWeThereYet2 Development Project Prompt

## Project Overview
I'm developing **AreWeThereYet2**, an enhanced Path of Exile follower plugin that combines the superior technical foundation of AreWeThereYet with the comprehensive following features of FollowBot. This is a new ExileCore plugin project.

## Source Codebases Analysis

### AreWeThereYet (Foundation)
- **Location**: `/Users/chris/Documents/POE/AreWeThereYet/`
- **Framework**: ExileCore plugin for Path of Exile (.NET 8.0)
- **Strengths**: 
  - Modern framework and architecture
  - Superior movement system with human-like mouse curves
  - Advanced LineOfSight pathfinding with 6-level terrain detection
  - Real-time door state monitoring and terrain analysis
  - Intelligent dash usage through obstacles
  - Clean, focused codebase (12 files)
  - Excellent debug visualization and terrain debugging

- **Current Limitations**:
  - Manual leader name configuration
  - No combat integration or distance management
  - Basic error handling
  - Limited party management
  - No aura/buff systems
  - Missing failsafe mechanisms

### FollowBot (Feature Reference)
- **Location**: `/Users/chris/Documents/FollowBot/`
- **Framework**: DreamPoeBot/Loki-based comprehensive automation (.NET Framework)
- **Strengths**:
  - Mature task-based priority system (10 tasks)
  - Comprehensive party management (auto-join, whitelist, status detection)
  - Combat integration with multiple leash ranges
  - Complete aura/buff management (26+ aura types, golem management)
  - Robust error handling with ErrorManager
  - Multi-zone travel support (towns, hideouts, maps, labyrinth)
  - Sophisticated object caching system (15-minute lifecycle)
  - Portal failsafe mechanisms (portal to town after failures)

### Key Reference Files

#### AreWeThereYet Essential Files:
- `AreWeThereYet.cs` - Main plugin entry point and architecture
- `AutoPilot.cs` - Core following logic and task execution (962 lines)
- `AreWeThereYetSettings.cs` - Configuration system with ExileCore nodes
- `Utils/LineOfSight.cs` - Advanced pathfinding and terrain analysis (677 lines)
- `PartyElements.cs` - Party member detection and UI parsing
- `TaskNode.cs` - Basic task structure and types
- `Mouse.cs` / `Keyboard.cs` - Human-like input simulation
- `Helper.cs` - Utility functions for world-to-screen conversion

#### FollowBot Essential Files:
- `FollowBot.cs` - Main bot architecture and task management (363 lines)
- `FollowTask.cs` - Core following behavior with failsafes (146 lines)
- `FollowBotSettings.cs` - Configuration system
- `JoinPartyTask.cs` - Party invitation handling (69 lines)
- `TravelToPartyZoneTask.cs` - Multi-zone travel logic (231 lines)
- `PreCombatFollowTask.cs` - Combat distance management (94 lines)
- `Helpers/PartyHelper.cs` - Party management utilities (84 lines)
- `SimpleEXtensions/Move.cs` - Movement system (83 lines)
- `SimpleEXtensions/PlayerAction.cs` - Game interactions (581 lines)
- `SimpleEXtensions/World.cs` - Area definitions and state (279 lines)
- `SimpleEXtensions/Global/CombatAreaCache.cs` - Object caching system (678 lines)
- `SimpleEXtensions/CommonTasks/CastAuraTask.cs` - Aura management (180 lines)
- `SimpleEXtensions/BotStructure.cs` - Task management framework (91 lines)

#### Additional Reference Documents:
- `FollowBot_vs_AreWeThereYet_Comparison.md` - Comprehensive feature comparison matrix and analysis
- This document contains detailed spreadsheet-style comparisons, priority recommendations, and architectural analysis

## Development Goals

### Primary Objective
Create AreWeThereYet2 that maintains AreWeThereYet's superior movement/pathfinding foundation while adding FollowBot's comprehensive following features, adapted to the ExileCore framework.

### Key Principles
1. **Keep What Works**: Preserve AreWeThereYet's movement system, pathfinding, and modern architecture
2. **Adapt, Don't Copy**: Translate FollowBot's concepts to ExileCore APIs rather than direct code copying
3. **Incremental Development**: Build features in priority order with testing at each phase
4. **Modern Architecture**: Use .NET 8.0 patterns and ExileCore best practices

## Priority Development Roadmap

### Phase 1: Core Foundation (High Priority - 1-2 weeks)
1. **Enhanced Error Handling System**
   - Centralized ErrorManager with error counting
   - Circuit breaker patterns for failed operations
   - Diagnostic information collection
   - Failsafe mechanisms (portal to town, etc.)

2. **Auto Party Leader Detection**
   - Remove manual leader name requirement
   - Auto-detect party leader from game state
   - Handle leader changes and disconnections
   - Fallback to manual configuration if needed

3. **Structured Task Management**
   - Priority-based task system adapted from FollowBot
   - TaskNode enhancement with priority queuing
   - Task lifecycle management (create, execute, complete, timeout)
   - Clear separation of task types (Movement, Combat, Portal, Recovery)

### Phase 2: Combat Integration (High Priority - 1 week)
4. **Combat Distance Management**
   - Separate follow distances for combat vs non-combat
   - Pre-combat positioning logic
   - Combat state detection
   - Leashing behavior during fights

5. **Robust Failsafe Mechanisms**
   - Multi-level failure detection
   - Escalating recovery strategies
   - Stuck detection and recovery
   - Emergency portal-to-town functionality

### Phase 3: Party & Travel Enhancement (Medium Priority - 1-2 weeks)
6. **Advanced Party Management**
   - Auto-join party invitations with whitelist
   - Party status monitoring and handling
   - Party member tracking and coordination

7. **Enhanced Zone Travel**
   - Zone-specific portal detection and matching
   - Multi-zone travel coordination
   - Portal request system with randomized messages

### Phase 4: Advanced Features (Medium-Low Priority - 2-3 weeks)
8. **Aura/Buff Management System**
   - Comprehensive aura casting (adapted from FollowBot's 26+ types)
   - Golem management with health monitoring
   - Dynamic skill management

9. **Object Caching System**
   - Monster, chest, shrine caching for performance
   - Cache lifecycle management
   - Smart cache invalidation

## Technical Implementation Context

### Current AreWeThereYet Architecture
```csharp
public class AreWeThereYet : BaseSettingsPlugin<AreWeThereYetSettings>
{
    internal AutoPilot autoPilot = new AutoPilot();
    internal LineOfSight lineOfSight;
    
    // Key components:
    // - Single coroutine-based execution
    // - TaskNode list for movement tasks
    // - Advanced pathfinding with terrain analysis
    // - Human-like input simulation
}
```

### Target AreWeThereYet2 Architecture
```csharp
public class AreWeThereYet2 : BaseSettingsPlugin<AreWeThereYet2Settings>
{
    private TaskManager taskManager;           // New: Priority-based task system
    private PartyManager partyManager;         // New: Comprehensive party handling
    private ErrorManager errorManager;         // New: Centralized error management
    private CombatManager combatManager;       // New: Combat integration
    private EnhancedAutoPilot autoPilot;      // Enhanced: Original + new features
    private LineOfSight lineOfSight;          // Keep: Superior pathfinding
}
```

### Key Integration Points
- **ExileCore APIs**: Use `GameController.EntityListWrapper`, `IngameState`, `PartyElement`
- **Settings System**: Extend ExileCore's node-based configuration
- **Input System**: Maintain AreWeThereYet's human-like input patterns
- **Pathfinding**: Keep LineOfSight system, enhance with task integration
- **Coroutines**: Use ExileCore's coroutine system for async operations

## Implementation Guidelines

### Code Quality Standards
- Follow ExileCore plugin patterns and conventions
- Use modern C# 12 features and .NET 8.0 capabilities
- Implement comprehensive error handling and logging
- Include debug visualization where appropriate
- Write maintainable, well-documented code

### Testing Strategy
- Test each phase incrementally before moving to next
- Validate against both solo and party gameplay scenarios
- Test failsafe mechanisms thoroughly
- Verify performance with caching systems

### Configuration Philosophy
- Provide sensible defaults that work out-of-the-box
- Allow granular customization for advanced users
- Maintain backward compatibility with AreWeThereYet settings where possible
- Use ExileCore's settings validation patterns

## Development Environment Setup

### Required References
- ExileCore framework
- GameOffsets for memory access
- SharpDX.Mathematics for vector operations
- Newtonsoft.Json for configuration

### Folder Structure
```
AreWeThereYet2/
├── Core/
│   ├── TaskManager.cs
│   ├── ErrorManager.cs
│   └── PartyManager.cs
├── Combat/
│   └── CombatManager.cs
├── Movement/
│   ├── EnhancedAutoPilot.cs
│   └── LineOfSight.cs (from original)
├── Utils/
│   ├── Mouse.cs (enhanced)
│   ├── Keyboard.cs (enhanced)
│   └── Helper.cs
└── AreWeThereYet2.cs (main plugin)
```

## Success Metrics
1. **Functionality**: All Phase 1 features working reliably
2. **Performance**: No degradation from original AreWeThereYet
3. **Usability**: Works out-of-the-box with minimal configuration
4. **Reliability**: Robust error handling with graceful failures
5. **Maintainability**: Clean, extensible architecture for future enhancements

## Development Support Needed
I need assistance with:
- Implementing the priority-based task management system
- Adapting FollowBot's party management concepts to ExileCore APIs
- Creating robust error handling patterns
- Integrating combat detection and distance management
- Testing and debugging the incremental implementations

## Current Status
Starting Phase 1 development. Ready to begin with enhanced error handling system and auto party leader detection as the foundation features.

---

**Context**: This project builds upon comprehensive analysis of both codebases, identifying AreWeThereYet's superior technical foundation while recognizing FollowBot's mature following features. The goal is to create the best-of-both-worlds solution using modern development practices on the ExileCore platform.

**Next Immediate Task**: Begin Phase 1 implementation starting with ErrorManager and PartyManager components. 