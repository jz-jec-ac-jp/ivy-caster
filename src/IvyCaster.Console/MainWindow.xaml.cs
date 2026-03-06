using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace IvyCaster.Console;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ExplorerNode> _treeRoots = [];
    private readonly ObservableCollection<TaskDefinitionNode> _taskRoots = [];
    private readonly ObservableCollection<TaskItem> _taskQueue = [];
    private readonly ObservableCollection<string> _taskSteps = [];
    private readonly ExplorerNode _discoveryGroup = ExplorerNode.CreateDiscoveryGroup();
    private readonly ExplorerNode _agentInstalledGroup = ExplorerNode.CreateAgentInstalledGroup();
    private int _nextGroupNumber = 3;
    private int _nextMachineNumber = 4;
    private int _nextTaskGroupNumber = 1;
    private int _discoveryRun;
    private Point _dragStartPoint;
    private Point _taskStepDragStartPoint;
    private bool _isDragging;
    private bool _isTaskStepDragging;
    private TreeViewItem? _lastDropTarget;
    private ListBoxItem? _lastTaskStepDropTarget;
    private readonly DispatcherTimer _elapsedRefreshTimer;

    public MainWindow()
    {
        InitializeComponent();

        SeedExplorerTree();
        SeedTaskExplorerTree();
        MachineTreeView.ItemsSource = _treeRoots;
        TaskExplorerTreeView.ItemsSource = _taskRoots;
        TaskListView.ItemsSource = _taskQueue;
        TaskStepListBox.ItemsSource = _taskSteps;
        UpdateTaskStepButtonsState();
        TaskListView.Sorting += TaskListView_Sorting;
        _elapsedRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedRefreshTimer.Tick += (_, _) => RefreshRunningTaskElapsed();
        _elapsedRefreshTimer.Start();
        WriteLog("コンソール起動");
    }

    private void AddGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        var name = PromptInput("グループ追加", "グループ名を入力してください:", $"group-{_nextGroupNumber}");
        if (string.IsNullOrWhiteSpace(name))
            return;

        _nextGroupNumber++;
        var parentGroup = GetSelectedGroupNodeOrDefault();
        var newGroup = ExplorerNode.CreateGroup(name.Trim());

        parentGroup.Children.Add(newGroup);
        parentGroup.IsExpanded = true;

        StatusTextBlock.Text = $"グループ {newGroup.Name} を追加";
        WriteLog($"グループ追加: {newGroup.Name}");
    }

    private void DiscoverPcButton_OnClick(object sender, RoutedEventArgs e)
    {
        _discoveryGroup.Children.Clear();
        _discoveryRun++;

        var found = new[] {
            ("pc-discover-01", "10.1.0.101", "Windows 11"),
            ("pc-discover-02", "10.1.0.102", "Windows 10"),
            ("pc-discover-03", "10.1.0.103", "Windows 11"),
        };

        foreach (var (name, ip, os) in found)
        {
            _discoveryGroup.Children.Add(
                ExplorerNode.CreateMachine(name, ip, os, "Discovered"));
        }

        _discoveryGroup.IsExpanded = true;
        StatusTextBlock.Text = $"ネットワーク探索完了: {found.Length} 台を検出";
        WriteLog($"ネットワーク探索 #{_discoveryRun}: {found.Length} 台を検出（ダミー）");
    }

    private void AddMachineButton_OnClick(object sender, RoutedEventArgs e)
    {
        var targetGroup = GetSelectedGroupNodeOrDefault();
        var machine = AddMachineToGroup(targetGroup);
        StatusTextBlock.Text = $"{targetGroup.Name} に {machine.Name} を追加";
        WriteLog($"マシン追加: {machine.Name} -> {targetGroup.Name}");
    }

    private void AddTaskGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        var name = PromptInput("タスクグループ追加", "タスクグループ名を入力してください:", $"task-group-{_nextTaskGroupNumber}");
        if (string.IsNullOrWhiteSpace(name))
            return;

        _nextTaskGroupNumber++;
        var group = TaskDefinitionNode.CreateGroup(name.Trim());
        var parent = GetSelectedTaskGroupNode();
        if (parent != null)
        {
            parent.Children.Add(group);
            parent.IsExpanded = true;
        }
        else
        {
            _taskRoots.Add(group);
        }

        StatusTextBlock.Text = $"タスクグループ {group.Name} を追加";
        WriteLog($"タスクグループ追加: {group.Name}");
    }

    private void CreateTaskButton_OnClick(object sender, RoutedEventArgs e)
    {
        var taskValue = BuildTaskDefinitionFromSteps();
        if (string.IsNullOrWhiteSpace(taskValue))
        {
            StatusTextBlock.Text = "タスク入力が空です";
            return;
        }

        var parent = GetSelectedTaskGroupNode();
        var siblingCount = parent?.Children.Count ?? _taskRoots.Count;
        var node = TaskDefinitionNode.CreateTask($"task-{siblingCount + 1:00}", taskValue);
        if (parent != null)
        {
            parent.Children.Add(node);
            parent.IsExpanded = true;
            WriteLog($"タスク定義追加: {node.Name} -> {parent.Name}");
        }
        else
        {
            _taskRoots.Add(node);
            WriteLog($"タスク定義追加: {node.Name} -> ルート");
        }

        StatusTextBlock.Text = $"タスクを追加: {node.Name}";
        UpdateTaskDefinitionPreview(node);
    }

    private void UpdateTaskButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (TaskExplorerTreeView.SelectedItem is not TaskDefinitionNode selected ||
            selected.NodeType != TaskDefinitionNodeType.Task)
        {
            StatusTextBlock.Text = "更新対象タスクを選択してください";
            return;
        }

        var updated = BuildTaskDefinitionFromSteps();
        if (string.IsNullOrWhiteSpace(updated))
        {
            StatusTextBlock.Text = "タスク入力が空です";
            return;
        }

        selected.TaskDefinition = updated;
        UpdateTaskDefinitionPreview(selected);
        StatusTextBlock.Text = $"タスクを保存: {selected.Name}";
        WriteLog($"タスク定義保存: {selected.Name}");
    }

    private void CancelTaskEditButton_OnClick(object sender, RoutedEventArgs e)
    {
        TaskCommandBatchInputTextBox.Clear();

        if (TaskExplorerTreeView.SelectedItem is TaskDefinitionNode selected &&
            selected.NodeType == TaskDefinitionNodeType.Task)
        {
            LoadTaskSteps(selected.TaskDefinition);
            StatusTextBlock.Text = $"編集を取り消し: {selected.Name}";
            return;
        }

        _taskSteps.Clear();
        SyncTaskDefinitionFromSteps();
        StatusTextBlock.Text = "編集を取り消しました";
    }

    private void AddTaskCommandsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var commands = SplitLines(TaskCommandBatchInputTextBox.Text ?? string.Empty);
        if (commands.Count == 0)
        {
            StatusTextBlock.Text = "追加するコマンドがありません";
            return;
        }

        foreach (var command in commands)
            _taskSteps.Add($"CMD: {command}");

        TaskCommandBatchInputTextBox.Clear();
        SyncTaskDefinitionFromSteps();
        StatusTextBlock.Text = $"コマンドを {commands.Count} 件追加";
        WriteLog($"タスクステップ追加(コマンド): {commands.Count} 件");
    }

    private void AddTaskFilesButton_OnClick(object sender, RoutedEventArgs e)
    {
        var destinationRoot = (TaskFileDestinationTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            StatusTextBlock.Text = "配信先が未入力です";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "配信ファイルを選択",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
            return;

        foreach (var file in dialog.FileNames)
        {
            var fileName = Path.GetFileName(file);
            var destPath = CombineDestinationPath(destinationRoot, fileName);
            _taskSteps.Add($"FILE: {file} -> {destPath}");
        }

        SyncTaskDefinitionFromSteps();
        StatusTextBlock.Text = $"配信ファイルを {dialog.FileNames.Length} 件追加 (宛先: {destinationRoot})";
        WriteLog($"タスクステップ追加(配信): {dialog.FileNames.Length} 件 -> {destinationRoot}");
    }

    private void MoveTaskStepUpButton_OnClick(object sender, RoutedEventArgs e)
    {
        var index = TaskStepListBox.SelectedIndex;
        if (index <= 0) return;

        (_taskSteps[index - 1], _taskSteps[index]) = (_taskSteps[index], _taskSteps[index - 1]);
        TaskStepListBox.SelectedIndex = index - 1;
        SyncTaskDefinitionFromSteps();
    }

    private void MoveTaskStepDownButton_OnClick(object sender, RoutedEventArgs e)
    {
        var index = TaskStepListBox.SelectedIndex;
        if (index < 0 || index >= _taskSteps.Count - 1) return;

        (_taskSteps[index + 1], _taskSteps[index]) = (_taskSteps[index], _taskSteps[index + 1]);
        TaskStepListBox.SelectedIndex = index + 1;
        SyncTaskDefinitionFromSteps();
    }

    private void RemoveTaskStepButton_OnClick(object sender, RoutedEventArgs e)
    {
        var index = TaskStepListBox.SelectedIndex;
        if (index < 0) return;

        _taskSteps.RemoveAt(index);
        if (_taskSteps.Count > 0)
            TaskStepListBox.SelectedIndex = Math.Min(index, _taskSteps.Count - 1);
        SyncTaskDefinitionFromSteps();
    }

    private void TaskStepListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTaskStepButtonsState();
    }

    private void TaskStepListBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _taskStepDragStartPoint = e.GetPosition(TaskStepListBox);
        _isTaskStepDragging = false;
    }

    private void TaskStepListBox_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isTaskStepDragging)
            return;

        var current = e.GetPosition(TaskStepListBox);
        var diff = _taskStepDragStartPoint - current;
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var sourceIndex = GetTaskStepDropIndex(_taskStepDragStartPoint);
        if (sourceIndex < 0 || sourceIndex >= _taskSteps.Count)
            return;

        _isTaskStepDragging = true;
        var data = new DataObject(typeof(int), sourceIndex);
        DragDrop.DoDragDrop(TaskStepListBox, data, DragDropEffects.Move);
        _isTaskStepDragging = false;
        ClearTaskStepDropIndicator();
    }

    private void TaskStepListBox_OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(int)))
        {
            e.Effects = DragDropEffects.Move;
            var point = e.GetPosition(TaskStepListBox);
            if (TryGetTaskStepDropTarget(point, out var targetItem, out var insertAfter))
                SetTaskStepDropIndicator(targetItem, insertAfter);
            else
                ClearTaskStepDropIndicator();
        }
        else
        {
            e.Effects = DragDropEffects.None;
            ClearTaskStepDropIndicator();
        }

        e.Handled = true;
    }

    private void TaskStepListBox_OnDrop(object sender, DragEventArgs e)
    {
        ClearTaskStepDropIndicator();
        if (!e.Data.GetDataPresent(typeof(int)))
            return;

        var sourceIndex = (int)e.Data.GetData(typeof(int));
        if (sourceIndex < 0 || sourceIndex >= _taskSteps.Count)
            return;

        var point = e.GetPosition(TaskStepListBox);
        var targetIndex = GetTaskStepInsertIndex(point);
        if (targetIndex < 0)
            targetIndex = _taskSteps.Count;

        if (targetIndex == sourceIndex || targetIndex == sourceIndex + 1)
            return;

        var item = _taskSteps[sourceIndex];
        _taskSteps.RemoveAt(sourceIndex);
        if (targetIndex > sourceIndex)
            targetIndex--;

        _taskSteps.Insert(targetIndex, item);
        TaskStepListBox.SelectedIndex = targetIndex;
        SyncTaskDefinitionFromSteps();
    }

    private void TaskStepListBox_OnDragLeave(object sender, DragEventArgs e)
    {
        ClearTaskStepDropIndicator();
    }

    private void RenameTaskNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (TaskExplorerTreeView.SelectedItem is not TaskDefinitionNode selected)
        {
            StatusTextBlock.Text = "リネーム対象が未選択です";
            return;
        }

        if (selected.IsBuiltIn)
        {
            StatusTextBlock.Text = "既定ノードはリネームできません";
            return;
        }

        var name = PromptInput("タスク名の変更", "新しい名前を入力してください:", selected.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var old = selected.Name;
        selected.Name = name.Trim();
        StatusTextBlock.Text = $"リネーム: {old} → {selected.Name}";
        WriteLog($"タスクノード名変更: {old} → {selected.Name}");
        UpdateTaskDefinitionPreview(selected);
    }

    private void DuplicateTaskNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (TaskExplorerTreeView.SelectedItem is not TaskDefinitionNode selected)
        {
            StatusTextBlock.Text = "複製対象が未選択です";
            return;
        }

        if (selected.IsBuiltIn)
        {
            StatusTextBlock.Text = "既定ノードは複製できません";
            return;
        }

        var clone = selected.DeepClone($"{selected.Name} (コピー)");
        var parent = FindTaskParentGroup(selected);
        if (parent != null)
        {
            var idx = parent.Children.IndexOf(selected);
            parent.Children.Insert(idx + 1, clone);
            parent.IsExpanded = true;
        }
        else if (_taskRoots.Contains(selected))
        {
            var idx = _taskRoots.IndexOf(selected);
            _taskRoots.Insert(idx + 1, clone);
        }
        else
        {
            _taskRoots.Add(clone);
        }

        StatusTextBlock.Text = $"{selected.Name} を複製";
        WriteLog($"タスクノード複製: {selected.Name} → {clone.Name}");
        UpdateTaskDefinitionPreview(clone);
    }

    private void RemoveTaskNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (TaskExplorerTreeView.SelectedItem is not TaskDefinitionNode selected)
        {
            StatusTextBlock.Text = "削除対象が未選択です";
            return;
        }

        if (selected.IsBuiltIn)
        {
            StatusTextBlock.Text = "既定ノードは削除できません";
            return;
        }

        if (RemoveTaskNode(_taskRoots, selected))
        {
            StatusTextBlock.Text = $"{selected.Name} を削除";
            WriteLog($"タスクノード削除: {selected.Name}");
            return;
        }

        StatusTextBlock.Text = "削除に失敗しました";
    }

    private void TaskExplorerTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (TaskExplorerTreeView.SelectedItem is TaskDefinitionNode selected)
        {
            UpdateTaskDefinitionPreview(selected);
            UpdateTaskEditorState(selected);
        }
        else
        {
            UpdateTaskEditorState(null);
        }
    }

    private void UpdateTaskDefinitionPreview(TaskDefinitionNode node)
    {
        _ = node;
    }

    private void DuplicateNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedNode() is not { } selected)
        {
            StatusTextBlock.Text = "複製対象が未選択です";
            return;
        }

        if (selected.IsBuiltIn)
        {
            StatusTextBlock.Text = "既定グループは複製できません";
            WriteLog($"複製拒否: 既定グループ {selected.Name}");
            return;
        }

        var suffix = selected.NodeType == ExplorerNodeType.Group ? " (コピー)" : "";
        var clone = selected.DeepClone(selected.Name + suffix);

        var parent = FindParentGroup(selected);
        if (parent != null)
        {
            var idx = parent.Children.IndexOf(selected);
            parent.Children.Insert(idx + 1, clone);
        }
        else if (_treeRoots.Contains(selected))
        {
            var idx = _treeRoots.IndexOf(selected);
            _treeRoots.Insert(idx + 1, clone);
        }

        StatusTextBlock.Text = $"{selected.Name} を複製";
        WriteLog($"ノード複製: {selected.Name} → {clone.Name} (子 {CollectMachines(clone).Count} 台)");
    }

    private void RenameNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedNode() is not { } selected)
        {
            StatusTextBlock.Text = "リネーム対象が未選択です";
            return;
        }

        if (selected.IsBuiltIn)
        {
            StatusTextBlock.Text = "既定グループはリネームできません";
            return;
        }

        if (selected.NodeType != ExplorerNodeType.Group)
        {
            StatusTextBlock.Text = "マシンのリネームはできません";
            return;
        }

        var newName = PromptInput("グループ名の変更", "新しいグループ名を入力してください:", selected.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == selected.Name) return;

        var oldName = selected.Name;
        selected.Name = newName.Trim();
        StatusTextBlock.Text = $"リネーム: {oldName} → {selected.Name}";
        WriteLog($"グループ名変更: {oldName} → {selected.Name}");
    }

    private void RemoveNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedNode() is not { } selected)
        {
            StatusTextBlock.Text = "削除対象が未選択です";
            return;
        }

        if (selected.IsBuiltIn)
        {
            StatusTextBlock.Text = "既定グループは削除できません";
            WriteLog($"削除拒否: 既定グループ {selected.Name}");
            return;
        }

        if (_treeRoots.Contains(selected))
        {
            StatusTextBlock.Text = "最上位グループは削除できません";
            WriteLog($"削除拒否: ルートノード {selected.Name}");
            return;
        }

        if (RemoveNode(_treeRoots, selected))
        {
            StatusTextBlock.Text = $"{selected.Name} を削除";
            WriteLog($"ノード削除: {selected.Name}");
            return;
        }

        StatusTextBlock.Text = "削除に失敗しました";
    }

    private void CollapseAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var root in _treeRoots)
        {
            CollapseRecursively(root);
        }

        StatusTextBlock.Text = "すべて折りたたみました";
        WriteLog("エクスプローラーを折りたたみ");
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AboutMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "ivy-caster Console\nMachine Explorer mock UI",
            "About ivy-caster",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    private void DeployAgentButton_OnClick(object sender, RoutedEventArgs e)
    {
        var targets = GetSelectedMachineTargets(excludeAgentInstalledMirrors: true);
        if (targets.Count == 0)
        {
            StatusTextBlock.Text = "展開対象が未選択です";
            WriteLog("エージェント展開: 対象未選択");
            return;
        }

        foreach (var m in targets)
        {
            var task = new TaskItem("エージェント展開", m.Name, m.IpAddress);
            _taskQueue.Insert(0, task);
            WriteLog($"WMI初回展開を送信: host={m.Name}, ip={m.IpAddress}");
            SimulateTaskProgress(task, m);
        }

        StatusTextBlock.Text = $"{targets.Count} 台に展開要求を送信";
    }

    private void HeartbeatButton_OnClick(object sender, RoutedEventArgs e)
    {
        var targets = GetSelectedMachineTargets();
        if (targets.Count == 0)
        {
            StatusTextBlock.Text = "疎通確認対象が未選択です";
            WriteLog("疎通確認: 対象未選択");
            return;
        }

        foreach (var m in targets)
        {
            var task = new TaskItem("疎通確認", m.Name, m.IpAddress);
            _taskQueue.Insert(0, task);
            WriteLog($"ハートビート送信（ダミー）: host={m.Name}");
            SimulateTaskProgress(task);
        }

        StatusTextBlock.Text = $"{targets.Count} 台に疎通確認";
    }

    private void TaskListView_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var column = e.Column;
        if (string.IsNullOrWhiteSpace(column.SortMemberPath))
        {
            StatusTextBlock.Text = "この列は並び替えできません";
            return;
        }

        var direction = column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        column.SortDirection = direction;

        var view = CollectionViewSource.GetDefaultView(TaskListView.ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(column.SortMemberPath, direction));
    }

    private void SimulateTaskProgress(TaskItem task, ExplorerNode? machine = null)
    {
        if (machine != null && task.TaskType == "エージェント展開")
        {
            machine.HasAgent = true;
            UpsertAgentInstalledMirror(machine);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            if (task.Status == TaskStatus.Pending)
            {
                task.Status = TaskStatus.Running;
            }
            else
            {
                var success = !task.TargetHost.Contains("02");
                task.Status = success ? TaskStatus.Completed : TaskStatus.Failed;
                task.FinishedAt = DateTime.Now;

                if (machine != null && task.TaskType == "エージェント展開" && !success)
                {
                    machine.HasAgent = false;
                    RemoveAgentInstalledMirror(machine);
                }

                WriteLog($"タスク完了: {task.TaskType} -> {task.TargetHost} [{task.Status}]");
                timer.Stop();
            }
        };
        timer.Start();
    }

    private void UpsertAgentInstalledMirror(ExplorerNode machine)
    {
        if (_agentInstalledGroup.Children.Any(x => x.IpAddress == machine.IpAddress))
            return;

        var mirror = ExplorerNode.CreateMachine(
            machine.Name,
            machine.IpAddress,
            machine.OperatingSystem,
            machine.Status
        );
        mirror.HasAgent = true;
        _agentInstalledGroup.Children.Add(mirror);
    }

    private void RemoveAgentInstalledMirror(ExplorerNode machine)
    {
        var mirror = _agentInstalledGroup.Children.FirstOrDefault(x => x.IpAddress == machine.IpAddress);
        if (mirror != null)
            _agentInstalledGroup.Children.Remove(mirror);
    }

    private void MachineTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (GetSelectedNode() is not { } selected)
        {
            SelectedPcTextBlock.Text = "選択中: なし";
            return;
        }

        if (selected.NodeType == ExplorerNodeType.Machine)
        {
            SelectedPcTextBlock.Text = $"選択中: {selected.Name} ({selected.IpAddress})";
            return;
        }

        var count = CollectMachines(selected).Count;
        SelectedPcTextBlock.Text = $"選択中グループ: {selected.Name} ({count} 台)";
    }

    private List<ExplorerNode> GetSelectedMachineTargets(bool excludeAgentInstalledMirrors = false)
    {
        if (GetSelectedNode() is not { } selected)
            return [];

        if (selected.NodeType == ExplorerNodeType.Machine)
        {
            if (excludeAgentInstalledMirrors && FindParentGroup(selected)?.GroupKind == GroupKind.AgentInstalled)
                return [];
            return [selected];
        }

        var machines = CollectMachines(selected);
        if (!excludeAgentInstalledMirrors)
            return machines;

        return machines
            .Where(m => FindParentGroup(m)?.GroupKind != GroupKind.AgentInstalled)
            .ToList();
    }

    private static List<ExplorerNode> CollectMachines(ExplorerNode group)
    {
        var result = new List<ExplorerNode>();
        foreach (var child in group.Children)
        {
            if (child.NodeType == ExplorerNodeType.Machine)
                result.Add(child);
            else
                result.AddRange(CollectMachines(child));
        }
        return result;
    }

    private ExplorerNode? GetSelectedNode() => MachineTreeView.SelectedItem as ExplorerNode;

    private ExplorerNode GetSelectedGroupNodeOrDefault()
    {
        if (MachineTreeView.SelectedItem is ExplorerNode node && node.NodeType == ExplorerNodeType.Group)
        {
            if (node.GroupKind == GroupKind.AgentInstalled || node.GroupKind == GroupKind.Discovery)
                return _treeRoots.First(n => n.GroupKind == GroupKind.Default);
            return node;
        }

        return _treeRoots.First(n => n.GroupKind == GroupKind.Default);
    }

    private TaskDefinitionNode? GetSelectedTaskGroupNode()
    {
        if (TaskExplorerTreeView.SelectedItem is TaskDefinitionNode node)
        {
            if (node.NodeType == TaskDefinitionNodeType.Group)
                return node;

            var parent = FindTaskParentGroup(node);
            if (parent != null)
                return parent;
        }

        return null;
    }

    private void SeedExplorerTree()
    {
        var defaultGroup = ExplorerNode.CreateDefaultGroup();

        var groupA = ExplorerNode.CreateGroup("group-a");
        groupA.Children.Add(ExplorerNode.CreateMachine("pc-lab-01", "10.1.0.11", "Windows 11", "Online"));
        groupA.Children.Add(ExplorerNode.CreateMachine("pc-lab-02", "10.1.0.12", "Windows 11", "Offline"));

        var groupB = ExplorerNode.CreateGroup("group-b");
        groupB.Children.Add(ExplorerNode.CreateMachine("pc-lab-03", "10.1.0.13", "Windows 11", "Online"));

        _treeRoots.Add(_discoveryGroup);
        _treeRoots.Add(_agentInstalledGroup);
        _treeRoots.Add(defaultGroup);
        _treeRoots.Add(groupA);
        _treeRoots.Add(groupB);

        groupA.IsExpanded = true;
        groupB.IsExpanded = true;
    }

    private void SeedTaskExplorerTree()
    {
        _taskRoots.Add(TaskDefinitionNode.CreateTask("hostname取得", "hostname"));
        _taskRoots.Add(TaskDefinitionNode.CreateTask("agent配布", @"copy C:\package\agent.zip C:\temp\agent.zip"));
    }

    private ExplorerNode AddMachineToGroup(ExplorerNode targetGroup)
    {
        var index = _nextMachineNumber++;
        var machine = ExplorerNode.CreateMachine(
            $"pc-lab-{index:00}",
            $"10.1.0.{10 + index}",
            "Windows 11",
            index % 2 == 0 ? "Online" : "Offline"
        );

        targetGroup.Children.Add(machine);
        targetGroup.IsExpanded = true;
        return machine;
    }

    private static bool RemoveNode(ObservableCollection<ExplorerNode> nodes, ExplorerNode target,
        ICollection<ExplorerNode>? excludeFrom = null)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], target))
            {
                if (excludeFrom != null && ReferenceEquals(nodes, excludeFrom))
                    continue;
                nodes.RemoveAt(i);
                return true;
            }

            if (RemoveNode(nodes[i].Children, target, excludeFrom))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RemoveTaskNode(ObservableCollection<TaskDefinitionNode> nodes, TaskDefinitionNode target)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], target))
            {
                nodes.RemoveAt(i);
                return true;
            }

            if (RemoveTaskNode(nodes[i].Children, target))
                return true;
        }

        return false;
    }

    private TaskDefinitionNode? FindTaskParentGroup(TaskDefinitionNode target)
    {
        return FindTaskParentGroupIn(_taskRoots, target);
    }

    private TaskDefinitionNode? FindTaskParentGroupIn(IEnumerable<TaskDefinitionNode> nodes, TaskDefinitionNode target)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Contains(target))
                return node;
            var found = FindTaskParentGroupIn(node.Children, target);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void CollapseRecursively(ExplorerNode node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
        {
            CollapseRecursively(child);
        }
    }

    private static string? PromptInput(string title, string message, string defaultValue = "")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Owner = Application.Current.MainWindow
        };

        var textBox = new TextBox
        {
            Text = defaultValue,
            Margin = new Thickness(12, 0, 12, 0),
            Padding = new Thickness(6, 4, 6, 4)
        };
        textBox.SelectAll();

        string? result = null;
        var okButton = new Button { Content = "OK", Width = 80, Height = 28, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "キャンセル", Width = 80, Height = 28, IsCancel = true };
        okButton.Click += (_, _) => { result = textBox.Text; dialog.Close(); };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 8, 12, 12) };
        buttons.Children.Add(okButton);
        buttons.Children.Add(cancelButton);

        var stack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        stack.Children.Add(new TextBlock { Text = message, Margin = new Thickness(12, 0, 12, 8) });
        stack.Children.Add(textBox);
        stack.Children.Add(buttons);

        dialog.Content = stack;
        dialog.Loaded += (_, _) => textBox.Focus();
        dialog.ShowDialog();
        return result;
    }

    private void WriteLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} {message}";
        LogTextBox.AppendText(line + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }

    private void UpdateTaskEditorState(TaskDefinitionNode? selected)
    {
        var isTask = selected?.NodeType == TaskDefinitionNodeType.Task;
        UpdateSelectedTaskButton.IsEnabled = isTask;
        if (isTask)
        {
            LoadTaskSteps(selected!.TaskDefinition);
        }
    }

    private string BuildTaskDefinitionFromSteps()
    {
        return string.Join(Environment.NewLine, _taskSteps).Trim();
    }

    private void LoadTaskSteps(string taskDefinition)
    {
        _taskSteps.Clear();
        foreach (var line in SplitLines(taskDefinition))
            _taskSteps.Add(line);

        SyncTaskDefinitionFromSteps();
    }

    private void SyncTaskDefinitionFromSteps()
    {
        UpdateTaskStepButtonsState();
    }

    private static List<string> SplitLines(string input)
    {
        return input
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private void UpdateTaskStepButtonsState()
    {
        var idx = TaskStepListBox.SelectedIndex;
        MoveTaskStepUpButton.IsEnabled = idx > 0;
        MoveTaskStepDownButton.IsEnabled = idx >= 0 && idx < _taskSteps.Count - 1;
        RemoveTaskStepButton.IsEnabled = idx >= 0;
    }

    private void RefreshRunningTaskElapsed()
    {
        foreach (var task in _taskQueue)
        {
            if (task.Status == TaskStatus.Running)
                task.RefreshElapsed();
        }
    }

    private int GetTaskStepDropIndex(Point point)
    {
        var hit = TaskStepListBox.InputHitTest(point) as DependencyObject;
        while (hit != null && hit is not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);

        if (hit is ListBoxItem item)
            return TaskStepListBox.ItemContainerGenerator.IndexFromContainer(item);

        return -1;
    }

    private int GetTaskStepInsertIndex(Point point)
    {
        if (!TryGetTaskStepDropTarget(point, out var item, out var insertAfter))
            return -1;

        var index = TaskStepListBox.ItemContainerGenerator.IndexFromContainer(item);
        return insertAfter ? index + 1 : index;
    }

    private bool TryGetTaskStepDropTarget(Point point, out ListBoxItem targetItem, out bool insertAfter)
    {
        insertAfter = false;
        targetItem = null!;

        var hit = TaskStepListBox.InputHitTest(point) as DependencyObject;
        while (hit != null && hit is not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);

        if (hit is not ListBoxItem item)
            return false;

        var itemTopLeft = item.TranslatePoint(new Point(0, 0), TaskStepListBox);
        var midpoint = itemTopLeft.Y + (item.ActualHeight / 2);
        insertAfter = point.Y > midpoint;
        targetItem = item;
        return true;
    }

    private void SetTaskStepDropIndicator(ListBoxItem targetItem, bool insertAfter)
    {
        if (_lastTaskStepDropTarget != null && _lastTaskStepDropTarget != targetItem)
            _lastTaskStepDropTarget.Tag = null;

        targetItem.Tag = insertAfter ? "InsertBelow" : "InsertAbove";
        _lastTaskStepDropTarget = targetItem;
    }

    private void ClearTaskStepDropIndicator()
    {
        if (_lastTaskStepDropTarget == null) return;
        _lastTaskStepDropTarget.Tag = null;
        _lastTaskStepDropTarget = null;
    }

    private static string CombineDestinationPath(string destinationRoot, string fileName)
    {
        var trimmed = destinationRoot.Trim();
        if (trimmed.EndsWith("\\") || trimmed.EndsWith("/"))
            return trimmed + fileName;

        return $"{trimmed}\\{fileName}";
    }
}