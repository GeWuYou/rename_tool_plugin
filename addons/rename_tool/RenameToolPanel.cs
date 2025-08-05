#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// RenameToolPanel 是一个用于资源重命名工具的面板界面，提供目录、扩展名配置以及自定义正则表达式的功能。
/// </summary>
[Tool]
public partial class RenameToolPanel : PanelContainer
{
    private const string ConfigKey = "settings";

    private const string ExtensionsKey = "extensions";

    private const string DirsKey = "dirs";

    private const string ConfigFilePath = $"res://addons/rename_tool/config.cfg";

    private const string CamelRegexKey = "camel_case_regex";
    private const string SeparatorRegexKey = "separator_regex";
    private const string CustomRegexEnable = "custom_regex_enabled";

    [Export] [ExportCategory("驼峰正则输入")] private LineEdit _camelCaseRegexInput;
    [Export] [ExportCategory("文件夹列表")] private VBoxContainer _dirList;

    [Export] [ExportCategory("启用自定义正则表达式单选框")]
    private CheckBox _enableCustomRegexCheckBox;

    [Export] [ExportCategory("扩展名列表")] private VBoxContainer _extList;
    [Export] [ExportCategory("根容器")] private VBoxContainer _root;
    [Export] [ExportCategory("分割正则输入")] private LineEdit _separatorRegexInput;

    /// <summary>
    /// 获取开始重命名按钮的引用。
    /// </summary>
    [Export]
    [ExportCategory("重命名按钮")]
    public Button RenameButton { get; private set; }

    [Export] [ExportCategory("添加文件夹按钮")] private Button AddDirButton { get; set; }

    [Export] [ExportCategory("添加扩展名按钮")] private Button AddExtensionButton { get; set; }

    [Export] [ExportCategory("保存配置按钮")] private Button SaveConfigButton { get; set; }

    [Export] [ExportCategory("重置配置按钮")] private Button ResetConfigButton { get; set; }

    [Export] [ExportCategory("加载配置按钮")] private Button LoadConfigButton { get; set; }

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex CamelCaseRegex();

    [GeneratedRegex(@"[\s\-]+", RegexOptions.Compiled)]
    private static partial Regex SeparatorRegex();

    /// <summary>
    /// 初始化面板控件和事件绑定。
    /// </summary>
    public override void _Ready()
    {
        InitCustomRegexSection();
        InitControlButtons();
        ReloadFromConfig();
    }


    /// <summary>
    /// 初始化自定义正则表达式输入区域。
    /// 默认情况下输入框不可编辑，当启用自定义正则复选框被勾选时才可编辑。
    /// </summary>
    private void InitCustomRegexSection()
    {
        _camelCaseRegexInput.Editable = false;
        _separatorRegexInput.Editable = false;

        _enableCustomRegexCheckBox.Toggled += enabled =>
        {
            _camelCaseRegexInput.Editable = enabled;
            _separatorRegexInput.Editable = enabled;
        };
    }


    /// <summary>
    /// 初始化控制按钮（保存、重置、加载等）。
    /// 绑定各个按钮的点击事件处理逻辑。
    /// </summary>
    private void InitControlButtons()
    {
        AddDirButton.Pressed += () => AddDirInput("");
        AddExtensionButton.Pressed += () => AddExtInput("");
        RenameButton.Pressed += () => SaveConfigToFile(GetDirectories(), GetExtensions());
        SaveConfigButton.Pressed += () => SaveConfigToFile(GetDirectories(), GetExtensions());
        ResetConfigButton.Pressed += () =>
        {
            _dirList.GetChildren().ToList().ForEach(n => n.QueueFree());
            _extList.GetChildren().ToList().ForEach(n => n.QueueFree());
            AddDirInput("res://assets/");
            AddExtInput(".png");
            AddExtInput(".tscn");
            AddExtInput(".json");
        };
        LoadConfigButton.Pressed += ReloadFromConfig;
        RenameButton.Pressed += RenameResources;
    }


