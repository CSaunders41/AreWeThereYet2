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

namespace AreWeThereYet2;

public class AreWeThereYet2 : BaseSettingsPlugin<AreWeThereYet2Settings>
{
    private TaskManager? _taskManager;
    private PartyManager? _partyManager;
    private ErrorManager? _errorManager;
    private MovementManager? _movementManager;
    
    public override bool Initialise()
    {
        try
        {
            // Initialize core managers
            _errorManager = new ErrorManager();
            _taskManager = new TaskManager(_errorManager);
            _partyManager = new PartyManager(GameController, _errorManager);
            _movementManager = new MovementManager(GameController, _taskManager, _partyManager, _errorManager, Settings);
            
            Name = "AreWeThereYet2";
            
            LogMessage("AreWeThereYet2 v2.0 initialized successfully", 3);
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize AreWeThereYet2: {ex.Message}", 3);
            return false;
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
            
            // Follow Toggle (prominent placement)
            if (Settings?.EnableFollowing != null)
            {
                var isFollowing = Settings.EnableFollowing.Value;
                var followColor = isFollowing ? new System.Numerics.Vector4(0, 1, 0, 1) : new System.Numerics.Vector4(1, 0.5f, 0, 1);
                ImGui.TextColored(followColor, $"Following: {(isFollowing ? "ENABLED" : "DISABLED")}");
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
                AlwaysAutoResize |
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
            }
            ImGui.End();
        }
        catch (Exception ex)
        {
            _errorManager?.HandleError("RenderTaskOverlay", ex);
        }
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