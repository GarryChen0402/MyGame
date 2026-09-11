using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// L2 of Docs/可视化UI布局编辑器-实施文档.md (design §5 L2): the visual editor
// for the UILayouts JSON assets. Pure editor tooling - it reads and writes the
// very assets the L1 runtime loader consumes, through the shared DTO
// (PanelLayoutData) and serializer (PanelLayoutSerializer), and touches no
// runtime code. The canvas is a geometric sketch (blocks + ids, no sprite
// rendering, design §4.3): element blocks are 100x100 author units, matching
// the runtime GameObjects (bar included - ProgressBarUI's default size), and
// rows of a grid run top-to-bottom like PanelLayoutRunner expands them.
public class UILayoutEditorWindow : EditorWindow
{
    private const string LayoutDir = "Assets/Resources/UILayouts";
    private const float AuthorGrid = 100f;   // design §5: 100 px author-space grid
    private const float BlockSize = 100f;    // slot/bar/grid-cell sketch block
    private const float SnapStep = 10f;
    private const float ZoomMin = 0.25f, ZoomMax = 2f;

    private static readonly Color SlotColor = new(0.35f, 0.55f, 0.85f, 0.55f);
    private static readonly Color GridColor = new(0.30f, 0.70f, 0.55f, 0.55f);
    private static readonly Color BarColor = new(0.85f, 0.55f, 0.25f, 0.55f);
    private static readonly Color SelectionColor = new(1f, 0.85f, 0.2f, 1f);

    // Import-from-Code registry (design §4.3): the C# static layouts are the
    // canonical first-hand sources; extend the table when a panel gains one.
    private static readonly (string name, Func<PanelLayout> layout)[] CodeLayouts =
    {
        ("furnace", () => FurnaceUI.Layout),
        ("crafting_table", () => CraftingTableUI.Layout),
    };

    private const int UndoLimit = 20;

    private PanelLayoutData data;
    private string currentPath;
    private string[] assetPaths = Array.Empty<string>();
    private int selectedElement = -1;
    private Vector2 listScroll, attrScroll;
    private float zoom = 1f;
    private bool snap = true;
    private bool dirty;
    private bool dragging;
    private bool dragPushed;
    private Vector2 dragBase, dragValue;
    private Vector2 mouseAuthor;
    // Object-level undo (design §4.3): the entry carries the asset path too,
    // so undoing an Import restores the previous asset alongside its state.
    private readonly List<(string path, PanelLayoutData data)> undoStack = new();
    private readonly List<string> warnings = new();
    private Vector2 warningScroll;

    [MenuItem("Tools/UI Layout Editor")]
    private static void Open()
    {
        var window = GetWindow<UILayoutEditorWindow>("UI Layout Editor");
        window.minSize = new Vector2(900, 560);
    }

    private void OnEnable() => RefreshAssetList();

    private void OnGUI()
    {
        HandleUndoShortcut();
        DrawToolbar();
        EditorGUILayout.BeginHorizontal();
        DrawLeftColumn();
        DrawCanvas();
        DrawAttributeColumn();
        EditorGUILayout.EndHorizontal();
        DrawWarningBar();
        DrawStatusBar();
    }

