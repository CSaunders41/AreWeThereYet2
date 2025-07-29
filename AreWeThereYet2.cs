using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Interfaces;
using System.Numerics;
using AreWeThereYet2.Core;
using AreWeThereYet2.Party;
using AreWeThereYet2.Movement;
using AreWeThereYet2.Settings;
using ImGuiNET;

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
            
            // Process tasks
            _taskManager?.ProcessNextTask();
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
            
            if (_partyManager != null)
            {
                ImGui.Text($"Party Status: {(_partyManager.IsInParty() ? "In Party" : "Solo")}");
                if (_partyManager.IsInParty())
                {
                    var leader = _partyManager.GetPartyLeader();
                    ImGui.Text($"Leader: {leader?.GetComponent<Player>()?.PlayerName ?? "Auto-detecting..."}");
                    
                    // Show distance to leader
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
                var followColor = isFollowing ? new Vector4(0, 1, 0, 1) : new Vector4(0.7f, 0.7f, 0.7f, 1);
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