    /// <summary>
    /// 从配置文件重新加载目录和扩展名设置。
    /// 清空当前界面中的输入项，并根据配置文件内容重新填充。
    /// 若无配置文件或配置为空，则使用默认值。
    /// </summary>
    private void ReloadFromConfig()
    {
        _dirList.GetChildren().ToList().ForEach(n => n.QueueFree());
        _extList.GetChildren().ToList().ForEach(n => n.QueueFree());

        LoadConfigFromFile(out var dirs, out var extensions);

        if (dirs.Count == 0) AddDirInput("res://assets/");
        else dirs.ForEach(AddDirInput);

        if (extensions.Count == 0)
        {
            AddExtInput(".png");
            AddExtInput(".tscn");
            AddExtInput(".json");
        }
        else
        {
            extensions.ForEach(AddExtInput);
        }

        GD.Print("[RenameTool] 配置已重新加载");
    }

    /// <summary>
    /// 添加一个目录输入框。
    /// </summary>
    /// <param name="defaultText">默认显示文本</param>
    private void AddDirInput(string defaultText)
    {
        var hBox = new HBoxContainer();
        var input = new LineEdit
        {
            Text = defaultText,
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill
        };
        var removeBtn = new Button
        {
            Text = "❌",
            FocusMode = FocusModeEnum.None
        };
        removeBtn.Pressed += () =>
        {
            _dirList.RemoveChild(hBox);
            hBox.QueueFree();
        };

        hBox.AddChild(input);
        hBox.AddChild(removeBtn);
        _dirList.AddChild(hBox);
    }

    /// <summary>
    /// 添加一个扩展名输入框。
    /// </summary>
    /// <param name="defaultText">默认显示文本</param>
    private void AddExtInput(string defaultText)
    {
        var hBox = new HBoxContainer();
        var input = new LineEdit
        {
            Text = defaultText,
            SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill
        };
        var removeBtn = new Button
        {
            Text = "❌",
            FocusMode = FocusModeEnum.None
        };
        removeBtn.Pressed += () =>
        {
            _extList.RemoveChild(hBox);
            hBox.QueueFree();
        };

        hBox.AddChild(input);
        hBox.AddChild(removeBtn);
        _extList.AddChild(hBox);
    }

    /// <summary>
    /// 获取所有有效的目录路径。
    /// 忽略空白或无效的输入项。
    /// </summary>
    /// <returns>返回目录路径列表</returns>
    private List<string> GetDirectories()
    {
        var list = new List<string>();
        foreach (var child in _dirList.GetChildren())
        {
            if (child is not HBoxContainer hBox || hBox.GetChild(0) is not LineEdit input) continue;
            if (!string.IsNullOrWhiteSpace(input.Text))
                list.Add(input.Text.Trim());
        }

        return list;
    }

    /// <summary>
    /// 获取所有有效的扩展名。
    /// 忽略空白或无效的输入项，并统一转为小写。
    /// </summary>
    /// <returns>返回扩展名列表</returns>
    private List<string> GetExtensions()
    {
        var list = new List<string>();
        foreach (var child in _extList.GetChildren())
        {
            if (child is not HBoxContainer hBox || hBox.GetChild(0) is not LineEdit input) continue;
            if (!string.IsNullOrWhiteSpace(input.Text))
                list.Add(input.Text.Trim().ToLowerInvariant());
        }

        return list;
    }

    /// <summary>
    /// 将当前配置保存到文件中。
    /// 包括目录、扩展名、自定义正则表达式等信息。
    /// </summary>
    /// <param name="dirs">要保存的目录列表</param>
    /// <param name="extensions">要保存的扩展名列表</param>
    private void SaveConfigToFile(List<string> dirs, List<string> extensions)
    {
        // 过滤非法路径（不是 res:// 开头）
        var validDirs = dirs
            .ConvertAll(d => d.Trim())
            .FindAll(d => !string.IsNullOrEmpty(d) && d.StartsWith("res://"));

        // 过滤非法扩展名（必须以 . 开头、且长度 > 1）
        var validExtensions = extensions
            .ConvertAll(e => e.Trim().ToLowerInvariant())
            .FindAll(e => !string.IsNullOrEmpty(e) && e.StartsWith($".") && e.Length > 1);

        if (validDirs.Count == 0 || validExtensions.Count == 0)
        {
            GD.PrintErr("[RenameTool] 保存失败：目录或扩展名配置无效。");
            ShowDialog("警告", "请填写有效的目录（以 res:// 开头）和扩展名（如 .png）！");
            return;
        }

        var enable = _enableCustomRegexCheckBox.ButtonPressed;
        var cri = _camelCaseRegexInput.Text.Trim();
        var sri = _separatorRegexInput.Text.Trim();
        if (enable && (cri.Length == 0 || sri.Length == 0))
        {
            ShowDialog("警告", "请填写有效的正则表达式！");
        }

        var config = new ConfigFile();
        config.SetValue(ConfigKey, DirsKey, validDirs.ToArray());
        config.SetValue(ConfigKey, ExtensionsKey, validExtensions.ToArray());
        config.SetValue(ConfigKey, CamelRegexKey, cri);
        config.SetValue(ConfigKey, SeparatorRegexKey, sri);
        config.SetValue(ConfigKey, CustomRegexEnable, enable);


        var err = config.Save(ConfigFilePath);
        if (err != Error.Ok)
        {
            GD.PrintErr("[RenameTool] 配置保存失败: " + err);
        }
        else
        {
            GD.Print("[RenameTool] 配置已保存到: " + ConfigFilePath);
        }
    }

