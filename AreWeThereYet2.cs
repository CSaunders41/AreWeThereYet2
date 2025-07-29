using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using System.Numerics;
using AreWeThereYet2.Core;
using AreWeThereYet2.Party;
using AreWeThereYet2.Settings;

namespace AreWeThereYet2;

public class AreWeThereYet2 : BasePlugin
{
    private TaskManager? _taskManager;
    private PartyManager? _partyManager;
    private ErrorManager? _errorManager;
    
    public override bool Initialise()
    {
        try
        {
            // Initialize core managers
            _errorManager = new ErrorManager();
            _taskManager = new TaskManager(_errorManager);
            _partyManager = new PartyManager(GameController, _errorManager);
            
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
            ImGuiExtension.Label("AreWeThereYet2 v2.0 - Phase 0 Development");
            ImGuiExtension.Label("Enhanced follower plugin combining superior pathfinding with comprehensive features");
            
            if (_partyManager != null)
            {
                ImGuiExtension.Label($"Party Status: {(_partyManager.IsInParty() ? "In Party" : "Solo")}");
                if (_partyManager.IsInParty())
                {
                    var leader = _partyManager.GetPartyLeader();
                    ImGuiExtension.Label($"Leader: {leader?.Name ?? "Auto-detecting..."}");
                }
            }
            
            if (_taskManager != null)
            {
                ImGuiExtension.Label($"Active Tasks: {_taskManager.GetActiveTaskCount()}");
            }
            
            if (_errorManager != null)
            {
                ImGuiExtension.Label($"Error Count: {_errorManager.GetErrorCount()}");
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
            _errorManager?.Dispose();
            
            LogMessage("AreWeThereYet2 closed successfully", 3);
        }
        catch (Exception ex)
        {
            LogError($"Error during close: {ex.Message}", 1);
        }
    }
} 