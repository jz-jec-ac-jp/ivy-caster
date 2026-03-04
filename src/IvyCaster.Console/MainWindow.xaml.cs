using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
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
    private readonly ExplorerNode _discoveryGroup = ExplorerNode.CreateDiscoveryGroup();
    private readonly ExplorerNode _agentInstalledGroup = ExplorerNode.CreateAgentInstalledGroup();
    private readonly TaskDefinitionNode _taskCatalogRoot = TaskDefinitionNode.CreateRootGroup();
    private int _nextGroupNumber = 3;
    private int _nextMachineNumber = 4;
    private int _nextTaskGroupNumber = 1;
    private int _discoveryRun;
    private Point _dragStartPoint;
    private bool _isDragging;
    private TreeViewItem? _lastDropTarget;

    public MainWindow()
    {
        InitializeComponent();

        SeedExplorerTree();
        SeedTaskExplorerTree();
        MachineTreeView.ItemsSource = _treeRoots;
        TaskExplorerTreeView.ItemsSource = _taskRoots;
        TaskListView.ItemsSource = _taskQueue;
        TaskListView.Sorting += TaskListView_Sorting;
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
        var parent = GetSelectedTaskGroupNodeOrDefault();
        var group = TaskDefinitionNode.CreateGroup(name.Trim());
        parent.Children.Add(group);
        parent.IsExpanded = true;

        StatusTextBlock.Text = $"タスクグループ {group.Name} を追加";
        WriteLog($"タスクグループ追加: {group.Name}");
    }

    private void CreateCommandTaskButton_OnClick(object sender, RoutedEventArgs e)
    {
        var command = (TaskCommandTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(command))
        {
            StatusTextBlock.Text = "コマンドが空です";
            return;
        }

        var parent = GetSelectedTaskGroupNodeOrDefault();
        var node = TaskDefinitionNode.CreateCommandTask($"cmd-{parent.Children.Count + 1:00}", command);
        parent.Children.Add(node);
        parent.IsExpanded = true;

        StatusTextBlock.Text = $"コマンドタスクを追加: {node.Name}";
        WriteLog($"タスク定義追加(コマンド): {node.Name} -> {parent.Name}");
        UpdateTaskDefinitionPreview(node);
    }

    private void CreateFileTaskButton_OnClick(object sender, RoutedEventArgs e)
    {
        var path = (TaskFilePathTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusTextBlock.Text = "配布ファイルパスが空です";
            return;
        }

        var parent = GetSelectedTaskGroupNodeOrDefault();
        var node = TaskDefinitionNode.CreateFileTask($"dist-{parent.Children.Count + 1:00}", path);
        parent.Children.Add(node);
        parent.IsExpanded = true;

        StatusTextBlock.Text = $"配布タスクを追加: {node.Name}";
        WriteLog($"タスク定義追加(配布): {node.Name} -> {parent.Name}");
        UpdateTaskDefinitionPreview(node);
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
            _taskCatalogRoot.Children.Add(clone);
            _taskCatalogRoot.IsExpanded = true;
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
            TaskDefinitionPreviewTextBox.Text = "ここにコマンド/配布タスクの詳細プレビューを表示します。";
            return;
        }

        StatusTextBlock.Text = "削除に失敗しました";
    }

    private void TaskExplorerTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (TaskExplorerTreeView.SelectedItem is TaskDefinitionNode selected)
            UpdateTaskDefinitionPreview(selected);
    }

    private void UpdateTaskDefinitionPreview(TaskDefinitionNode node)
    {
        TaskDefinitionPreviewTextBox.Text = node.NodeType switch
        {
            TaskDefinitionNodeType.Group => $"[タスクグループ]\n名前: {node.Name}\n子ノード: {node.Children.Count} 件",
            TaskDefinitionNodeType.Command => $"[コマンド実行タスク]\n名前: {node.Name}\nコマンド:\n{node.CommandOrPath}",
            TaskDefinitionNodeType.FileDistribution => $"[ファイル配布タスク]\n名前: {node.Name}\n配布元:\n{node.CommandOrPath}",
            _ => node.Name
        };
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
        var targets = GetSelectedMachineTargets();
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
            if (!_agentInstalledGroup.Children.Contains(machine))
                _agentInstalledGroup.Children.Add(machine);
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
                    _agentInstalledGroup.Children.Remove(machine);
                }

                WriteLog($"タスク完了: {task.TaskType} -> {task.TargetHost} [{task.Status}]");
                timer.Stop();
            }
        };
        timer.Start();
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

    private List<ExplorerNode> GetSelectedMachineTargets()
    {
        if (GetSelectedNode() is not { } selected)
            return [];

        if (selected.NodeType == ExplorerNodeType.Machine)
            return [selected];

        return CollectMachines(selected);
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

    private TaskDefinitionNode GetSelectedTaskGroupNodeOrDefault()
    {
        if (TaskExplorerTreeView.SelectedItem is TaskDefinitionNode node)
        {
            if (node.NodeType == TaskDefinitionNodeType.Group)
                return node;

            var parent = FindTaskParentGroup(node);
            if (parent != null)
                return parent;
        }

        return _taskCatalogRoot;
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
        var common = TaskDefinitionNode.CreateGroup("共通タスク");
        common.Children.Add(TaskDefinitionNode.CreateCommandTask("hostname取得", "hostname"));

        var deploy = TaskDefinitionNode.CreateGroup("配布タスク");
        deploy.Children.Add(TaskDefinitionNode.CreateFileTask("agent配布", @"C:\package\agent.zip"));

        _taskRoots.Add(_taskCatalogRoot);
        _taskCatalogRoot.Children.Add(common);
        _taskCatalogRoot.Children.Add(deploy);
        _taskCatalogRoot.IsExpanded = true;
        common.IsExpanded = true;
        deploy.IsExpanded = true;
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

    // ── Drag & Drop ──

    private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void TreeView_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (_isDragging) return;

        if (MachineTreeView.SelectedItem is not ExplorerNode source) return;

        if (source.IsBuiltIn) return;

        if (_treeRoots.Contains(source) && source.NodeType == ExplorerNodeType.Group && source.GroupKind == GroupKind.Discovery)
            return;

        _isDragging = true;
        var data = new DataObject(typeof(ExplorerNode), source);
        DragDrop.DoDragDrop(MachineTreeView, data, DragDropEffects.Move);
        _isDragging = false;
        ClearDropTarget();
    }

    private void TreeView_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;

        if (!e.Data.GetDataPresent(typeof(ExplorerNode)))
        {
            e.Handled = true;
            return;
        }

        var source = (ExplorerNode)e.Data.GetData(typeof(ExplorerNode));
        var targetItem = FindTreeViewItemUnderMouse(e);
        var targetNode = targetItem?.DataContext as ExplorerNode;

        if (targetNode == null)
        {
            if (!_treeRoots.Contains(source))
                e.Effects = DragDropEffects.Move;
            ClearDropTarget();
        }
        else if (IsValidDropTarget(source, targetNode))
        {
            e.Effects = DragDropEffects.Move;
            SetDropTarget(targetItem!);
        }
        else
        {
            ClearDropTarget();
        }

        e.Handled = true;
    }

    private void TreeView_Drop(object sender, DragEventArgs e)
    {
        ClearDropTarget();

        if (!e.Data.GetDataPresent(typeof(ExplorerNode))) return;

        var source = (ExplorerNode)e.Data.GetData(typeof(ExplorerNode));
        var targetItem = FindTreeViewItemUnderMouse(e);
        var targetNode = targetItem?.DataContext as ExplorerNode;

        if (targetNode == null)
        {
            if (_treeRoots.Contains(source)) return;
            RemoveNode(_treeRoots, source, _agentInstalledGroup.Children);
            _treeRoots.Add(source);
            WriteLog($"移動: {source.Name} → ルート");
            StatusTextBlock.Text = $"{source.Name} をルートに移動";
            return;
        }

        if (!IsValidDropTarget(source, targetNode)) return;

        var destGroup = targetNode.NodeType == ExplorerNodeType.Group
            ? targetNode
            : FindParentGroup(targetNode);

        if (destGroup == null) return;

        RemoveNode(_treeRoots, source, _agentInstalledGroup.Children);
        destGroup.Children.Add(source);
        destGroup.IsExpanded = true;

        WriteLog($"移動: {source.Name} → {destGroup.Name}");
        StatusTextBlock.Text = $"{source.Name} を {destGroup.Name} に移動";
    }

    private void TreeView_DragLeave(object sender, DragEventArgs e)
    {
        ClearDropTarget();
    }

    private bool IsValidDropTarget(ExplorerNode source, ExplorerNode target)
    {
        if (ReferenceEquals(source, target)) return false;

        if (target.GroupKind == GroupKind.Discovery || target.GroupKind == GroupKind.AgentInstalled) return false;

        if (source.NodeType == ExplorerNodeType.Group && IsDescendant(source, target))
            return false;

        var destGroup = target.NodeType == ExplorerNodeType.Group ? target : FindParentGroup(target);
        if (destGroup == null) return false;

        var sourceParent = FindParentGroup(source);
        if (sourceParent != null && ReferenceEquals(sourceParent, destGroup)) return false;

        return true;
    }

    private bool IsDescendant(ExplorerNode ancestor, ExplorerNode candidate)
    {
        foreach (var child in ancestor.Children)
        {
            if (ReferenceEquals(child, candidate)) return true;
            if (IsDescendant(child, candidate)) return true;
        }
        return false;
    }

    private ExplorerNode? FindParentGroup(ExplorerNode target)
    {
        return FindParentGroupIn(_treeRoots, target);
    }

    private ExplorerNode? FindParentGroupIn(IEnumerable<ExplorerNode> nodes, ExplorerNode target)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Contains(target)) return node;
            var found = FindParentGroupIn(node.Children, target);
            if (found != null) return found;
        }
        return null;
    }

    private TreeViewItem? FindTreeViewItemUnderMouse(DragEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        while (hit != null)
        {
            if (hit is TreeViewItem tvi) return tvi;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    private void SetDropTarget(TreeViewItem item)
    {
        if (_lastDropTarget != null && _lastDropTarget != item)
            _lastDropTarget.Tag = null;

        item.Tag = "DropTarget";
        _lastDropTarget = item;
    }

    private void ClearDropTarget()
    {
        if (_lastDropTarget != null)
        {
            _lastDropTarget.Tag = null;
            _lastDropTarget = null;
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
}

public sealed class TreeDepthToMarginConverter : IValueConverter
{
    private const double IndentSize = 20.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DependencyObject item)
        {
            int depth = 0;
            var parent = ItemsControl.ItemsControlFromItemContainer(item);
            while (parent is TreeViewItem)
            {
                depth++;
                parent = ItemsControl.ItemsControlFromItemContainer(parent);
            }
            return new Thickness(depth * IndentSize, 0, 0, 0);
        }
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public enum ExplorerNodeType
{
    Group,
    Machine
}

public enum GroupKind { Normal, Default, Discovery, AgentInstalled }

public sealed class ExplorerNode : INotifyPropertyChanged
{
    private string _name = string.Empty;

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
    public bool IsExpanded { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
}

public enum TaskDefinitionNodeType
{
    Group,
    Command,
    FileDistribution
}

public sealed class TaskDefinitionNode : INotifyPropertyChanged
{
    private string _name = string.Empty;

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
    public string CommandOrPath { get; init; } = string.Empty;
    public ObservableCollection<TaskDefinitionNode> Children { get; } = [];
    public bool IsExpanded { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string IconGlyph => NodeType switch
    {
        TaskDefinitionNodeType.Group => "\uE8D5",
        TaskDefinitionNodeType.Command => "\uE756",
        TaskDefinitionNodeType.FileDistribution => "\uE8C3",
        _ => "\uE9CE"
    };

    public string IconColor => NodeType switch
    {
        TaskDefinitionNodeType.Group => "#DCB67A",
        TaskDefinitionNodeType.Command => "#5B9BD5",
        TaskDefinitionNodeType.FileDistribution => "#7B68EE",
        _ => "#999999"
    };

    public string PrimaryText => Name;
    public string SecondaryText => NodeType switch
    {
        TaskDefinitionNodeType.Group => "Task Group",
        TaskDefinitionNodeType.Command => CommandOrPath,
        TaskDefinitionNodeType.FileDistribution => CommandOrPath,
        _ => string.Empty
    };

    public TaskDefinitionNode DeepClone(string? nameOverride = null)
    {
        var clone = new TaskDefinitionNode
        {
            NodeType = NodeType,
            Name = nameOverride ?? Name,
            IsBuiltIn = false,
            CommandOrPath = CommandOrPath,
            IsExpanded = IsExpanded
        };

        foreach (var child in Children)
            clone.Children.Add(child.DeepClone());

        return clone;
    }

    public static TaskDefinitionNode CreateRootGroup() =>
        new()
        {
            NodeType = TaskDefinitionNodeType.Group,
            Name = "タスク定義",
            IsBuiltIn = true,
            IsExpanded = true
        };

    public static TaskDefinitionNode CreateGroup(string name) =>
        new()
        {
            NodeType = TaskDefinitionNodeType.Group,
            Name = name
        };

    public static TaskDefinitionNode CreateCommandTask(string name, string command) =>
        new()
        {
            NodeType = TaskDefinitionNodeType.Command,
            Name = name,
            CommandOrPath = command
        };

    public static TaskDefinitionNode CreateFileTask(string name, string path) =>
        new()
        {
            NodeType = TaskDefinitionNodeType.FileDistribution,
            Name = name,
            CommandOrPath = path
        };
}

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
        set { _finishedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(Elapsed)); }
    }

    public TaskStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusBadgeBg));
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
            return span.TotalMinutes >= 1 ? $"{span.Minutes}分{span.Seconds}秒" : $"{span.TotalSeconds:F0}秒";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}