using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IvyCaster.Console;

public partial class MainWindow
{
    private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private static void SelectTreeViewItemOnRightClick(MouseButtonEventArgs e)
    {
        var hit = e.OriginalSource as DependencyObject;
        while (hit is not null && hit is not TreeViewItem)
            hit = VisualTreeHelper.GetParent(hit);

        if (hit is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void MachineTreeView_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) =>
        SelectTreeViewItemOnRightClick(e);

    private void TaskExplorerTreeView_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) =>
        SelectTreeViewItemOnRightClick(e);

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

    private void TaskExplorerTreeView_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void TaskExplorerTreeView_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (_isDragging) return;
        if (TaskExplorerTreeView.SelectedItem is not TaskDefinitionNode source) return;
        if (source.IsBuiltIn) return;

        _isDragging = true;
        var data = new DataObject(typeof(TaskDefinitionNode), source);
        DragDrop.DoDragDrop(TaskExplorerTreeView, data, DragDropEffects.Move);
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
            if (!_treeRoots.Contains(source) && source.NodeType == ExplorerNodeType.Group)
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
            if (_treeRoots.Contains(source) || source.NodeType != ExplorerNodeType.Group) return;
            if (!RemoveNode(_treeRoots, source, _agentInstalledGroup.Children)) return;
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
        if (!RemoveNode(_treeRoots, source, _agentInstalledGroup.Children)) return;

        destGroup.Children.Add(source);
        destGroup.IsExpanded = true;
        WriteLog($"移動: {source.Name} → {destGroup.Name}");
        StatusTextBlock.Text = $"{source.Name} を {destGroup.Name} に移動";
    }

    private void TreeView_DragLeave(object sender, DragEventArgs e)
    {
        ClearDropTarget();
    }

    private void TaskExplorerTreeView_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;

        if (!e.Data.GetDataPresent(typeof(TaskDefinitionNode)))
        {
            e.Handled = true;
            return;
        }

        var source = (TaskDefinitionNode)e.Data.GetData(typeof(TaskDefinitionNode));
        var targetItem = FindTreeViewItemUnderMouse(e);
        var targetNode = targetItem?.DataContext as TaskDefinitionNode;

        if (targetNode != null && IsValidTaskDropTarget(source, targetNode))
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

    private void TaskExplorerTreeView_OnDrop(object sender, DragEventArgs e)
    {
        ClearDropTarget();
        if (!e.Data.GetDataPresent(typeof(TaskDefinitionNode))) return;

        var source = (TaskDefinitionNode)e.Data.GetData(typeof(TaskDefinitionNode));
        var targetItem = FindTreeViewItemUnderMouse(e);
        var targetNode = targetItem?.DataContext as TaskDefinitionNode;
        if (targetNode == null) return;
        if (!IsValidTaskDropTarget(source, targetNode)) return;

        var destGroup = targetNode.NodeType == TaskDefinitionNodeType.Group
            ? targetNode
            : FindTaskParentGroup(targetNode);
        if (destGroup == null) return;
        if (!RemoveTaskNode(_taskRoots, source)) return;

        destGroup.Children.Add(source);
        destGroup.IsExpanded = true;
        WriteLog($"タスク移動: {source.Name} → {destGroup.Name}");
        StatusTextBlock.Text = $"{source.Name} を {destGroup.Name} に移動";
    }

    private void TaskExplorerTreeView_OnDragLeave(object sender, DragEventArgs e)
    {
        ClearDropTarget();
    }

    private bool IsValidDropTarget(ExplorerNode source, ExplorerNode target)
    {
        if (ReferenceEquals(source, target)) return false;
        if (source.NodeType == ExplorerNodeType.Group && IsDescendant(source, target)) return false;

        var destGroup = target.NodeType == ExplorerNodeType.Group ? target : FindParentGroup(target);
        if (destGroup == null) return false;
        if (destGroup.GroupKind is GroupKind.Discovery or GroupKind.AgentInstalled) return false;

        var sourceParent = FindParentGroup(source);
        if (sourceParent?.GroupKind == GroupKind.AgentInstalled) return false;
        if (sourceParent != null && ReferenceEquals(sourceParent, destGroup)) return false;

        return true;
    }

    private bool IsValidTaskDropTarget(TaskDefinitionNode source, TaskDefinitionNode target)
    {
        if (ReferenceEquals(source, target)) return false;
        if (source.NodeType == TaskDefinitionNodeType.Group && IsTaskDescendant(source, target)) return false;

        var destGroup = target.NodeType == TaskDefinitionNodeType.Group ? target : FindTaskParentGroup(target);
        if (destGroup == null) return false;

        var sourceParent = FindTaskParentGroup(source);
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

    private bool IsTaskDescendant(TaskDefinitionNode ancestor, TaskDefinitionNode candidate)
    {
        foreach (var child in ancestor.Children)
        {
            if (ReferenceEquals(child, candidate)) return true;
            if (IsTaskDescendant(child, candidate)) return true;
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
        if (_lastDropTarget == null) return;
        _lastDropTarget.Tag = null;
        _lastDropTarget = null;
    }
}
