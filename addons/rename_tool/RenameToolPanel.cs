using System.Collections.Generic;
using System.Linq;
using Godot;

namespace rename_tool_plugin.addons.rename_tool;

/// <summary>
/// RenameToolPanel 是一个用于资源重命名工具的面板界面，提供目录、扩展名配置以及自定义正则表达式的功能。
/// </summary>
public partial class RenameToolPanel : VBoxContainer
{
    private VBoxContainer _dirList;
    private VBoxContainer _extList;
    private const string ConfigKey = "settings";

    private const string ExtensionsKey = "extensions";

    private const string DirsKey = "dirs";

    private const string ConfigFilePath = "res://addons/rename_tool/config.cfg";

    private const string CamelRegexKey = "camel_case_regex";
    private const string SeparatorRegexKey = "separator_regex";
    private const string CustomRegexEnable = "custom_regex_enabled";

    /// <summary>
    /// 获取开始重命名按钮的引用。
    /// </summary>
    public Button RenameButton { get; private set; }

    private Button SaveConfigButton { get; set; }

    private Button ResetConfigButton { get; set; }

    private Button LoadConfigButton { get; set; }

    private CheckBox _enableCustomRegexCheckBox;
    private LineEdit _camelCaseRegexInput;
    private LineEdit _separatorRegexInput;


    /// <summary>
    /// 初始化 RenameToolPanel 实例并调用 Init 方法进行初始化。
    /// </summary>
    public RenameToolPanel() => Init();

    /// <summary>
    /// 初始化面板控件和界面元素。
    /// </summary>
    private void Init()
    {
        _dirList = new VBoxContainer();
        _extList = new VBoxContainer();
        AddTitle();
        var root = AddPanelRoot();
        AddDirectoryInputs(root, []); // 先创建空内容
        AddExtensionInputs(root, []);
        InitCustomRegexSection(root);
        AddControlButtons(root);
        ReloadFromConfig();
    }

