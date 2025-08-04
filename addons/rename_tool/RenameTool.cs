#if TOOLS
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace rename_tool_plugin.addons.rename_tool;

/// <summary>
/// 批量重命名资源文件的编辑器插件工具。
/// 该工具会在 Godot 编辑器中添加一个菜单项，用于将项目中的所有资源文件名统一为下划线命名风格（snake_case）。
/// </summary>
[Tool]
public partial class RenameTool : EditorPlugin
{
    private RenameToolPanel _panel;

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex CamelCaseRegex();

    [GeneratedRegex(@"[\s\-]+", RegexOptions.Compiled)]
    private static partial Regex SeparatorRegex();

    /// <summary>
    /// 当插件被加载到编辑器时调用。
    /// 添加“批量重命名资源”菜单项，并监听文件系统变更事件。
    /// </summary>
    public override void _EnterTree()
    {
        _panel = new RenameToolPanel();
        _panel.Name = "重命名工具";
        _panel.RenameButton.Pressed += OnRenameResources;
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
        _panel.QueueFree();
    }

    /// <summary>
    /// 菜单项点击后的处理函数。
    /// 执行资源文件的批量重命名，并弹出提示对话框告知用户操作完成。
    /// </summary>
    private void OnRenameResources()
    {
        var fs = EditorInterface.Singleton.GetResourceFilesystem();
        RenameFilesRecursive(fs.GetFilesystem());

        GD.Print("[RenameTool] 重命名完成。可手动刷新资源面板查看效果。");
        var dialog = new AcceptDialog
        {
            Title = "重命名完成",
            DialogText = "资源文件命名已统一为下划线风格。"
        };
        EditorInterface.Singleton.GetBaseControl().AddChild(dialog);
        dialog.PopupCentered();
    }

    /// <summary>
    /// 递归遍历目录结构并重命名其中的文件。
    /// 对每个文件尝试将其名称转换为 snake_case 风格。
    /// </summary>
    /// <param name="dir">当前处理的目录对象</param>
    private void RenameFilesRecursive(EditorFileSystemDirectory dir)
    {
        // 遍历当前目录下的所有文件
        for (var i = 0; i < dir.GetFileCount(); i++)
        {
            var originalPath = dir.GetFilePath(i);
            if (!ShouldProcessFile(originalPath)) continue;
            var newFileName = ToSnakeCase(Path.GetFileName(originalPath));
            if (newFileName == Path.GetFileName(originalPath)) continue;
            // 获取 Godot 路径（res://...）中的目录部分
            // 确保 godotDir 是以 res:// 开头的目录路径
            var godotDir = originalPath.GetBaseDir();

            // 拼接 Godot 资源路径
            var godotNewPath = $"{godotDir}/{newFileName}";

            // 获取磁盘绝对路径
            var absDir = ProjectSettings.GlobalizePath(godotDir);
            var absOldPath = ProjectSettings.GlobalizePath(originalPath);
            var absNewPath = Path.Combine(absDir, newFileName).Replace("\\", "/");

            GD.Print($"[Debug] originalPath: {originalPath}");
            GD.Print($"[Debug] godotDir: {godotDir}");
            GD.Print($"[Debug] godotNewPath: {godotNewPath}");
            GD.Print($"[Debug] absDir: {absDir}");
            GD.Print($"[Debug] absOldPath: {absOldPath}");
            GD.Print($"[Debug] absNewPath: {absNewPath}");

            try
            {
                GD.Print($"[RenameTool] Renaming: {originalPath} -> {godotNewPath}");

                var accessDir = DirAccess.Open(ProjectSettings.GlobalizePath(godotDir));
                if (accessDir != null)
                {
                    var err = accessDir.Rename(absOldPath, absNewPath);
                    if (err != Error.Ok)
                    {
                        GD.PrintErr($"[RenameTool] Failed to rename {originalPath}: Error {err}");
                    }
                }
                else
                {
                    GD.PrintErr($"[RenameTool] Failed to open directory: {ProjectSettings.GlobalizePath(godotDir)}");
                }
            }
            catch (Exception e)
            {
                GD.PrintErr($"[RenameTool] Exception while renaming {originalPath}: {e.Message}: {e}");
            }
        }

        // 递归处理子目录
        for (var i = 0; i < dir.GetSubdirCount(); i++)
        {
            RenameFilesRecursive(dir.GetSubdir(i));
        }
    }

    /// <summary>
    /// 判断指定路径的文件是否应该被处理。
    /// 根据面板设置的目录和扩展名过滤条件进行判断。
    /// </summary>
    /// <param name="path">文件的完整路径</param>
    /// <returns>如果文件应被处理返回 true，否则返回 false</returns>
    private bool ShouldProcessFile(string path)
    {
        if (!_panel.GetDirectories().Any(path.StartsWith))
        {
            return false;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return _panel.GetExtensions().Contains(ext);
    }

    /// <summary>
    /// 将输入字符串转换为 snake_case 格式。
    /// 支持 CamelCase、空格、连字符等格式转为小写下划线形式。
    /// </summary>
    /// <param name="input">原始文件名（含扩展名）</param>
    /// <returns>转换后的 snake_case 文件名（保留原扩展名）</returns>
    private string ToSnakeCase(string input)
    {
        var name = Path.GetFileNameWithoutExtension(input);
        var ext = Path.GetExtension(input);

        var camel = CamelCaseRegex();
        var sep = SeparatorRegex();

        if (_panel != null && _panel.IsCustomRegexEnabled())
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_panel.GetCamelCaseRegex()))
                    camel = new Regex(_panel.GetCamelCaseRegex());

                if (!string.IsNullOrWhiteSpace(_panel.GetSeparatorRegex()))
                    sep = new Regex(_panel.GetSeparatorRegex());
            }
            catch (Exception e)
            {
                GD.PrintErr("[RenameTool] 使用用户正则失败，降级为默认：", e.Message);
            }
        }

        name = camel.Replace(name, "$1_$2");
        name = sep.Replace(name, "_");

        return name.ToLowerInvariant() + ext;
    }
}
#endif