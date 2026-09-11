using System.Collections.Generic;
using UnityEngine;

public class UIBehavior : MonoBehaviour
{
    // Assigned by UIManager when the UI is opened; a container panel's Panel
    // descriptor (S3) rides on it.
    public UIDefinition uIDefinition;
    public virtual void SetData(object data){}

    // Assembly hook (L1 of Docs/可视化UI布局编辑器-实施文档.md): called by
    // UIManager once the definition has been injected and before SetData -
    // panels whose layout rides on the definition (JSON-resolved by UIDefs at
    // registration time) build their runtime structure here, never in Awake.
    // Not called on UICache hits: such instances are already assembled.
    public virtual void OnDefinitionReady(){}

    // Open-time declaration validation (P0 of Docs/UI槽位编码与解析映射-实施文档.md,
    // C6/A4): duplicate slot codes in the layout or the packet are a hard
    // error - dictionaries swallow duplicates and a binding could land on the
    // wrong cell. A layout slot with no binding entry, or whose target the
    // packet lacks, warns and stays unbound (never a position-based
    // fallback). Channel names keep the S3 both-directions check.
    protected bool ValidatePanelData(PanelData data, PanelLayoutHandles handles)
    {
        var descriptor = uIDefinition != null ? uIDefinition.Panel : null;
        if(descriptor == null)return true;

        // The duplicate check reads the layout and packet sources, not the
        // handles dictionaries - a duplicate is swallowed there.
        var uiCodes = new List<string>();
        if(descriptor.Layout != null)
            foreach(var element in descriptor.Layout.Elements)element.CollectSlotNames(uiCodes);
        if(HasDuplicate(uiCodes) || HasDuplicate(data.SlotNames))
        {
            Debug.LogError($"[{GetType().Name}] duplicate slot code in layout/packet; bindings refused");
            handles.ClearBindings();
            return false;
        }

        foreach(var uiCode in uiCodes)
        {
            var entry = descriptor.Resolve(uiCode);
            if(entry == null)
            {
                Debug.LogWarning($"[{GetType().Name}] slot '{uiCode}' has no binding declaration; left unbound");
                continue;
            }
            if(data.SlotIndex(entry.Target) < 0)
                Debug.LogWarning($"[{GetType().Name}] slot '{uiCode}' target '{entry.Target}' is missing from the packet; left unbound");
        }

        var missing = new List<string>();
        var extra = new List<string>();
        Diff(descriptor.ChannelNames, data.ChannelNames, missing, extra);
        if(missing.Count == 0 && extra.Count == 0)return true;

        Debug.LogError($"[{GetType().Name}] channel name mismatch: missing [{string.Join(", ", missing)}] extra [{string.Join(", ", extra)}]; bindings refused");
        handles.ClearBindings();
        return false;
    }

    private static void Diff(IReadOnlyList<string> declared, IReadOnlyList<string> actual,
        List<string> missing, List<string> extra)
    {
        if(declared != null)
            foreach(var name in declared)
                if(!Contains(actual, name))missing.Add(name);
        if(actual != null)
            foreach(var name in actual)
                if(!Contains(declared, name))extra.Add(name);
    }

    private static bool Contains(IReadOnlyList<string> names, string name)
    {
        if(names == null)return false;
        for(int i = 0; i < names.Count; i++)
            if(names[i] == name)return true;
        return false;
    }

    private static bool HasDuplicate(IReadOnlyList<string> names)
    {
        var seen = new HashSet<string>();
        for(int i = 0; i < names.Count; i++)
            if(!seen.Add(names[i]))return true;
        return false;
    }

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    // Per-frame poll entry of the panel's own slots (reads mirrors; Unity
    // skips Update on inactive panels, so this may also run from a periodic
    // sweep where a panel has no Update).
    public virtual void Refresh() {}
}