    /// <summary>
    /// 显示一个对话框提示信息。
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="content">对话框内容</param>
    private static void ShowDialog(string title, string content)
    {
        EditorInterface.Singleton.GetBaseControl().AddChild(
            new AcceptDialog
            {
                Title = title,
                DialogText = content
            }
        );
    }


    /// <summary>
    /// 从配置文件加载目录和扩展名设置。
    /// </summary>
    /// <param name="dirs">输出参数，加载的目录列表</param>
    /// <param name="extensions">输出参数，加载的扩展名列表</param>
    private void LoadConfigFromFile(out List<string> dirs, out List<string> extensions)
    {
        dirs = [];
        extensions = [];

        var config = new ConfigFile();
        if (config.Load(ConfigFilePath) != Error.Ok)
        {
            GD.Print("[RenameTool] 无配置文件，使用默认配置。");
            return;
        }

        if (config.HasSectionKey(ConfigKey, DirsKey))
            dirs.AddRange((string[])config.GetValue(ConfigKey, DirsKey));

        if (config.HasSectionKey(ConfigKey, ExtensionsKey))
            extensions.AddRange((string[])config.GetValue(ConfigKey, ExtensionsKey));
        if (config.HasSectionKey(ConfigKey, CamelRegexKey))
            _camelCaseRegexInput.Text = config.GetValue(ConfigKey, CamelRegexKey).ToString();

        if (config.HasSectionKey(ConfigKey, SeparatorRegexKey))
            _separatorRegexInput.Text = config.GetValue(ConfigKey, SeparatorRegexKey).ToString();
        var enabled = false;
        if (config.HasSectionKey(ConfigKey, "custom_regex_enabled"))
            enabled = (bool)config.GetValue(ConfigKey, "custom_regex_enabled");

        _enableCustomRegexCheckBox.ButtonPressed = enabled;
        _camelCaseRegexInput.Editable = enabled;
        _separatorRegexInput.Editable = enabled;
    }


    /// <summary>
    /// 菜单项点击后的处理函数。
    /// 执行资源文件的批量重命名，并弹出提示对话框告知用户操作完成。
    /// </summary>
    private void RenameResources()
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
        if (!GetDirectories().Any(path.StartsWith))
        {
            return false;
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return GetExtensions().Contains(ext);
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

        if (IsCustomRegexEnabled())
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(GetCamelCaseRegex()))
                    camel = new Regex(GetCamelCaseRegex());

                if (!string.IsNullOrWhiteSpace(GetSeparatorRegex()))
                    sep = new Regex(GetSeparatorRegex());
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

    /// <summary>
    /// 获取 CamelCase 正则表达式字符串。
    /// </summary>
    /// <returns>CamelCase 正则表达式字符串</returns>
    private string GetCamelCaseRegex() => _camelCaseRegexInput.Text.Trim();

    /// <summary>
    /// 获取分隔符替换正则表达式字符串。
    /// </summary>
    /// <returns>分隔符替换正则表达式字符串</returns>
    private string GetSeparatorRegex() => _separatorRegexInput.Text.Trim();

    /// <summary>
    /// 判断是否启用了自定义正则表达式。
    /// </summary>
    /// <returns>true 表示启用，false 表示未启用</returns>
    private bool IsCustomRegexEnabled() => _enableCustomRegexCheckBox.ButtonPressed;
}
#endif