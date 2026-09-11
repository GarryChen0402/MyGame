using System.IO;
using UnityEditor;
using UnityEngine;

// Export command (L1 item 4 of Docs/可视化UI布局编辑器-实施文档.md): writes the
// C# static layouts into Assets/Resources/UILayouts/*.json - the assets
// PanelLayoutAssets loads (and the future editor edits). Re-running is
// idempotent: ToData writes deterministically and JsonUtility pretty-prints
// the same way. Same file-writing shape as BlockModelEditorWindow.Export.
public static class UILayoutExportCommands
{
    [MenuItem("Tools/UI Layout/Export Static Layouts")]
    private static void ExportStaticLayouts()
    {
        string dir = "Assets/Resources/UILayouts";
        if(!Directory.Exists(dir))Directory.CreateDirectory(dir);
        Export(dir, "furnace", FurnaceUI.Layout);
        Export(dir, "crafting_table", CraftingTableUI.Layout);
        AssetDatabase.Refresh();
    }

    private static void Export(string dir, string panel, PanelLayout layout)
    {
        string path = Path.Combine(dir, panel + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(PanelLayoutSerializer.ToData(layout, panel), true));
        Debug.Log("Exported: " + path);
    }
}