    /// <summary>
    /// 创建一个带标题的分组容器（VBoxContainer）
    /// </summary>
    /// <param name="title">小节标题</param>
    /// <returns>包含标题与空内容容器的 VBox</returns>
    private VBoxContainer CreateGroupSection(string title)
    {
        var group = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 10),
        };

        var label = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 24),
        };

        group.AddChild(label);
        return group;
    }

    /// <summary>
    /// 初始化自定义正则表达式输入区域。
    /// </summary>
    /// <param name="root">父容器控件</param>
    private void InitCustomRegexSection(VBoxContainer root)
    {
        var group = CreateGroupSection("🧩 自定义正则配置");

        _enableCustomRegexCheckBox = new CheckBox { Text = "启用自定义正则规则" };
        _camelCaseRegexInput = new LineEdit { PlaceholderText = @"CamelCase 拆分: 默认 ([a-z0-9])([A-Z])" };
        _separatorRegexInput = new LineEdit { PlaceholderText = @"分隔符替换: 默认 [\s\-]+" };

        _camelCaseRegexInput.Editable = false;
        _separatorRegexInput.Editable = false;

        _enableCustomRegexCheckBox.Toggled += enabled =>
        {
            _camelCaseRegexInput.Editable = enabled;
            _separatorRegexInput.Editable = enabled;
        };

        group.AddChild(_enableCustomRegexCheckBox);
        group.AddChild(_camelCaseRegexInput);
        group.AddChild(_separatorRegexInput);

        root.AddChild(group);
    }


    /// <summary>
    /// 添加标题标签到面板顶部。
    /// </summary>
    private void AddTitle()
    {
        AddChild(new Label
        {
            Text = "⚙️ 插件配置",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(0, 24)
        });
    }

    /// <summary>
    /// 创建并添加主面板容器。
    /// </summary>
    /// <returns>返回创建的 VBoxContainer 容器</returns>
    private VBoxContainer AddPanelRoot()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.15f) });

        var root = new VBoxContainer();
        panel.AddChild(root);
        AddChild(panel);
        return root;
    }

    /// <summary>
    /// 添加目录输入控件。
    /// </summary>
    /// <param name="root">父容器控件</param>
    /// <param name="savedDirs">已保存的目录列表</param>
    private void AddDirectoryInputs(VBoxContainer root, List<string> savedDirs)
    {
        var group = CreateGroupSection("📁 处理目录列表（res:// 开头）");
        group.AddChild(_dirList);

        if (savedDirs.Count == 0)
            AddDirInput("res://assets/");
        else
            foreach (var dir in savedDirs)
                AddDirInput(dir);

        var addDirBtn = new Button { Text = "➕ 添加目录" };
        addDirBtn.Pressed += () => AddDirInput("");
        group.AddChild(addDirBtn);

        root.AddChild(group);
    }

    /// <summary>
    /// 添加扩展名输入控件。
    /// </summary>
    /// <param name="root">父容器控件</param>
    /// <param name="savedExtensions">已保存的扩展名列表</param>
    private void AddExtensionInputs(VBoxContainer root, List<string> savedExtensions)
    {
        var group = CreateGroupSection("📄 处理扩展名列表（如 .png）");
        group.AddChild(_extList);

        if (savedExtensions.Count == 0)
        {
            AddExtInput(".png");
            AddExtInput(".tscn");
            AddExtInput(".json");
        }
        else
        {
            foreach (var ext in savedExtensions)
                AddExtInput(ext);
        }

        var addExtBtn = new Button { Text = "➕ 添加扩展名" };
        addExtBtn.Pressed += () => AddExtInput("");
        group.AddChild(addExtBtn);

        root.AddChild(group);
    }

    /// <summary>
    /// 添加控制按钮（保存、重置、加载等）。
    /// </summary>
    /// <param name="root">父容器控件</param>
    private void AddControlButtons(VBoxContainer root)
    {
        var group = CreateGroupSection("⚙️ 控制操作");

        RenameButton = new Button { Text = "✅ 开始重命名" };
        RenameButton.Pressed += () => SaveConfigToFile(GetDirectories(), GetExtensions());
        group.AddChild(RenameButton);

        SaveConfigButton = new Button { Text = "💾 保存配置" };
        SaveConfigButton.Pressed += () => SaveConfigToFile(GetDirectories(), GetExtensions());
        group.AddChild(SaveConfigButton);

        ResetConfigButton = new Button { Text = "♻️ 重置为默认" };
        ResetConfigButton.Pressed += () =>
        {
            _dirList.GetChildren().ToList().ForEach(n => n.QueueFree());
            _extList.GetChildren().ToList().ForEach(n => n.QueueFree());
            AddDirInput("res://assets/");
            AddExtInput(".png");
            AddExtInput(".tscn");
            AddExtInput(".json");
        };
        group.AddChild(ResetConfigButton);

        LoadConfigButton = new Button { Text = "📂 加载配置" };
        LoadConfigButton.Pressed += ReloadFromConfig;
        group.AddChild(LoadConfigButton);

        root.AddChild(group);
    }


    /// <summary>
    /// 从配置文件重新加载目录和扩展名设置。
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
    /// </summary>
    /// <returns>返回目录路径列表</returns>
    public List<string> GetDirectories()
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
    /// </summary>
    /// <returns>返回扩展名列表</returns>
    public List<string> GetExtensions()
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
            .FindAll(e => !string.IsNullOrEmpty(e) && e.StartsWith(".") && e.Length > 1);

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
    /// 获取 CamelCase 正则表达式字符串。
    /// </summary>
    /// <returns>CamelCase 正则表达式字符串</returns>
    public string GetCamelCaseRegex() => _camelCaseRegexInput.Text.Trim();

    /// <summary>
    /// 获取分隔符替换正则表达式字符串。
    /// </summary>
    /// <returns>分隔符替换正则表达式字符串</returns>
    public string GetSeparatorRegex() => _separatorRegexInput.Text.Trim();

    /// <summary>
    /// 判断是否启用了自定义正则表达式。
    /// </summary>
    /// <returns>true 表示启用，false 表示未启用</returns>
    public bool IsCustomRegexEnabled() => _enableCustomRegexCheckBox.ButtonPressed;
}