using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using AreWeThereYet2.Core;
using AreWeThereYet2.Settings;

namespace AreWeThereYet2.Party;

/// <summary>
/// Manages party status and auto-detects party leader
/// </summary>
public class PartyManager : IDisposable
{
    private readonly GameController _gameController;
    private readonly ErrorManager _errorManager;
    private readonly AreWeThereYet2Settings _settings;
    private Entity? _currentLeader;
    private Entity? _manualLeader;
    private List<Entity> _partyMembers;
    private List<Entity> _nearbyPlayers;
    private DateTime _lastPartyUpdate;
    private string? _lastKnownManualLeaderName; // Track the last manual leader name
    private bool _disposed;

    // Auto-detection settings
    private const int UpdateIntervalMs = 1000; // Update every second
    private const float MaxFollowDistance = 100f; // Max distance to consider following

    public PartyManager(GameController gameController, ErrorManager errorManager, AreWeThereYet2Settings settings)
    {
        _gameController = gameController;
        _errorManager = errorManager;
        _settings = settings;
        _partyMembers = new List<Entity>();
        _nearbyPlayers = new List<Entity>();
        _lastPartyUpdate = DateTime.MinValue;
        
        // Initialize with any existing manual leader name from settings
        _lastKnownManualLeaderName = _settings.ManualLeaderName?.Value;
    }

    /// <summary>
    /// Update party status and leader detection
    /// </summary>
    public void Update()
    {
        if (_disposed) return;

        try
        {
            // Throttle updates
            if (DateTime.UtcNow - _lastPartyUpdate < TimeSpan.FromMilliseconds(UpdateIntervalMs))
                return;

            _lastPartyUpdate = DateTime.UtcNow;

            // Update party member list
            UpdatePartyMembers();
            
            // Update nearby players list (for manual selection)
            UpdateNearbyPlayers();

            // FIXED: Handle manual leader persistence across area transitions
            RestoreManualLeaderIfNeeded();

            // Auto-detect leader if in party and no manual override
            if (IsInParty())
            {
                AutoDetectLeader();
            }
            else
            {
                _currentLeader = null;
            }
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.Update", ex);
        }
    }

