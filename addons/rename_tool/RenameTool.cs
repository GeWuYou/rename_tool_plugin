#if TOOLS
using Godot;

/// <summary>
/// 批量重命名资源文件的编辑器插件工具。
/// 该工具会在 Godot 编辑器中添加一个菜单项，用于将项目中的所有资源文件名统一为下划线命名风格（snake_case）。
/// </summary>
[Tool]
public partial class RenameTool : EditorPlugin
{
    private RenameToolPanel _panel;

    /// <summary>
    /// 当插件被加载到编辑器时调用。
    /// 添加“批量重命名资源”菜单项，并监听文件系统变更事件。
    /// </summary>
    public override void _EnterTree()
    {
        _panel =
            GD.Load<PackedScene>($"res://addons/rename_tool/rename_tool_panel.tscn").Instantiate<PanelContainer>() as
                RenameToolPanel;
        if (_panel == null)
        {
            GD.PrintErr("[RenameTool] 无法加载面板资源。");
            return;
        }

        _panel.Name = "重命名工具";
        // 右上角
        AddControlToDock(DockSlot.RightUl, _panel);
    }

    /// <summary>
    /// 当插件从编辑器卸载时调用。
    /// 移除之前添加的菜单项。
    /// </summary>
    public override void _ExitTree()
    {
        RemoveControlFromDocks(_panel);
        _panel.Free();
        _panel = null;
    }
}
#endif