using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Interfaces;
using SharpDX;
using AreWeThereYet2.Core;
using AreWeThereYet2.Party;
using AreWeThereYet2.Movement;
using AreWeThereYet2.Settings;
using ImGuiNET;
using static ImGuiNET.ImGuiCond;
using static ImGuiNET.ImGuiWindowFlags;
using static ImGuiNET.ImGuiCol;

namespace AreWeThereYet2;

public class AreWeThereYet2 : BaseSettingsPlugin<AreWeThereYet2Settings>
{
    private TaskManager? _taskManager;
    private PartyManager? _partyManager;
    private ErrorManager? _errorManager;
    private MovementManager? _movementManager;
    private List<string> _debugLog = new List<string>();
    private readonly int _maxDebugLogLines = 100;
    
    public override bool Initialise()
    {
        try
        {
            // Initialize core managers
            _errorManager = new ErrorManager();
            _taskManager = new TaskManager(_errorManager);
            _partyManager = new PartyManager(GameController, _errorManager);
            _movementManager = new MovementManager(GameController, _taskManager, _partyManager, _errorManager, Settings, DebugLog);
            
            Name = "AreWeThereYet2";
            
            LogMessage("AreWeThereYet2 v2.0 initialized successfully", 3);
            DebugLog("Plugin initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize AreWeThereYet2: {ex.Message}", 3);
            DebugLog($"INIT ERROR: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Add debug message to persistent log and ExileCore log
    /// </summary>
    private void DebugLog(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            
            // Add to in-memory debug log
            _debugLog.Add(logEntry);
            
            // Keep log size manageable
            if (_debugLog.Count > _maxDebugLogLines)
            {
                _debugLog.RemoveAt(0);
            }
            
            // Also log to ExileCore's logging system (more persistent)
            if (Settings?.DebugMode?.Value == true)
            {
                LogMessage($"[DEBUG] {message}", 1);
            }
        }
        catch
        {
            // Don't crash if logging fails
        }
    }

    public override void Render()
    {
        try
        {
            // Basic validation
            if (!GameController.Game.IngameState.InGame)
                return;

            // Update managers
            _taskManager?.Update();
            _partyManager?.Update();
            _movementManager?.Update();
            
            // Debug output every ~1 second (using milliseconds)
            var timeMs = (long)GameController.Game.IngameState.TimeInGame.TotalMilliseconds;
            if (timeMs % 1000 < 50) // Show debug roughly every second (within 50ms window)
            {
                var player = GameController?.Player;
                var leader = _partyManager?.GetPartyLeader();
                
                // Get player and leader names for comparison
                var playerName = player?.GetComponent<Player>()?.PlayerName ?? "Unknown";
                var leaderName = leader?.GetComponent<Player>()?.PlayerName ?? "Unknown";
                var manualLeaderName = Settings?.ManualLeaderName?.Value ?? "";
                
                var distance = _movementManager?.GetDistanceToLeader() ?? 0f;
                var currentTask = _taskManager?.GetCurrentTask();
                
                DebugLog($"Player={player != null}({playerName}), Leader={leader != null}({leaderName}), " +
                        $"ManualLeader='{manualLeaderName}', EnableFollowing={Settings?.EnableFollowing?.Value}, " +
                        $"Distance={distance:F1}, CurrentTask={currentTask?.Description ?? "None"}, " +
                        $"SameEntity={player == leader}");
            }
            
            // Sync manual leader setting
            SyncManualLeaderSetting();
            
            // Process tasks
            _taskManager?.ProcessNextTask();
            
            // Render task overlay
            RenderTaskOverlay();
        }
        catch (Exception ex)
        {
            _errorManager?.HandleError("Render", ex);
        }
    }

    public override void DrawSettings()
    {
        try
        {
            // Settings will be implemented later
            ImGui.Text("AreWeThereYet2 v2.0 - Phase 0 Development");
            ImGui.Text("Enhanced follower plugin combining superior pathfinding with comprehensive features");
            ImGui.Separator();
            
            // Follow Toggle Button (prominent placement)
            if (Settings?.EnableFollowing != null)
            {
                var isFollowing = Settings.EnableFollowing.Value;
                var followColor = isFollowing ? new System.Numerics.Vector4(0, 1, 0, 1) : new System.Numerics.Vector4(1, 0.5f, 0, 1);
                
                // Large, prominent toggle button
                ImGui.PushStyleColor(Text, followColor);
                if (ImGui.Button($"Following: {(isFollowing ? "ENABLED" : "DISABLED")}##FollowToggle", new System.Numerics.Vector2(200, 30)))
                {
                    Settings.EnableFollowing.Value = !Settings.EnableFollowing.Value;
                    LogMessage($"Following toggled to: {Settings.EnableFollowing.Value}");
                }
                ImGui.PopStyleColor();
                
                // Alternative checkbox version (smaller)
                ImGui.SameLine();
                bool enableFollowing = Settings.EnableFollowing.Value;
                if (ImGui.Checkbox("##EnableFollowingCheck", ref enableFollowing))
                {
                    Settings.EnableFollowing.Value = enableFollowing;
                    LogMessage($"Following checkbox set to: {Settings.EnableFollowing.Value}");
                }
                
                // Debug text to verify setting state
                ImGui.SameLine();
                ImGui.Text($"(Debug: Actual={Settings.EnableFollowing.Value})");
            }
            
            ImGui.Separator();
            
            if (_partyManager != null)
            {
                ImGui.Text($"Party Status: {(_partyManager.IsInParty() ? "In Party" : "Solo")}");
                
                // Manual Leader Selection
                var nearbyPlayers = _partyManager.GetNearbyPlayerNames();
                if (nearbyPlayers.Count > 0)
                {
                    ImGui.Text("Select Leader:");
                    
                    // Get current manual leader or empty
                    var currentManualLeader = Settings?.ManualLeaderName?.Value ?? "";
                    
                    if (ImGui.BeginCombo("##LeaderSelect", string.IsNullOrEmpty(currentManualLeader) ? "Auto-detect" : currentManualLeader))
                    {
                        // Auto-detect option
                        if (ImGui.Selectable("Auto-detect", string.IsNullOrEmpty(currentManualLeader)))
                        {
                            if (Settings?.ManualLeaderName != null)
                            {
                                Settings.ManualLeaderName.Value = "";
                                _partyManager.SetManualLeader("");
                            }
                        }
                        
                        // Player options
                        foreach (var playerName in nearbyPlayers)
                        {
                            bool isSelected = currentManualLeader.Equals(playerName, StringComparison.OrdinalIgnoreCase);
                            if (ImGui.Selectable(playerName, isSelected))
                            {
                                if (Settings?.ManualLeaderName != null)
                                {
                                    Settings.ManualLeaderName.Value = playerName;
                                    _partyManager.SetManualLeader(playerName);
                                }
                            }
                        }
                        
                        ImGui.EndCombo();
                    }
                }
                
                // Show current leader info
                var leader = _partyManager.GetPartyLeader();
                var leaderName = leader?.GetComponent<Player>()?.PlayerName ?? "None";
                var manualLeaderName = _partyManager.GetManualLeaderName();
                
                if (!string.IsNullOrEmpty(manualLeaderName))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), $"Leader: {manualLeaderName} (Manual)");
                }
                else if (_partyManager.IsInParty())
                {
                    ImGui.Text($"Leader: {leaderName} (Auto)");
                }
                
                // Show distance to leader
                if (leader != null)
                {
                    var distance = _movementManager?.GetDistanceToLeader();
                    if (distance.HasValue)
                    {
                        ImGui.Text($"Distance to Leader: {distance.Value:F1}");
                    }
                }
            }
            
            if (_movementManager != null)
            {
                var isFollowing = _movementManager.IsFollowing();
                var followColor = isFollowing ? new System.Numerics.Vector4(0, 1, 0, 1) : new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1);
                ImGui.TextColored(followColor, $"Following: {(isFollowing ? "Active" : "Idle")}");
            }
            
