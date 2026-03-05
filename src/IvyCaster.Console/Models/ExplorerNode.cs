using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IvyCaster.Console;

public enum ExplorerNodeType
{
    Group,
    Machine
}

public enum GroupKind
{
    Normal,
    Default,
    Discovery,
    AgentInstalled
}

public sealed class ExplorerNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isExpanded;

    public required ExplorerNodeType NodeType { get; init; }

    public required string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PrimaryText));
        }
    }

    public bool IsBuiltIn { get; init; }
    public GroupKind GroupKind { get; init; }
    public bool HasAgent { get; set; }
    public string IpAddress { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public ObservableCollection<ExplorerNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string IconGlyph => NodeType switch
    {
        ExplorerNodeType.Group when GroupKind == GroupKind.Discovery => "\uE968",
        ExplorerNodeType.Group when GroupKind == GroupKind.AgentInstalled => "\uE73E",
        ExplorerNodeType.Group => "\uE8D5",
        _ => "\uE7F4"
    };

    public string IconColor => NodeType switch
    {
        ExplorerNodeType.Group when GroupKind == GroupKind.Discovery => "#7B68EE",
        ExplorerNodeType.Group when GroupKind == GroupKind.AgentInstalled => "#4CAF50",
        ExplorerNodeType.Group => "#DCB67A",
        _ => "#5B9BD5"
    };

    public string PrimaryText => Name;

    public string SecondaryText => NodeType switch
    {
        ExplorerNodeType.Group when GroupKind == GroupKind.Discovery => "自動探索",
        ExplorerNodeType.Group when GroupKind == GroupKind.AgentInstalled => "展開済み",
        ExplorerNodeType.Group when IsBuiltIn => "既定",
        ExplorerNodeType.Group => "Group",
        _ => $"{IpAddress}  |  {Status}"
    };

    public ExplorerNode DeepClone(string? nameOverride = null)
    {
        var clone = new ExplorerNode
        {
            NodeType = NodeType,
            Name = nameOverride ?? Name,
            GroupKind = GroupKind,
            IpAddress = IpAddress,
            OperatingSystem = OperatingSystem,
            Status = Status,
            IsExpanded = IsExpanded
        };

        foreach (var child in Children)
            clone.Children.Add(child.DeepClone());

        return clone;
    }

    public static ExplorerNode CreateDiscoveryGroup() =>
        new()
        {
            NodeType = ExplorerNodeType.Group,
            Name = "ネットワーク探索",
            IsBuiltIn = true,
            GroupKind = GroupKind.Discovery
        };

    public static ExplorerNode CreateDefaultGroup() =>
        new()
        {
            NodeType = ExplorerNodeType.Group,
            Name = "未分類",
            IsBuiltIn = true,
            GroupKind = GroupKind.Default,
            IsExpanded = true
        };

    public static ExplorerNode CreateAgentInstalledGroup() =>
        new()
        {
            NodeType = ExplorerNodeType.Group,
            Name = "エージェント展開済み",
            IsBuiltIn = true,
            GroupKind = GroupKind.AgentInstalled,
            IsExpanded = false
        };

    public static ExplorerNode CreateGroup(string name) =>
        new()
        {
            NodeType = ExplorerNodeType.Group,
            Name = name
        };

    public static ExplorerNode CreateMachine(string name, string ipAddress, string operatingSystem, string status) =>
        new()
        {
            NodeType = ExplorerNodeType.Machine,
            Name = name,
            IpAddress = ipAddress,
            OperatingSystem = operatingSystem,
            Status = status
        };

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