    /// <summary>
    /// Check if currently in a party
    /// </summary>
    public bool IsInParty()
    {
        try
        {
            var gameState = _gameController?.Game?.IngameState;
            if (gameState == null) return false;

            var serverData = gameState.ServerData;
            if (serverData == null) return false;

            // Check if party elements exist and have members
            var partyStatusElement = gameState.IngameUi?.PartyElement;
            if (partyStatusElement == null) return false;

            return _partyMembers.Count > 0;
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.IsInParty", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the current party leader (manual override takes priority)
    /// </summary>
    public Entity? GetPartyLeader()
    {
        // FIXED: Manual leader always takes priority if set
        if (_manualLeader != null && _manualLeader.IsValid)
            return _manualLeader;
            
        return _currentLeader;
    }

    /// <summary>
    /// Get all current party members
    /// </summary>
    public List<Entity> GetPartyMembers()
    {
        return new List<Entity>(_partyMembers);
    }

    /// <summary>
    /// Manually set the party leader
    /// </summary>
    public void SetPartyLeader(Entity leader)
    {
        try
        {
            if (leader != null && _partyMembers.Contains(leader))
            {
                _currentLeader = leader;
            }
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.SetPartyLeader", ex);
        }
    }

    /// <summary>
    /// Clear the current leader (forces auto-detection)
    /// </summary>
    public void ClearLeader()
    {
        _currentLeader = null;
        _manualLeader = null;
    }

    /// <summary>
    /// Set manual leader by player name - FIXED: Persists across area transitions
    /// </summary>
    public bool SetManualLeader(string playerName)
    {
        try
        {
            if (string.IsNullOrEmpty(playerName))
            {
                // Clear manual leader
                _manualLeader = null;
                _lastKnownManualLeaderName = null;
                _settings.ManualLeaderName.Value = "";
                return true;
            }

            // FIXED: Store the name in settings for persistence across area transitions
            _settings.ManualLeaderName.Value = playerName;
            _lastKnownManualLeaderName = playerName;

            // Find player by name in nearby players
            var currentPlayer = _gameController?.Player;
            var targetPlayer = _nearbyPlayers.FirstOrDefault(p => 
            {
                try
                {
                    // CRITICAL BUG FIX: Make sure we don't select the player's own entity
                    if (p == currentPlayer) 
                    {
                        return false; // Never select the player's own entity as leader
                    }
                    
                    var player = p.GetComponent<Player>();
                    var playerName_entity = player?.PlayerName;
                    var matches = playerName_entity?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true;
                    
                    // Double-check this isn't the current player (extra safety)
                    if (matches && currentPlayer != null && p == currentPlayer)
                    {
                        return false; // Never select player's own entity
                    }
                    
                    return matches;
                }
                catch (Exception ex)
                {
                    return false;
                }
            });

            _manualLeader = targetPlayer;
            
            // Even if we can't find them right now, consider it successful 
            // because the name is stored and will be restored when they're available
            return true;
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.SetManualLeader", ex);
            return false;
        }
    }

    /// <summary>
    /// Get all nearby players for manual selection
    /// </summary>
    public List<string> GetNearbyPlayerNames()
    {
        try
        {
            var playerNames = new List<string>();
            
            foreach (var entity in _nearbyPlayers)
            {
                try
                {
                    var player = entity.GetComponent<Player>();
                    if (player?.PlayerName != null && !string.IsNullOrEmpty(player.PlayerName))
                    {
                        playerNames.Add(player.PlayerName);
                    }
                }
                catch
                {
                    // Skip invalid entities
                    continue;
                }
            }

            return playerNames.Distinct().OrderBy(name => name).ToList();
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.GetNearbyPlayerNames", ex);
            return new List<string>();
        }
    }

    /// <summary>
    /// Get the current manual leader name (if set)
    /// </summary>
    public string? GetManualLeaderName()
    {
        try
        {
            if (_manualLeader == null) return null;
            
            var player = _manualLeader.GetComponent<Player>();
            return player?.PlayerName;
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.GetManualLeaderName", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the distance to the current leader
    /// </summary>
    public float? GetDistanceToLeader()
    {
        try
        {
            if (_currentLeader == null) return null;

            var player = _gameController?.Player;
            if (player == null) return null;

            var playerPos = player.Pos;
            var leaderPos = _currentLeader.Pos;

            return (float)Math.Sqrt(
                Math.Pow(playerPos.X - leaderPos.X, 2) +
                Math.Pow(playerPos.Y - leaderPos.Y, 2));
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.GetDistanceToLeader", ex);
            return null;
        }
    }

    /// <summary>
    /// Update the list of nearby players (for manual selection)
    /// </summary>
    private void UpdateNearbyPlayers()
    {
        try
        {
            _nearbyPlayers.Clear();

            var entities = _gameController?.EntityListWrapper?.ValidEntitiesByType[ExileCore.Shared.Enums.EntityType.Player];
            if (entities == null) return;

            var player = _gameController?.Player;
            if (player == null) return;

            // Find all player entities within reasonable distance
            foreach (var entity in entities)
            {
                try
                {
                    if (entity == null || entity == player) continue;

                    // Check if entity has a valid player component
                    var playerComponent = entity.GetComponent<Player>();
                    if (playerComponent?.PlayerName == null) continue;

                    // ADDITIONAL SAFETY: Double-check this isn't the current player's entity
                    // This prevents any edge cases where entity == player check might fail
                    if (playerComponent.PlayerName.Equals(player.GetComponent<Player>()?.PlayerName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip player's own character
                    }

                    // Add to nearby players list (we'll filter by distance if needed)
                    var distance = GetDistanceBetweenEntities(player, entity);
                    if (distance <= 200f) // Reasonable range for leader selection
                    {
                        _nearbyPlayers.Add(entity);
                    }
                }
                catch (Exception ex)
                {
                    _errorManager.HandleError($"PartyManager.UpdateNearbyPlayers.EntityCheck", ex);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.UpdateNearbyPlayers", ex);
        }
    }

    /// <summary>
    /// Update the list of party members
    /// </summary>
    private void UpdatePartyMembers()
    {
        try
        {
            _partyMembers.Clear();

            var gameState = _gameController?.Game?.IngameState;
            if (gameState == null) return;

            var entities = _gameController?.EntityListWrapper?.ValidEntitiesByType[ExileCore.Shared.Enums.EntityType.Player];
            if (entities == null) return;

            var player = _gameController?.Player;
            if (player == null) return;

            // Find all player entities that are party members
            foreach (var entity in entities)
            {
                try
                {
                    if (entity == null || entity == player) continue;

                    // Check if this entity is a party member
                    // This is a simplified check - in practice, you'd want to use
                    // the actual party UI elements or server data
                    if (IsEntityPartyMember(entity))
                    {
                        _partyMembers.Add(entity);
                    }
                }
                catch (Exception ex)
                {
                    _errorManager.HandleError($"PartyManager.UpdatePartyMembers.EntityCheck", ex);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.UpdatePartyMembers", ex);
        }
    }

    /// <summary>
    /// Auto-detect the party leader based on movement patterns and positioning
    /// </summary>
    private void AutoDetectLeader()
    {
        try
        {
            if (_partyMembers.Count == 0) return;

            // If we already have a leader and they're still in the party, keep them
            if (_currentLeader != null && _partyMembers.Contains(_currentLeader))
            {
                return;
            }

            // Simple leader detection: find the party member who is furthest ahead
            // or moving the most (indicating they're leading the group)
            Entity? bestCandidate = null;
            float bestScore = float.MinValue;

            var player = _gameController?.Player;
            if (player == null) return;

            foreach (var member in _partyMembers)
            {
                try
                {
                    if (member == null) continue;

                    float score = CalculateLeaderScore(member);
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCandidate = member;
                    }
                }
                catch (Exception ex)
                {
                    _errorManager.HandleError($"PartyManager.AutoDetectLeader.ScoreCalculation", ex);
                    continue;
                }
            }

            if (bestCandidate != null && bestScore > 0)
            {
                _currentLeader = bestCandidate;
            }
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.AutoDetectLeader", ex);
        }
    }

    /// <summary>
    /// Calculate a score for how likely this entity is to be the leader
    /// Higher score = more likely to be leader
    /// Simplified version to avoid API compatibility issues
    /// </summary>
    private float CalculateLeaderScore(Entity entity)
    {
        try
        {
            if (entity == null) return 0f;

            float score = 0f;

            // Primary factor: Distance from origin (leaders typically move ahead)
            var pos = entity.Pos;
            if (pos != null)
            {
                var distanceFromOrigin = Math.Sqrt(pos.X * pos.X + pos.Y * pos.Y);
                score += (float)distanceFromOrigin * 0.1f;
            }

            // Secondary factor: Entity has Player component (confirms it's a player)
            var player = entity.GetComponent<Player>();
            if (player != null)
            {
                score += 5f; // Base score for being a player entity
                
                // Bonus if we can safely access player name (indicates healthy entity)
                try
                {
                    if (!string.IsNullOrEmpty(player.PlayerName))
                    {
                        score += 2f;
                    }
                }
                catch
                {
                    // Player name access failed, skip bonus
                }
            }

            // Tertiary factor: Entity is valid and addressable
            if (entity.IsValid && entity.Address != 0)
            {
                score += 1f;
            }

            return score;
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.CalculateLeaderScore", ex);
            return 0f;
        }
    }

    /// <summary>
    /// Check if an entity is a party member
    /// This is a simplified implementation - in practice you'd use party UI data
    /// </summary>
    private bool IsEntityPartyMember(Entity entity)
    {
        try
        {
            if (entity == null) return false;

            // Basic checks for party membership
            // In a real implementation, you'd check the party UI elements
            // or server data for actual party membership

            // For now, assume any nearby player entity could be a party member
            var player = _gameController?.Player;
            if (player == null) return false;

            var distance = GetDistanceBetweenEntities(player, entity);
            
            // If they're within reasonable distance, consider them a potential party member
            return distance <= MaxFollowDistance;
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.IsEntityPartyMember", ex);
            return false;
        }
    }

    /// <summary>
    /// Calculate distance between two entities
    /// </summary>
    private float GetDistanceBetweenEntities(Entity entity1, Entity entity2)
    {
        try
        {
            if (entity1 == null || entity2 == null) return float.MaxValue;

            var pos1 = entity1.Pos;
            var pos2 = entity2.Pos;

            return (float)Math.Sqrt(
                Math.Pow(pos1.X - pos2.X, 2) +
                Math.Pow(pos1.Y - pos2.Y, 2));
        }
        catch
        {
            return float.MaxValue;
        }
    }

    /// <summary>
    /// Restore manual leader after area transitions - CRITICAL for leader persistence
    /// </summary>
    private void RestoreManualLeaderIfNeeded()
    {
        try
        {
            // Get the current manual leader name from settings
            var currentSettingsName = _settings.ManualLeaderName?.Value;
            
            // If no manual leader is set in settings, clear everything
            if (string.IsNullOrEmpty(currentSettingsName))
            {
                if (_manualLeader != null)
                {
                    _manualLeader = null;
                    _lastKnownManualLeaderName = null;
                }
                return;
            }

            // Update our cached name if settings changed
            if (_lastKnownManualLeaderName != currentSettingsName)
            {
                _lastKnownManualLeaderName = currentSettingsName;
                _manualLeader = null; // Force re-find
            }

            // If we have a manual leader and it's still valid, we're good
            if (_manualLeader != null && _manualLeader.IsValid)
            {
                // Double-check the name matches (in case the entity changed)
                try
                {
                    var currentLeaderName = _manualLeader.GetComponent<Player>()?.PlayerName;
                    if (currentLeaderName?.Equals(_lastKnownManualLeaderName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return; // All good, leader is still valid
                    }
                    else
                    {
                        // Name doesn't match, need to re-find
                        _manualLeader = null;
                    }
                }
                catch
                {
                    // Can't read name, assume invalid
                    _manualLeader = null;
                }
            }

            // Try to find the manual leader by name in nearby players
            if (_manualLeader == null && !string.IsNullOrEmpty(_lastKnownManualLeaderName))
            {
                var targetPlayer = _nearbyPlayers.FirstOrDefault(p => 
                {
                    try
                    {
                        var player = p.GetComponent<Player>();
                        return player?.PlayerName?.Equals(_lastKnownManualLeaderName, StringComparison.OrdinalIgnoreCase) == true;
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (targetPlayer != null)
                {
                    _manualLeader = targetPlayer;
                    // Log successful restoration (optional debug info)
                }
            }
        }
        catch (Exception ex)
        {
            _errorManager.HandleError("PartyManager.RestoreManualLeaderIfNeeded", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _partyMembers.Clear();
        _nearbyPlayers.Clear();
        _currentLeader = null;
        _manualLeader = null;
        _lastKnownManualLeaderName = null;
        _disposed = true;
    }
} 