            if (_taskManager != null)
            {
                ImGui.Text($"Active Tasks: {_taskManager.GetActiveTaskCount()}");
            }
            
            if (_errorManager != null)
            {
                ImGui.Text($"Error Count: {_errorManager.GetErrorCount()}");
            }

            ImGui.Separator();
            
            // Basic status information
            if (GameController?.Game?.IngameState?.InGame == true)
            {
                ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), "Status: In Game");
            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Status: Not In Game");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error in DrawSettings: {ex.Message}", 1);
        }
    }

    /// <summary>
    /// Render task status overlay on screen
    /// </summary>
    private void RenderTaskOverlay()
    {
        try
        {
            if (Settings?.ShowTaskOverlay?.Value != true)
                return;

            if (!GameController?.Game?.IngameState?.InGame == true)
                return;

            var overlayX = Settings?.OverlayX?.Value ?? 10;
            var overlayY = Settings?.OverlayY?.Value ?? 200;

            // Create overlay window
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(overlayX, overlayY), FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 150), FirstUseEver);
            
            if (ImGui.Begin("AreWeThereYet2 Status", 
                NoCollapse | 
                ImGuiNET.ImGuiWindowFlags.AlwaysAutoResize |
                NoScrollbar))
            {
                // Follow Status
                var isFollowEnabled = Settings?.EnableFollowing?.Value == true;
                var followColor = isFollowEnabled ? new System.Numerics.Vector4(0, 1, 0, 1) : new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1);
                ImGui.TextColored(followColor, $"Following: {(isFollowEnabled ? "ENABLED" : "DISABLED")}");

                // Current Leader
                var leader = _partyManager?.GetPartyLeader();
                var leaderName = leader?.GetComponent<Player>()?.PlayerName ?? "None";
                var manualLeaderName = _partyManager?.GetManualLeaderName();
                
                if (!string.IsNullOrEmpty(manualLeaderName))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0, 1, 1, 1), $"Leader: {manualLeaderName} (Manual)");
                }
                else if (_partyManager?.IsInParty() == true)
                {
                    ImGui.Text($"Leader: {leaderName} (Auto)");
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Leader: None");
                }

                // Distance to Leader
                if (leader != null)
                {
                    var distance = _movementManager?.GetDistanceToLeader();
                    if (distance.HasValue)
                    {
                        var distanceColor = distance.Value > (Settings?.MaxFollowDistance?.Value ?? 30f) 
                            ? new System.Numerics.Vector4(1, 0.5f, 0, 1)  // Orange if too far
                            : new System.Numerics.Vector4(0, 1, 0, 1);    // Green if close enough
                        ImGui.TextColored(distanceColor, $"Distance: {distance.Value:F1}");
                    }
                }

                // Current Task
                var currentTask = _taskManager?.GetCurrentTask();
                if (currentTask != null)
                {
                    var taskColor = new System.Numerics.Vector4(1, 1, 0, 1); // Yellow for active tasks
                    ImGui.TextColored(taskColor, $"Task: {currentTask.Description}");
                    ImGui.Text($"Status: {currentTask.Status}");
                    
                    if (currentTask.RetryCount > 0)
                    {
                        ImGui.Text($"Retries: {currentTask.RetryCount}/{currentTask.MaxRetries}");
                    }
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1), "Task: Idle");
                }

                // Active Tasks Count
                var activeTaskCount = _taskManager?.GetActiveTaskCount() ?? 0;
                ImGui.Text($"Queue: {activeTaskCount} tasks");

                // Error Count
                var errorCount = _errorManager?.GetErrorCount() ?? 0;
                if (errorCount > 0)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), $"Errors: {errorCount}");
                }

                // Debug Information
                if (Settings?.DebugMode?.Value == true)
                {
                    ImGui.Separator();
                    ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "DEBUG LOG:");
                    
                    // Show recent debug messages in a scrollable region
                    if (ImGui.BeginChild("DebugLog", new System.Numerics.Vector2(0, 100), ImGuiNET.ImGuiChildFlags.Border))
                    {
                        // Show last 10 debug messages (newest first)
                        var recentMessages = _debugLog.TakeLast(10).Reverse().ToList();
                        foreach (var message in recentMessages)
                        {
                            ImGui.TextWrapped(message);
                        }
                        
                        // Auto-scroll to bottom for newest messages
                        if (_debugLog.Count > 0)
                        {
                            ImGui.SetScrollHereY(1.0f);
                        }
                    }
                    ImGui.EndChild();
                }
            }
            ImGui.End();
        }
        catch (Exception ex)
        {
            _errorManager?.HandleError("RenderTaskOverlay", ex);
        }
    }

    /// <summary>
    /// Get debug information for movement system
    /// </summary>
    private List<string> GetMovementDebugInfo()
    {
        var debug = new List<string>();
        
        try
        {
            // Basic game state
            debug.Add($"InGame: {GameController?.Game?.IngameState?.InGame}");
            debug.Add($"Player: {(GameController?.Player != null ? "Yes" : "No")}");
            
            // Following settings
            debug.Add($"EnableFollowing: {Settings?.EnableFollowing?.Value}");
            debug.Add($"MaxFollowDistance: {Settings?.MaxFollowDistance?.Value}");
            
            // Party and leader info
            if (_partyManager != null)
            {
                debug.Add($"IsInParty: {_partyManager.IsInParty()}");
                var leader = _partyManager.GetPartyLeader();
                debug.Add($"Leader: {(leader != null ? "Found" : "None")}");
                
                if (leader != null)
                {
                    var playerComponent = leader.GetComponent<Player>();
                    debug.Add($"LeaderName: {playerComponent?.PlayerName ?? "Unknown"}");
                }
            }
            
            // Distance calculation
            if (_movementManager != null)
            {
                var distance = _movementManager.GetDistanceToLeader();
                debug.Add($"Distance: {(distance.HasValue ? distance.Value.ToString("F1") : "N/A")}");
                debug.Add($"IsFollowing: {_movementManager.IsFollowing()}");
            }
            
            // Task manager info
            if (_taskManager != null)
            {
                debug.Add($"ActiveTasks: {_taskManager.GetActiveTaskCount()}");
                var currentTask = _taskManager.GetCurrentTask();
                debug.Add($"CurrentTask: {currentTask?.Description ?? "None"}");
                debug.Add($"HasFollowTask: {_taskManager.HasTask("follow_leader")}");
            }
        }
        catch (Exception ex)
        {
            debug.Add($"Debug Error: {ex.Message}");
        }
        
        return debug;
    }

    /// <summary>
    /// Sync manual leader setting with PartyManager
    /// </summary>
    private void SyncManualLeaderSetting()
    {
        try
        {
            if (_partyManager == null || Settings?.ManualLeaderName == null)
                return;

            var settingValue = Settings.ManualLeaderName.Value ?? "";
            var currentManualLeader = _partyManager.GetManualLeaderName() ?? "";

            // If setting changed, update party manager
            if (!settingValue.Equals(currentManualLeader, StringComparison.OrdinalIgnoreCase))
            {
                _partyManager.SetManualLeader(settingValue);
            }
        }
        catch (Exception ex)
        {
            _errorManager?.HandleError("SyncManualLeaderSetting", ex);
        }
    }

    public override void OnClose()
    {
        try
        {
            _taskManager?.Dispose();
            _partyManager?.Dispose();
            _movementManager?.Dispose();
            _errorManager?.Dispose();
            
            LogMessage("AreWeThereYet2 closed successfully", 3);
        }
        catch (Exception ex)
        {
            LogError($"Error during close: {ex.Message}", 1);
        }
    }
} 