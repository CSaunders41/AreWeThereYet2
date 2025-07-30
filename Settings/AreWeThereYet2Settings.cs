using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Attributes;
using System.Windows.Forms;

namespace AreWeThereYet2.Settings;

public class AreWeThereYet2Settings : ISettings
{
    [Menu("Enable Plugin")]
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Debug Mode")]
    public ToggleNode DebugMode { get; set; } = new ToggleNode(false);

    [Menu("Party Settings", "Party and leader detection settings")]
    public EmptyNode PartyHeader { get; set; } = new EmptyNode();

    [Menu("Enable Following")]
    public ToggleNode EnableFollowing { get; set; } = new ToggleNode(false);

    [Menu("Follow Toggle Hotkey", "Keyboard hotkey to toggle following on/off (essential when bot controls mouse)")]
    public HotkeyNode FollowToggleHotkey { get; set; } = new HotkeyNode(Keys.F1);

    [Menu("Auto-detect Party Leader")]
    public ToggleNode AutoDetectLeader { get; set; } = new ToggleNode(true);

    [Menu("Manual Leader Name (overrides auto-detect)")]
    public TextNode ManualLeaderName { get; set; } = new TextNode("");

    [Menu("Leader Detection Update Interval (ms)")]
    public RangeNode<int> LeaderUpdateInterval { get; set; } = new RangeNode<int>(1000, 500, 5000);

    [Menu("Max Follow Distance")]
    public RangeNode<float> MaxFollowDistance { get; set; } = new RangeNode<float>(30f, 10f, 500f);

    [Menu("Task Management", "Task priority and execution settings")]
    public EmptyNode TaskHeader { get; set; } = new EmptyNode();

    [Menu("Max Concurrent Tasks")]
    public RangeNode<int> MaxConcurrentTasks { get; set; } = new RangeNode<int>(1, 1, 5);

    [Menu("Task Timeout (seconds)")]
    public RangeNode<int> TaskTimeout { get; set; } = new RangeNode<int>(30, 5, 120);

    [Menu("Error Management", "Error handling and recovery settings")]
    public EmptyNode ErrorHeader { get; set; } = new EmptyNode();

    [Menu("Enable Circuit Breaker")]
    public ToggleNode EnableCircuitBreaker { get; set; } = new ToggleNode(true);

    [Menu("Max Errors Per Category")]
    public RangeNode<int> MaxErrorsPerCategory { get; set; } = new RangeNode<int>(10, 1, 50);

    [Menu("Circuit Breaker Timeout (minutes)")]
    public RangeNode<int> CircuitBreakerTimeout { get; set; } = new RangeNode<int>(5, 1, 30);

    [Menu("Overlay Settings", "On-screen task status display")]
    public EmptyNode OverlayHeader { get; set; } = new EmptyNode();

    [Menu("Show Task Overlay")]
    public ToggleNode ShowTaskOverlay { get; set; } = new ToggleNode(true);

    [Menu("Overlay X Position")]
    public RangeNode<int> OverlayX { get; set; } = new RangeNode<int>(10, 0, 1920);

    [Menu("Overlay Y Position")]
    public RangeNode<int> OverlayY { get; set; } = new RangeNode<int>(200, 0, 1080);
} 