    // ------------------------------------------------------------------ toolbar

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        var layoutRect = GUILayoutUtility.GetRect(new GUIContent("Layout"), EditorStyles.toolbarDropDown, GUILayout.Width(90));
        if(EditorGUI.DropdownButton(layoutRect, new GUIContent("Layout"), FocusType.Passive, EditorStyles.toolbarDropDown))
            ShowLayoutMenu(layoutRect);
        var importRect = GUILayoutUtility.GetRect(new GUIContent("Import from Code"), EditorStyles.toolbarDropDown, GUILayout.Width(150));
        if(EditorGUI.DropdownButton(importRect, new GUIContent("Import from Code"), FocusType.Passive, EditorStyles.toolbarDropDown))
            ShowImportMenu(importRect);
        using(new EditorGUI.DisabledScope(data == null))
        {
            if(GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))Save();
        }
        if(GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))RefreshAssetList();
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Zoom {zoom:0.00}x", EditorStyles.miniLabel, GUILayout.Width(80));
        snap = GUILayout.Toggle(snap, "Snap 10px", EditorStyles.toolbarButton, GUILayout.Width(80));
        GUILayout.EndHorizontal();
    }

    private void ShowLayoutMenu(Rect rect)
    {
        var menu = new GenericMenu();
        if(assetPaths.Length == 0)
        {
            menu.AddDisabledItem(new GUIContent("(no assets)"));
        }
        else
        {
            foreach(var path in assetPaths)
            {
                string captured = path;
                menu.AddItem(new GUIContent(Path.GetFileNameWithoutExtension(path)), captured == currentPath,
                    () => LoadAsset(captured));
            }
        }
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Refresh List"), false, RefreshAssetList);
        menu.DropDown(rect);
    }

    private void ShowImportMenu(Rect rect)
    {
        var menu = new GenericMenu();
        foreach(var entry in CodeLayouts)
        {
            string captured = entry.name;
            menu.AddItem(new GUIContent(captured), false, () => ImportFromCode(captured));
        }
        menu.DropDown(rect);
    }

    // -------------------------------------------------------------------- left

    private void DrawLeftColumn()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(170));
        listScroll = EditorGUILayout.BeginScrollView(listScroll);
        GUILayout.Label("Layouts", EditorStyles.boldLabel);
        if(assetPaths.Length > 0)
        {
            var names = new string[assetPaths.Length];
            for(int i = 0; i < assetPaths.Length; i++)names[i] = Path.GetFileNameWithoutExtension(assetPaths[i]);
            int index = Array.IndexOf(assetPaths, currentPath);
            int picked = GUILayout.SelectionGrid(index, names, 1);
            if(picked != index && picked >= 0 && picked < assetPaths.Length)LoadAsset(assetPaths[picked]);
        }
        else
        {
            GUILayout.Label("(no assets)", EditorStyles.miniLabel);
        }
        GUILayout.Space(8);
        GUILayout.Label("Elements", EditorStyles.boldLabel);
        if(data != null && data.elements != null && data.elements.Count > 0)
        {
            var labels = new string[data.elements.Count];
            for(int i = 0; i < data.elements.Count; i++)labels[i] = ElementLabel(data.elements[i]);
            selectedElement = GUILayout.SelectionGrid(selectedElement, labels, 1);
        }
        else
        {
            selectedElement = -1;
            GUILayout.Label("(none)", EditorStyles.miniLabel);
        }
        using(new EditorGUI.DisabledScope(data == null))
        {
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("+slot"))AddElement("slot");
            if(GUILayout.Button("+grid"))AddElement("grid");
            if(GUILayout.Button("+bar"))AddElement("bar");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            using(new EditorGUI.DisabledScope(selectedElement < 0 || selectedElement >= data.elements.Count))
            {
                if(GUILayout.Button("Copy"))CopyElement();
                if(GUILayout.Button("Delete"))DeleteElement();
            }
            GUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static string ElementLabel(PanelElementData element)
        => element.kind == "grid"
            ? $"{element.idPrefix}* {element.rows}x{element.cols}"
            : $"{(string.IsNullOrEmpty(element.id) ? "?" : element.id)} ({element.kind})";

    // ------------------------------------------------------------------ canvas

    private void DrawCanvas()
    {
        Rect canvas = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(canvas, new Color(0.16f, 0.16f, 0.17f, 1f));
        if(data == null)
        {
            GUI.Label(canvas, "Select a layout (Layout menu) or Import from Code", EditorStyles.centeredGreyMiniLabel);
            return;
        }
        HandleCanvasEvents(canvas);
        GUI.BeginClip(canvas);
        DrawCanvasContent(canvas.size);
        GUI.EndClip();
    }

    // Canvas-local point (0,0 = canvas top-left) -> author coordinates, with
    // the panel center at the canvas center and +y pointing up.
    private Vector2 LocalToAuthor(Vector2 local, Vector2 center)
        => new((local.x - center.x) / zoom, -(local.y - center.y) / zoom);

    private void HandleCanvasEvents(Rect canvas)
    {
        var e = Event.current;
        if(dragging && e.type == EventType.MouseUp)
        {
            dragging = false;
            e.Use();
            return;
        }
        if(!canvas.Contains(e.mousePosition))return;
        Vector2 center = canvas.size * 0.5f;
        Vector2 author = LocalToAuthor(e.mousePosition - new Vector2(canvas.x, canvas.y), center);
        mouseAuthor = author;
        switch(e.type)
        {
            case EventType.MouseDown when e.button == 0:
                selectedElement = HitTest(author);
                if(selectedElement >= 0)
                {
                    dragging = true;
                    dragPushed = false;
                    dragBase = author;
                    dragValue = ElementPos(data.elements[selectedElement]);
                }
                e.Use();
                Repaint();
                break;
            case EventType.MouseDrag when dragging && selectedElement >= 0 && selectedElement < data.elements.Count:
                // The undo entry captures the pre-drag state at the first
                // move, so a click that only selects never pollutes the stack.
                if(!dragPushed)
                {
                    PushUndo();
                    dragPushed = true;
                }
                var raw = dragValue + (author - dragBase);
                if(snap && !e.shift)raw = Snap(raw);
                SetElementPos(data.elements[selectedElement], raw);
                dirty = true;
                e.Use();
                Repaint();
                break;
            case EventType.ScrollWheel:
                zoom = Mathf.Clamp(zoom - e.delta.y * 0.1f, ZoomMin, ZoomMax);
                e.Use();
                Repaint();
                break;
        }
    }

    private static Vector2 Snap(Vector2 v)
        => new(Mathf.Round(v.x / SnapStep) * SnapStep, Mathf.Round(v.y / SnapStep) * SnapStep);

    private static Vector2 ElementPos(PanelElementData element)
        => element.kind == "grid" ? element.origin : element.pos;

    private static void SetElementPos(PanelElementData element, Vector2 value)
    {
        if(element.kind == "grid")element.origin = value;
        else element.pos = value;
    }

    private int HitTest(Vector2 author)
    {
        // Reverse order: the last painted element is the topmost.
        for(int i = data.elements.Count - 1; i >= 0; i--)
            if(HitElement(data.elements[i], author))return i;
        return -1;
    }

    private static bool HitElement(PanelElementData element, Vector2 p)
    {
        switch(element.kind)
        {
            case "slot":
            case "bar":
                return Mathf.Abs(p.x - element.pos.x) <= BlockSize * 0.5f
                    && Mathf.Abs(p.y - element.pos.y) <= BlockSize * 0.5f;
            case "grid":
                if(element.rows <= 0 || element.cols <= 0)return false;
                float minX = element.origin.x - BlockSize * 0.5f;
                float maxX = element.origin.x + (element.cols - 1) * element.pitch + BlockSize * 0.5f;
                float minY = element.origin.y - (element.rows - 1) * element.pitch - BlockSize * 0.5f;
                float maxY = element.origin.y + BlockSize * 0.5f;
                return p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;
            default:
                return false;
        }
    }

    private void DrawCanvasContent(Vector2 size)
    {
        Vector2 center = size * 0.5f;
        var panelRect = new Rect(
            center.x - data.width * zoom * 0.5f, center.y - data.height * zoom * 0.5f,
            data.width * zoom, data.height * zoom);
        EditorGUI.DrawRect(panelRect, new Color(0.26f, 0.26f, 0.29f, 1f));
        DrawOutline(panelRect, new Color(0.5f, 0.5f, 0.55f, 1f));
        DrawGrid(panelRect, center);
        DrawAxis(panelRect, center);
        for(int i = 0; i < data.elements.Count; i++)
            DrawElement(data.elements[i], i == selectedElement, center);
    }

    private void DrawGrid(Rect panelRect, Vector2 center)
    {
        float step = AuthorGrid * zoom;
        if(step < 6f)return;
        var lineColor = new Color(1f, 1f, 1f, 0.06f);
        int kMin = Mathf.FloorToInt((panelRect.xMin - center.x) / step);
        int kMax = Mathf.CeilToInt((panelRect.xMax - center.x) / step);
        for(int k = kMin; k <= kMax; k++)
        {
            float x = center.x + k * step;
            if(x < panelRect.xMin || x > panelRect.xMax - 1f)continue;
            EditorGUI.DrawRect(new Rect(x, panelRect.y, 1f, panelRect.height), lineColor);
        }
        int lMin = Mathf.FloorToInt((panelRect.yMin - center.y) / step);
        int lMax = Mathf.CeilToInt((panelRect.yMax - center.y) / step);
        for(int l = lMin; l <= lMax; l++)
        {
            float y = center.y + l * step;
            if(y < panelRect.yMin || y > panelRect.yMax - 1f)continue;
            EditorGUI.DrawRect(new Rect(panelRect.x, y, panelRect.width, 1f), lineColor);
        }
    }

    // The center cross marks author (0,0), i.e. the anchor/offset reference.
    private static void DrawAxis(Rect panelRect, Vector2 center)
    {
        var color = new Color(1f, 1f, 1f, 0.25f);
        if(center.x >= panelRect.xMin && center.x <= panelRect.xMax)
            EditorGUI.DrawRect(new Rect(center.x, panelRect.y, 1f, panelRect.height), color);
        if(center.y >= panelRect.yMin && center.y <= panelRect.yMax)
            EditorGUI.DrawRect(new Rect(panelRect.x, center.y, panelRect.width, 1f), color);
    }

    private void DrawElement(PanelElementData element, bool selected, Vector2 center)
    {
        switch(element.kind)
        {
            case "slot":
                DrawBlock(center, element.pos, element.id, SlotColor, selected);
                break;
            case "bar":
                DrawBlock(center, element.pos, element.id, BarColor, selected);
                break;
            case "grid":
                if(element.rows <= 0 || element.cols <= 0)break;
                for(int i = 0; i < element.rows * element.cols; i++)
                {
                    var pos = new Vector2(
                        element.origin.x + i % element.cols * element.pitch,
                        element.origin.y - i / element.cols * element.pitch);
                    DrawBlock(center, pos, element.idPrefix + i, GridColor, selected);
                }
                break;
        }
    }

    private void DrawBlock(Vector2 canvasCenter, Vector2 authorPos, string label, Color color, bool selected)
    {
        float size = BlockSize * zoom;
        var rect = new Rect(
            canvasCenter.x + authorPos.x * zoom - size * 0.5f,
            canvasCenter.y - authorPos.y * zoom - size * 0.5f,
            size, size);
        EditorGUI.DrawRect(rect, selected ? Brighten(color) : color);
        DrawOutline(rect, selected ? SelectionColor : Darken(color));
        if(size >= 34f)GUI.Label(rect, label ?? "", EditorStyles.centeredGreyMiniLabel);
    }

    private static void DrawOutline(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }

    private static Color Brighten(Color c)
        => new(Mathf.Min(1f, c.r + 0.25f), Mathf.Min(1f, c.g + 0.25f), Mathf.Min(1f, c.b + 0.25f), Mathf.Min(1f, c.a + 0.3f));

    private static Color Darken(Color c) => new(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);

    // --------------------------------------------------------------- attributes

    private void DrawAttributeColumn()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(230));
        attrScroll = EditorGUILayout.BeginScrollView(attrScroll);
        if(data == null)
        {
            GUILayout.Label("No layout loaded", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            // Pre-change snapshot for the undo stack: field edits are applied
            // by the controls themselves, so the "before" state must be taken
            // before they run; it is only pushed when a control reports a
            // change (the copy itself is cheap - window state is tiny).
            var snapshot = Clone(data);
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("Frame", EditorStyles.boldLabel);
            data.panel = EditorGUILayout.TextField("Panel", data.panel);
            data.width = EditorGUILayout.FloatField("Width", data.width);
            data.height = EditorGUILayout.FloatField("Height", data.height);
            data.scale = EditorGUILayout.FloatField("Scale", data.scale);
            data.anchor = EditorGUILayout.Vector2Field("Anchor", data.anchor);
            data.offset = EditorGUILayout.Vector2Field("Offset", data.offset);
            SpriteRefField("Background", ref data.background);
            GUILayout.Space(8);
            GUILayout.Label("Element", EditorStyles.boldLabel);
            if(selectedElement >= 0 && selectedElement < data.elements.Count)
                DrawElementFields(data.elements[selectedElement]);
            else
                GUILayout.Label("(select an element on the canvas)", EditorStyles.miniLabel);
            if(EditorGUI.EndChangeCheck())
            {
                PushUndoSnapshot(currentPath, snapshot);
                dirty = true;
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static void DrawElementFields(PanelElementData element)
    {
        GUILayout.Label(element.kind, EditorStyles.miniBoldLabel);
        switch(element.kind)
        {
            case "slot":
                element.id = EditorGUILayout.TextField("Id", element.id);
                element.pos = EditorGUILayout.Vector2Field("Pos", element.pos);
                SpriteRefField("Frame", ref element.frame);
                break;
            case "grid":
                element.idPrefix = EditorGUILayout.TextField("Id Prefix", element.idPrefix);
                element.rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", element.rows));
                element.cols = Mathf.Max(1, EditorGUILayout.IntField("Cols", element.cols));
                element.pitch = EditorGUILayout.FloatField("Pitch", element.pitch);
                element.origin = EditorGUILayout.Vector2Field("Origin", element.origin);
                break;
            case "bar":
                element.id = EditorGUILayout.TextField("Id", element.id);
                element.pos = EditorGUILayout.Vector2Field("Pos", element.pos);
                element.dir = (int)(ProgressBarUI.Direction)EditorGUILayout.EnumPopup(
                    "Dir", (ProgressBarUI.Direction)element.dir);
                SpriteRefField("Front", ref element.front);
                SpriteRefField("Back", ref element.back);
                break;
            default:
                GUILayout.Label($"Unknown kind '{element.kind}'", EditorStyles.miniLabel);
                break;
        }
    }

    // An empty sprite clears the whole reference (= "no sprite"/"default"),
    // so the nil state cannot be stranded in a half-filled SpriteRefData.
    private static void SpriteRefField(string label, ref SpriteRefData reference)
    {
        string current = reference != null ? reference.sprite : "";
        string edited = EditorGUILayout.TextField(label, current);
        if(edited != current)
            reference = string.IsNullOrEmpty(edited)
                ? null
                : new SpriteRefData { sprite = edited, tint = reference != null ? reference.tint : Color.white };
        if(reference != null)
            reference.tint = EditorGUILayout.ColorField(label + " Tint", reference.tint);
    }

    // ------------------------------------------------------------------ status

    private void DrawStatusBar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label($"Cursor {Mathf.RoundToInt(mouseAuthor.x)}, {Mathf.RoundToInt(mouseAuthor.y)}",
            EditorStyles.miniLabel, GUILayout.Width(150));
        string selected = data != null && selectedElement >= 0 && selectedElement < data.elements.Count
            ? ElementLabel(data.elements[selectedElement])
            : "(none)";
        GUILayout.Label("Selected: " + selected, EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if(dirty)GUILayout.Label("Unsaved changes", EditorStyles.miniLabel);
        if(!string.IsNullOrEmpty(currentPath))GUILayout.Label(currentPath, EditorStyles.miniLabel);
        GUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------------- undo

    private void HandleUndoShortcut()
    {
        var e = Event.current;
        if(e.type != EventType.KeyDown || e.keyCode != KeyCode.Z)return;
        if(!e.control && !e.command)return;
        if(EditorGUIUtility.editingTextField)return;   // a focused text field keeps its own Ctrl+Z
        if(undoStack.Count == 0)return;
        var entry = undoStack[^1];
        undoStack.RemoveAt(undoStack.Count - 1);
        data = entry.data;
        currentPath = entry.path;
        selectedElement = -1;
        dragging = false;
        dirty = true;   // the restored state is not what is on disk
        e.Use();
        Repaint();
    }

    private void PushUndo()
    {
        if(data == null)return;
        PushUndoSnapshot(currentPath, Clone(data));
    }

    private void PushUndoSnapshot(string path, PanelLayoutData snapshot)
    {
        if(snapshot == null)return;
        undoStack.Add((path, snapshot));
        if(undoStack.Count > UndoLimit)undoStack.RemoveAt(0);
    }

    // Deep copy through the project's own serializer: the DTO round-trips
    // losslessly (JsonUtility cannot express null refs - re-canonicalize).
    private static PanelLayoutData Clone(PanelLayoutData source)
    {
        var copy = JsonUtility.FromJson<PanelLayoutData>(JsonUtility.ToJson(source));
        if(copy == null)return null;
        Canonicalize(copy);
        return copy;
    }

    // -------------------------------------------------------- editing actions

    private void AddElement(string kind)
    {
        PushUndo();
        var element = new PanelElementData { kind = kind };
        switch(kind)
        {
            case "slot":
                element.id = UniqueName("slot");
                break;
            case "grid":
                element.idPrefix = UniqueName("grid");
                element.rows = 1;
                element.cols = 1;
                element.pitch = 100f;
                element.origin = Vector2.zero;
                break;
            case "bar":
                element.id = UniqueName("bar");
                element.dir = (int)ProgressBarUI.Direction.LeftToRight;
                break;
        }
        data.elements.Add(element);
        selectedElement = data.elements.Count - 1;
        dirty = true;
    }

    private void CopyElement()
    {
        if(selectedElement < 0 || selectedElement >= data.elements.Count)return;
        PushUndo();
        var copy = JsonUtility.FromJson<PanelElementData>(JsonUtility.ToJson(data.elements[selectedElement]));
        switch(copy.kind)
        {
            case "slot":
                copy.id = UniqueName(string.IsNullOrEmpty(copy.id) ? "slot" : copy.id);
                copy.pos += new Vector2(20f, -20f);
                break;
            case "grid":
                copy.idPrefix = UniqueName(string.IsNullOrEmpty(copy.idPrefix) ? "grid" : copy.idPrefix);
                copy.origin += new Vector2(20f, -20f);
                break;
            case "bar":
                copy.id = UniqueName(string.IsNullOrEmpty(copy.id) ? "bar" : copy.id);
                copy.pos += new Vector2(20f, -20f);
                break;
        }
        data.elements.Add(copy);
        selectedElement = data.elements.Count - 1;
        dirty = true;
    }

    private void DeleteElement()
    {
        if(selectedElement < 0 || selectedElement >= data.elements.Count)return;
        PushUndo();
        data.elements.RemoveAt(selectedElement);
        selectedElement = -1;
        dirty = true;
    }

    // "slot_1", "grid_1"... - the separator keeps a grid prefix from colliding
    // with its own expansion (prefix "grid" expands to grid0..gridN).
    private string UniqueName(string baseName)
    {
        var taken = new HashSet<string>();
        foreach(var element in data.elements)
        {
            if(!string.IsNullOrEmpty(element.id))taken.Add(element.id);
            if(element.kind == "grid" && !string.IsNullOrEmpty(element.idPrefix))
            {
                taken.Add(element.idPrefix);
                for(int i = 0; i < element.rows * element.cols; i++)taken.Add(element.idPrefix + i);
            }
        }
        int index = 1;
        while(taken.Contains(baseName + "_" + index))index++;
        return baseName + "_" + index;
    }

    // ---------------------------------------------------------------- warnings

    private void DrawWarningBar()
    {
        if(data == null)return;
        warnings.Clear();
        CollectWarnings(warnings);
        GUILayout.BeginVertical(EditorStyles.helpBox);
        if(warnings.Count == 0)
        {
            GUILayout.Label("OK - no layout warnings", EditorStyles.miniLabel);
        }
        else
        {
            warningScroll = EditorGUILayout.BeginScrollView(warningScroll,
                GUILayout.Height(Mathf.Min(warnings.Count, 4) * 16f + 6f));
            foreach(var warning in warnings)GUILayout.Label("- " + warning, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }
        GUILayout.EndVertical();
    }

    // Soft validation (design §4.3): report, never block the save.
    private void CollectWarnings(List<string> results)
    {
        if(data.formatVersion != PanelLayoutSerializer.FormatVersion)
            results.Add($"formatVersion {data.formatVersion} is not supported (expected {PanelLayoutSerializer.FormatVersion})");
        var seen = new HashSet<string>();
        foreach(var element in data.elements)
        {
            switch(element.kind)
            {
                case "slot":
                    if(string.IsNullOrEmpty(element.id))results.Add("a slot has an empty id");
                    else if(!seen.Add(element.id))results.Add($"duplicate slot name '{element.id}'");
                    break;
                case "grid":
                    if(string.IsNullOrEmpty(element.idPrefix))
                    {
                        results.Add("a grid has an empty id prefix");
                        break;
                    }
                    if(element.rows <= 0 || element.cols <= 0)
                    {
                        results.Add($"grid '{element.idPrefix}' has no cells");
                        break;
                    }
                    for(int i = 0; i < element.rows * element.cols; i++)
                    {
                        string name = element.idPrefix + i;
                        if(!seen.Add(name))results.Add($"duplicate slot name '{name}'");
                    }
                    break;
                case "bar":
                    if(string.IsNullOrEmpty(element.id))results.Add("a bar has an empty id");
                    break;
            }
        }
        foreach(var element in data.elements)
        {
            CheckOutside(results, element);
            switch(element.kind)
            {
                case "slot":
                    CheckSprite(results, ElementLabel(element), element.frame);
                    break;
                case "bar":
                    CheckSprite(results, ElementLabel(element) + " front", element.front);
                    CheckSprite(results, ElementLabel(element) + " back", element.back);
                    break;
            }
        }
        CheckSprite(results, "background", data.background);
    }

    private void CheckOutside(List<string> results, PanelElementData element)
    {
        float minX, maxX, minY, maxY;
        if(element.kind == "grid")
        {
            if(element.rows <= 0 || element.cols <= 0)return;
            minX = element.origin.x - BlockSize * 0.5f;
            maxX = element.origin.x + (element.cols - 1) * element.pitch + BlockSize * 0.5f;
            minY = element.origin.y - (element.rows - 1) * element.pitch - BlockSize * 0.5f;
            maxY = element.origin.y + BlockSize * 0.5f;
        }
        else
        {
            minX = element.pos.x - BlockSize * 0.5f;
            maxX = element.pos.x + BlockSize * 0.5f;
            minY = element.pos.y - BlockSize * 0.5f;
            maxY = element.pos.y + BlockSize * 0.5f;
        }
        float halfWidth = data.width * 0.5f, halfHeight = data.height * 0.5f;
        if(maxX < -halfWidth || minX > halfWidth || maxY < -halfHeight || minY > halfHeight)
            results.Add($"{ElementLabel(element)} lies fully outside the panel");
    }

    private static void CheckSprite(List<string> results, string owner, SpriteRefData reference)
    {
        if(reference == null || string.IsNullOrEmpty(reference.sprite))return;
        if(!UISprites.Exists(reference.sprite))
            results.Add($"{owner}: sprite '{reference.sprite}' not found");
    }

    // -------------------------------------------------------------- file flows

    private void RefreshAssetList()
    {
        if(!Directory.Exists(LayoutDir))
        {
            assetPaths = Array.Empty<string>();
            return;
        }
        var files = Directory.GetFiles(LayoutDir, "*.json");
        for(int i = 0; i < files.Length; i++)files[i] = files[i].Replace('\\', '/');
        Array.Sort(files, StringComparer.Ordinal);
        assetPaths = files;
    }

    private void LoadAsset(string path)
    {
        if(!ConfirmDiscard())return;
        try
        {
            var loaded = JsonUtility.FromJson<PanelLayoutData>(File.ReadAllText(path));
            if(loaded == null)
            {
                Debug.LogError("[UILayoutEditor] failed to parse " + path);
                return;
            }
            Canonicalize(loaded);
            data = loaded;
            currentPath = path;
            selectedElement = -1;
            dragging = false;
            dirty = false;
            // Undo never crosses asset loads; Import is the one path that
            // changes the asset while staying undoable (it pushes the pair).
            undoStack.Clear();
        }
        catch(Exception e)
        {
            Debug.LogError($"[UILayoutEditor] failed to load {path}: {e.Message}");
        }
    }

    private void ImportFromCode(string name)
    {
        if(!ConfirmDiscard())return;
        string path = $"{LayoutDir}/{name}.json";
        if(File.Exists(path) && !EditorUtility.DisplayDialog("Import from Code",
            $"'{name}.json' already exists.\n\nReplace the editor state with the C# static layout? The file is only overwritten when you Save.",
            "Import", "Cancel"))return;
        PanelLayout layout = null;
        foreach(var entry in CodeLayouts)
        {
            if(entry.name != name)continue;
            layout = entry.layout();
            break;
        }
        if(layout == null)return;
        // Push before replacing: undoing an Import restores the previous
        // asset and path (the entry carries both).
        PushUndo();
        var imported = PanelLayoutSerializer.ToData(layout, name);
        Canonicalize(imported);
        data = imported;
        currentPath = path;
        selectedElement = -1;
        dragging = false;
        dirty = true;
        RefreshAssetList();
        Repaint();
    }

    private void Save()
    {
        if(data == null || string.IsNullOrEmpty(currentPath))return;
        var layout = PanelLayoutSerializer.ToLayout(data);
        if(layout == null)
        {
            Debug.LogError("[UILayoutEditor] the layout state failed to convert; nothing saved");
            return;
        }
        string panelName = string.IsNullOrEmpty(data.panel) ? Path.GetFileNameWithoutExtension(currentPath) : data.panel;
        // Normalize through ToLayout->ToData (design §7.3): the written asset
        // always carries the full explicit field set, matching the L1 exporter.
        var normalized = PanelLayoutSerializer.ToData(layout, panelName);
        File.WriteAllText(currentPath, JsonUtility.ToJson(normalized, true));
        AssetDatabase.Refresh();
        Canonicalize(normalized);
        data = normalized;
        selectedElement = -1;
        dirty = false;
        Debug.Log("Saved: " + currentPath);
    }

    private bool ConfirmDiscard()
    {
        if(!dirty)return true;
        string name = string.IsNullOrEmpty(currentPath) ? "the current layout" : Path.GetFileName(currentPath);
        return EditorUtility.DisplayDialog("UI Layout Editor",
            $"Discard unsaved changes to '{name}'?", "Discard", "Cancel");
    }

    // JsonUtility cannot express a null SpriteRefData - it writes empty
    // default instances - so the editor canonicalizes empty references back
    // to null: the canvas, attribute panel and runtime all read the same
    // "no sprite" meaning.
    private static void Canonicalize(PanelLayoutData layout)
    {
        if(layout.elements == null)layout.elements = new List<PanelElementData>();
        if(layout.background != null && string.IsNullOrEmpty(layout.background.sprite))layout.background = null;
        foreach(var element in layout.elements)
        {
            if(element.frame != null && string.IsNullOrEmpty(element.frame.sprite))element.frame = null;
            if(element.front != null && string.IsNullOrEmpty(element.front.sprite))element.front = null;
            if(element.back != null && string.IsNullOrEmpty(element.back.sprite))element.back = null;
        }
    }
}
