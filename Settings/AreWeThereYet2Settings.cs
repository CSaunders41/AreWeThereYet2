using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Attributes;

namespace AreWeThereYet2.Settings;

public class AreWeThereYet2Settings : ISettings
{
    [Menu("Enable Plugin")]
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Debug Mode")]
    public ToggleNode DebugMode { get; set; } = new ToggleNode(false);

    [Menu("Party Settings", "Party and leader detection settings")]
    public EmptyNode PartyHeader { get; set; } = new EmptyNode();

    [Menu("Auto-detect Party Leader")]
    public ToggleNode AutoDetectLeader { get; set; } = new ToggleNode(true);

    [Menu("Leader Detection Update Interval (ms)")]
    public RangeNode<int> LeaderUpdateInterval { get; set; } = new RangeNode<int>(1000, 500, 5000);

    [Menu("Max Follow Distance")]
    public RangeNode<float> MaxFollowDistance { get; set; } = new RangeNode<float>(100f, 10f, 500f);

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
} 