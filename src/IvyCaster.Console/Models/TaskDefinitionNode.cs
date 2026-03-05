using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IvyCaster.Console;

public enum TaskDefinitionNodeType
{
    Group,
    Task
}

public sealed class TaskDefinitionNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _taskDefinition = string.Empty;
    private bool _isExpanded;

    public required TaskDefinitionNodeType NodeType { get; init; }

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
    public string TaskDefinition
    {
        get => _taskDefinition;
        set
        {
            if (_taskDefinition == value) return;
            _taskDefinition = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SecondaryText));
        }
    }
    public ObservableCollection<TaskDefinitionNode> Children { get; } = [];

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
        TaskDefinitionNodeType.Group => "\uE8D5",
        TaskDefinitionNodeType.Task => "\uE756",
        _ => "\uE9CE"
    };

    public string IconColor => NodeType switch
    {
        TaskDefinitionNodeType.Group => "#DCB67A",
        TaskDefinitionNodeType.Task => "#5B9BD5",
        _ => "#999999"
    };

    public string PrimaryText => Name;

    public string SecondaryText => NodeType switch
    {
        TaskDefinitionNodeType.Group => "Task Group",
        TaskDefinitionNodeType.Task => TaskDefinition,
        _ => string.Empty
    };

    public TaskDefinitionNode DeepClone(string? nameOverride = null)
    {
        var clone = new TaskDefinitionNode
        {
            NodeType = NodeType,
            Name = nameOverride ?? Name,
            IsBuiltIn = false,
            TaskDefinition = TaskDefinition,
            IsExpanded = IsExpanded
        };

        foreach (var child in Children)
            clone.Children.Add(child.DeepClone());

        return clone;
    }

    public static TaskDefinitionNode CreateGroup(string name) =>
        new()
        {
            NodeType = TaskDefinitionNodeType.Group,
            Name = name
        };

    public static TaskDefinitionNode CreateTask(string name, string taskValue) =>
        new()
        {
            NodeType = TaskDefinitionNodeType.Task,
            Name = name,
            TaskDefinition = taskValue
        };

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
