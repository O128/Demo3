using System.IO;
using System.Text.Json;
using 点胶机.Core.Enums;

namespace 点胶机.Recipe;

/// <summary>
/// 点胶配方 —— 含工艺参数 + 轨迹点列表
/// </summary>
public class DispenseRecipe
{
    public string Name { get; set; } = "Default";
    /// <summary>拍照位 X/Y</summary>
    public double PhotoPosX { get; set; } = 150;
    public double PhotoPosY { get; set; } = 150;
    /// <summary>Z 上位(安全高度)</summary>
    public double ZSafeHeight { get; set; } = 80;
    /// <summary>点胶高度(Z 下降到此处)</summary>
    public double ZDispenseHeight { get; set; } = 5;
    /// <summary>点胶速度</summary>
    public double DispenseSpeed { get; set; } = 50;
    /// <summary>胶量(mg/ms 流量)</summary>
    public double GlueFlow { get; set; } = 0.5;
    /// <summary>各轴 Home 位置</summary>
    public double HomeX { get; set; } = 0;
    public double HomeY { get; set; } = 0;
    public double HomeZ { get; set; } = 0;
    /// <summary>点胶轨迹点</summary>
    public List<DispensePoint> Points { get; set; } = new();

    /// <summary>默认配方(一个矩形点胶轨迹)</summary>
    public static DispenseRecipe CreateDefault()
    {
        return new DispenseRecipe
        {
            Name = "矩形点胶",
            Points = new()
            {
                new() { X = 100, Y = 100, Z = 5, GlueOn = true },   // 起点(开胶)
                new() { X = 200, Y = 100, Z = 5, GlueOn = true },   // 边1
                new() { X = 200, Y = 200, Z = 5, GlueOn = true },   // 边2
                new() { X = 100, Y = 200, Z = 5, GlueOn = true },   // 边3
                new() { X = 100, Y = 100, Z = 5, GlueOn = false },  // 回起点(关胶)
            }
        };
    }
}

/// <summary>点胶轨迹点</summary>
public class DispensePoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    /// <summary>到达此点时是否开胶</summary>
    public bool GlueOn { get; set; }
}

/// <summary>
/// 配方管理器 —— 负责加载/保存配方到 JSON 文件
/// </summary>
public class RecipeManager
{
    private static readonly string DefaultPath = Path.Combine(AppContext.BaseDirectory, "Config", "recipe.json");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public DispenseRecipe Current { get; private set; } = DispenseRecipe.CreateDefault();

    public void Load(string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) { Save(path); return; }
        var json = File.ReadAllText(path);
        Current = JsonSerializer.Deserialize<DispenseRecipe>(json, JsonOpts) ?? DispenseRecipe.CreateDefault();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(Current, JsonOpts));
    }
}
