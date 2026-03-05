using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IvyCaster.Console;

public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public class TaskItem : INotifyPropertyChanged
{
    private TaskStatus _status = TaskStatus.Pending;
    private DateTime? _finishedAt;

    public TaskItem(string taskType, string targetHost, string targetIp)
    {
        TaskType = taskType;
        TargetHost = targetHost;
        TargetIp = targetIp;
        CreatedAt = DateTime.Now;
    }

    public string TaskType { get; }
    public string TargetHost { get; }
    public string TargetIp { get; }
    public DateTime CreatedAt { get; }

    public DateTime? FinishedAt
    {
        get => _finishedAt;
        set
        {
            _finishedAt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Elapsed));
        }
    }

    public TaskStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusBadgeBg));
            OnPropertyChanged(nameof(Elapsed));
        }
    }

    public string StatusLabel => Status switch
    {
        TaskStatus.Pending => "待機中",
        TaskStatus.Running => "実行中...",
        TaskStatus.Completed => "完了",
        TaskStatus.Failed => "失敗",
        _ => "不明"
    };

    public string StatusColor => Status switch
    {
        TaskStatus.Running => "#2196F3",
        TaskStatus.Completed => "#4CAF50",
        TaskStatus.Failed => "#F44336",
        _ => "#999999"
    };

    public string StatusIcon => Status switch
    {
        TaskStatus.Pending => "\uE823",
        TaskStatus.Running => "\uE895",
        TaskStatus.Completed => "\uE73E",
        TaskStatus.Failed => "\uE711",
        _ => "\uE9CE"
    };

    public string StatusBadgeBg => Status switch
    {
        TaskStatus.Running => "#E3F2FD",
        TaskStatus.Completed => "#E8F5E9",
        TaskStatus.Failed => "#FFEBEE",
        _ => "#F5F5F5"
    };

    public string TypeIcon => TaskType.Contains("展開") ? "\uE896" : "\uE704";

    public string Elapsed
    {
        get
        {
            var end = FinishedAt ?? DateTime.Now;
            var span = end - CreatedAt;
            var totalMinutes = (int)span.TotalMinutes;
            return totalMinutes >= 1 ? $"{totalMinutes}分{span.Seconds}秒" : $"{span.TotalSeconds:F0}秒";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshElapsed()
    {
        OnPropertyChanged(nameof(Elapsed));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
