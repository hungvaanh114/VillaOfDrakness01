using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class HouseLayoutMapGenerator
{
    private const string ScenePath = "Assets/MainGame/Game.unity";
    private const string OutputFolder = "Assets/MainGame/UI/Sprites";
    private const int ImageSize = 1536;
    private const int Padding = 96;

    private static readonly Color32 Background = new(8, 16, 24, 255);
    private static readonly Color32 Grid = new(25, 59, 75, 120);
    private static readonly Color32 Floor = new(28, 45, 56, 255);
    private static readonly Color32 Wall = new(185, 210, 220, 255);
    private static readonly Color32 Door = new(199, 147, 82, 255);
    private static readonly Color32 Window = new(103, 206, 236, 255);
    private static readonly Color32 Stairs = new(153, 128, 224, 255);
    private static readonly Color32 Column = new(220, 229, 232, 255);
    private static readonly Color32 Detail = new(90, 121, 136, 255);
    private static readonly Color32 Outline = new(154, 231, 255, 255);

    private enum LayoutCategory
    {
        Floor,
        Wall,
        Door,
        Window,
        Stairs,
        Column,
        Detail
    }

    private readonly struct LayoutItem
    {
        public LayoutItem(Bounds bounds, string name, LayoutCategory category)
        {
            Bounds = bounds;
            Name = name;
            Category = category;
        }

        public Bounds Bounds { get; }
        public string Name { get; }
        public LayoutCategory Category { get; }
    }

    private readonly struct FloorBand
    {
        public FloorBand(float centerY, float minY, float maxY, string fileSuffix)
        {
            CenterY = centerY;
            MinY = minY;
            MaxY = maxY;
            FileSuffix = fileSuffix;
        }

        public float CenterY { get; }
        public float MinY { get; }
        public float MaxY { get; }
        public string FileSuffix { get; }
    }

    [MenuItem("Tools/MainGame/Generate House Layout Map")]
    public static void Generate()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var house = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "House")
            ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(obj => obj.name == "House" && obj.scene.path == ScenePath);

        if (house == null)
        {
            Debug.LogError("HouseLayoutMapGenerator could not find root GameObject named House.");
            return;
        }

        var items = CollectItems(house);
        if (items.Count == 0)
        {
            Debug.LogError("HouseLayoutMapGenerator did not find drawable renderers under House.");
            return;
        }

        Directory.CreateDirectory(OutputFolder);

        SaveMap(items, "house_layout_overview", "Tong the", item => item.Category != LayoutCategory.Detail);

        var floorBands = BuildFloorBands(items);
        foreach (var band in floorBands)
        {
            var localBand = band;
            SaveMap(
                items,
                $"house_layout_{localBand.FileSuffix}",
                localBand.FileSuffix.Replace('_', ' '),
                item => item.Bounds.center.y >= localBand.MinY
                    && item.Bounds.center.y < localBand.MaxY
                    && !item.Name.Contains("Roof", StringComparison.OrdinalIgnoreCase));
        }

        if (floorBands.Count > 0)
            SaveFloorOneWallsDoorsStairsMap(items, floorBands[0]);

        AssetDatabase.Refresh();
        ConfigureGeneratedTextures();
        Debug.Log("Generated house layout maps in Assets/MainGame/UI/Sprites.");
    }

    private static List<LayoutItem> CollectItems(GameObject house)
    {
        var items = new List<LayoutItem>();
        foreach (var renderer in house.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                continue;

            if (!TryCategorize(renderer.name, out var category))
                continue;

            var bounds = renderer.bounds;
            if (bounds.size.x < 0.08f || bounds.size.z < 0.08f)
                continue;

            items.Add(new LayoutItem(bounds, renderer.name, category));
        }

        return items;
    }

    private static bool TryCategorize(string name, out LayoutCategory category)
    {
        if (name.Contains("Floor", StringComparison.OrdinalIgnoreCase))
        {
            category = LayoutCategory.Floor;
            return true;
        }

        if (name.Contains("Stairs", StringComparison.OrdinalIgnoreCase))
        {
            category = LayoutCategory.Stairs;
            return true;
        }

        if (name.Contains("Door", StringComparison.OrdinalIgnoreCase))
        {
            category = LayoutCategory.Door;
            return true;
        }

        if (name.Contains("WindowGlass", StringComparison.OrdinalIgnoreCase) || name.Contains("Window", StringComparison.OrdinalIgnoreCase))
        {
            category = LayoutCategory.Window;
            return true;
        }

        if (name.Contains("Column", StringComparison.OrdinalIgnoreCase))
        {
            category = LayoutCategory.Column;
            return true;
        }

        if (name.Contains("Handrail", StringComparison.OrdinalIgnoreCase) || name.Contains("Beam", StringComparison.OrdinalIgnoreCase) || name.Contains("Deco", StringComparison.OrdinalIgnoreCase))
        {
            category = LayoutCategory.Detail;
            return true;
        }

        if (name.Contains("Wall", StringComparison.OrdinalIgnoreCase) || name.Contains("Base", StringComparison.OrdinalIgnoreCase) || name.Contains("Top", StringComparison.OrdinalIgnoreCase))
        {
            category = LayoutCategory.Wall;
            return true;
        }

        category = LayoutCategory.Detail;
        return false;
    }

    private static List<FloorBand> BuildFloorBands(List<LayoutItem> items)
    {
        var floorHeights = items
            .Where(item => item.Category == LayoutCategory.Floor)
            .Select(item => item.Bounds.center.y)
            .OrderBy(y => y)
            .ToList();

        var clusters = new List<List<float>>();
        foreach (var y in floorHeights)
        {
            var cluster = clusters.FirstOrDefault(existing => Mathf.Abs(existing.Average() - y) < 1.75f);
            if (cluster == null)
                clusters.Add(new List<float> { y });
            else
                cluster.Add(y);
        }

        var centers = clusters
            .Select(cluster => cluster.Average())
            .OrderBy(y => y)
            .Take(3)
            .ToList();

        if (centers.Count == 0)
        {
            var minY = items.Min(item => item.Bounds.min.y);
            var maxY = items.Max(item => item.Bounds.max.y);
            centers.Add((minY + maxY) * 0.5f);
        }

        var bands = new List<FloorBand>();
        for (var i = 0; i < centers.Count; i++)
        {
            var min = i == 0 ? float.NegativeInfinity : (centers[i - 1] + centers[i]) * 0.5f;
            var max = i == centers.Count - 1 ? float.PositiveInfinity : (centers[i] + centers[i + 1]) * 0.5f;
            bands.Add(new FloorBand(centers[i], min, max, $"floor_{i + 1}"));
        }

        return bands;
    }

    private static void SaveMap(List<LayoutItem> allItems, string fileName, string title, Func<LayoutItem, bool> predicate)
    {
        var items = allItems.Where(predicate).ToList();
        if (items.Count == 0)
            return;

        var texture = new Texture2D(ImageSize, ImageSize, TextureFormat.RGBA32, false);
        Fill(texture, Background);
        DrawGrid(texture, 64);

        var mapBounds = BuildPlanBounds(items);
        DrawItems(texture, items.Where(item => item.Category == LayoutCategory.Floor), mapBounds, true);
        DrawItems(texture, items.Where(item => item.Category != LayoutCategory.Floor), mapBounds, false);
        DrawBorder(texture);

        var bytes = texture.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(OutputFolder, fileName + ".png"), bytes);
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static Rect BuildPlanBounds(List<LayoutItem> items)
    {
        var minX = items.Min(item => item.Bounds.min.x);
        var maxX = items.Max(item => item.Bounds.max.x);
        var minZ = items.Min(item => item.Bounds.min.z);
        var maxZ = items.Max(item => item.Bounds.max.z);

        var width = Mathf.Max(1f, maxX - minX);
        var height = Mathf.Max(1f, maxZ - minZ);
        var extra = Mathf.Max(width, height) * 0.05f;
        return Rect.MinMaxRect(minX - extra, minZ - extra, maxX + extra, maxZ + extra);
    }

    private static void SaveFloorOneWallsDoorsStairsMap(List<LayoutItem> allItems, FloorBand floorBand)
    {
        var items = allItems
            .Where(item => IsInFloorBand(item, floorBand)
                && !item.Name.Contains("Roof", StringComparison.OrdinalIgnoreCase)
                && (IsBoundaryWall(item) || item.Category == LayoutCategory.Door || item.Category == LayoutCategory.Stairs))
            .ToList();

        if (items.Count == 0)
            return;

        var texture = new Texture2D(ImageSize, ImageSize, TextureFormat.RGBA32, false);
        Fill(texture, Background);
        DrawGrid(texture, 96);

        var mapBounds = BuildPlanBounds(items);
        foreach (var item in items.Where(IsBoundaryWall).OrderBy(item => item.Bounds.size.sqrMagnitude))
            DrawBoundaryWall(texture, BoundsToPixelRect(item.Bounds, mapBounds));

        foreach (var item in items.Where(item => item.Category == LayoutCategory.Stairs))
            DrawStairsSymbol(texture, BoundsToPixelRect(item.Bounds, mapBounds));

        foreach (var item in items.Where(item => item.Category == LayoutCategory.Door))
            DrawDoorSymbol(texture, BoundsToPixelRect(item.Bounds, mapBounds));

        DrawLegend(texture);
        DrawBorder(texture);

        var bytes = texture.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(OutputFolder, "house_layout_floor_1_walls_doors_stairs.png"), bytes);
        UnityEngine.Object.DestroyImmediate(texture);
    }

    private static bool IsInFloorBand(LayoutItem item, FloorBand band)
    {
        return item.Bounds.center.y >= band.MinY && item.Bounds.center.y < band.MaxY;
    }

    private static bool IsBoundaryWall(LayoutItem item)
    {
        if (item.Category != LayoutCategory.Wall)
            return false;

        var name = item.Name;
        if (name.Contains("Int", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Deco", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Column", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Handrail", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Beam", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Stairs", StringComparison.OrdinalIgnoreCase))
            return false;

        return name.Contains("Wall_Mid", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Wall_Cor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Wall_Ext", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Base_Mid", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Base_Cor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Base_Ext", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Top_Mid", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Top_Cor", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Top_Ext", StringComparison.OrdinalIgnoreCase);
    }

    private static void DrawItems(Texture2D texture, IEnumerable<LayoutItem> items, Rect mapBounds, bool filled)
    {
        foreach (var item in items.OrderBy(item => (int)item.Category))
        {
            var rect = BoundsToPixelRect(item.Bounds, mapBounds);
            var color = CategoryColor(item.Category);
            if (filled)
                FillRect(texture, rect, WithAlpha(color, 90));

            DrawRect(texture, rect, color, item.Category == LayoutCategory.Wall ? 4 : 3);
        }
    }

    private static void DrawBoundaryWall(Texture2D texture, RectInt rect)
    {
        rect = Inflate(rect, 3);
        FillRect(texture, rect, WithAlpha(Wall, 72));
        DrawRect(texture, rect, Wall, 8);
        DrawRect(texture, Inflate(rect, 5), WithAlpha(Outline, 90), 2);
    }

    private static void DrawDoorSymbol(Texture2D texture, RectInt rect)
    {
        rect = Inflate(rect, 5);
        FillRect(texture, rect, WithAlpha(Door, 175));
        DrawRect(texture, rect, Door, 4);

        var centerX = rect.xMin + rect.width / 2;
        var centerY = rect.yMin + rect.height / 2;
        var length = Mathf.Clamp(Mathf.Max(rect.width, rect.height) * 2, 22, 72);

        if (rect.width >= rect.height)
        {
            DrawLine(texture, centerX, centerY, centerX, centerY + length, Door, 3);
            DrawLine(texture, centerX, centerY + length, centerX + length, centerY + length, WithAlpha(Door, 190), 2);
        }
        else
        {
            DrawLine(texture, centerX, centerY, centerX + length, centerY, Door, 3);
            DrawLine(texture, centerX + length, centerY, centerX + length, centerY + length, WithAlpha(Door, 190), 2);
        }
    }

    private static void DrawStairsSymbol(Texture2D texture, RectInt rect)
    {
        rect = Inflate(rect, 4);
        FillRect(texture, rect, WithAlpha(Stairs, 105));
        DrawRect(texture, rect, Stairs, 4);

        var steps = 7;
        for (var i = 1; i < steps; i++)
        {
            if (rect.width >= rect.height)
            {
                var x = rect.xMin + rect.width * i / steps;
                DrawLine(texture, x, rect.yMin, x, rect.yMax, Stairs, 2);
            }
            else
            {
                var y = rect.yMin + rect.height * i / steps;
                DrawLine(texture, rect.xMin, y, rect.xMax, y, Stairs, 2);
            }
        }
    }

    private static void DrawLegend(Texture2D texture)
    {
        DrawRect(texture, new RectInt(74, 72, 28, 28), Wall, 5);
        DrawRect(texture, new RectInt(74, 118, 28, 28), Door, 5);
        DrawRect(texture, new RectInt(74, 164, 28, 28), Stairs, 5);
    }

    private static RectInt Inflate(RectInt rect, int amount)
    {
        var xMin = Mathf.Clamp(rect.xMin - amount, 0, ImageSize - 1);
        var yMin = Mathf.Clamp(rect.yMin - amount, 0, ImageSize - 1);
        var xMax = Mathf.Clamp(rect.xMax + amount, 0, ImageSize - 1);
        var yMax = Mathf.Clamp(rect.yMax + amount, 0, ImageSize - 1);
        return new RectInt(xMin, yMin, Mathf.Max(1, xMax - xMin), Mathf.Max(1, yMax - yMin));
    }

    private static RectInt BoundsToPixelRect(Bounds bounds, Rect mapBounds)
    {
        var usable = ImageSize - Padding * 2;
        var mapScale = Mathf.Min(usable / mapBounds.width, usable / mapBounds.height);
        var offsetX = Padding + (usable - mapBounds.width * mapScale) * 0.5f;
        var offsetY = Padding + (usable - mapBounds.height * mapScale) * 0.5f;

        var x0 = Mathf.RoundToInt(offsetX + (bounds.min.x - mapBounds.xMin) * mapScale);
        var x1 = Mathf.RoundToInt(offsetX + (bounds.max.x - mapBounds.xMin) * mapScale);
        var y0 = Mathf.RoundToInt(offsetY + (bounds.min.z - mapBounds.yMin) * mapScale);
        var y1 = Mathf.RoundToInt(offsetY + (bounds.max.z - mapBounds.yMin) * mapScale);

        var minPixelX = Mathf.Clamp(Mathf.Min(x0, x1), 0, ImageSize - 1);
        var minPixelY = Mathf.Clamp(Mathf.Min(y0, y1), 0, ImageSize - 1);
        var maxPixelX = Mathf.Clamp(Mathf.Max(x0, x1), 0, ImageSize - 1);
        var maxPixelY = Mathf.Clamp(Mathf.Max(y0, y1), 0, ImageSize - 1);

        return new RectInt(minPixelX, minPixelY, Mathf.Max(1, maxPixelX - minPixelX), Mathf.Max(1, maxPixelY - minPixelY));
    }

    private static Color32 CategoryColor(LayoutCategory category)
    {
        return category switch
        {
            LayoutCategory.Floor => Floor,
            LayoutCategory.Wall => Wall,
            LayoutCategory.Door => Door,
            LayoutCategory.Window => Window,
            LayoutCategory.Stairs => Stairs,
            LayoutCategory.Column => Column,
            _ => Detail
        };
    }

    private static Color32 WithAlpha(Color32 color, byte alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void Fill(Texture2D texture, Color32 color)
    {
        var pixels = Enumerable.Repeat(color, texture.width * texture.height).ToArray();
        texture.SetPixels32(pixels);
    }

    private static void DrawGrid(Texture2D texture, int spacing)
    {
        for (var x = Padding; x < ImageSize - Padding; x += spacing)
            DrawLine(texture, x, Padding, x, ImageSize - Padding, Grid, 1);

        for (var y = Padding; y < ImageSize - Padding; y += spacing)
            DrawLine(texture, Padding, y, ImageSize - Padding, y, Grid, 1);
    }

    private static void DrawBorder(Texture2D texture)
    {
        DrawRect(texture, new RectInt(28, 28, ImageSize - 56, ImageSize - 56), Outline, 3);
        DrawRect(texture, new RectInt(44, 44, ImageSize - 88, ImageSize - 88), WithAlpha(Outline, 85), 1);
    }

    private static void FillRect(Texture2D texture, RectInt rect, Color32 color)
    {
        for (var y = rect.yMin; y <= rect.yMax; y++)
        {
            for (var x = rect.xMin; x <= rect.xMax; x++)
                BlendPixel(texture, x, y, color);
        }
    }

    private static void DrawRect(Texture2D texture, RectInt rect, Color32 color, int thickness)
    {
        for (var i = 0; i < thickness; i++)
        {
            DrawLine(texture, rect.xMin, rect.yMin + i, rect.xMax, rect.yMin + i, color, 1);
            DrawLine(texture, rect.xMin, rect.yMax - i, rect.xMax, rect.yMax - i, color, 1);
            DrawLine(texture, rect.xMin + i, rect.yMin, rect.xMin + i, rect.yMax, color, 1);
            DrawLine(texture, rect.xMax - i, rect.yMin, rect.xMax - i, rect.yMax, color, 1);
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color, int thickness)
    {
        var dx = Mathf.Abs(x1 - x0);
        var dy = Mathf.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            Plot(texture, x0, y0, color, thickness);
            if (x0 == x1 && y0 == y1)
                break;

            var e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void Plot(Texture2D texture, int x, int y, Color32 color, int thickness)
    {
        for (var oy = -thickness; oy <= thickness; oy++)
        {
            for (var ox = -thickness; ox <= thickness; ox++)
                BlendPixel(texture, x + ox, y + oy, color);
        }
    }

    private static void BlendPixel(Texture2D texture, int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
            return;

        if (color.a == 255)
        {
            texture.SetPixel(x, y, color);
            return;
        }

        var existing = texture.GetPixel(x, y);
        var alpha = color.a / 255f;
        texture.SetPixel(x, y, Color.Lerp(existing, color, alpha));
    }

    private static void ConfigureGeneratedTextures()
    {
        foreach (var path in Directory.GetFiles(OutputFolder, "house_layout_*.png"))
        {
            var assetPath = path.Replace("\\", "/");
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
