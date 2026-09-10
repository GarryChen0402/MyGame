using UnityEngine;

public class UIBehavior : MonoBehaviour
{
    // Assigned by UIManager when the UI is opened; a container panel's Panel
    // descriptor (S3) rides on it.
    public UIDefinition uIDefinition;
    public virtual void SetData(object data){}

    // Open-time name-set validation (S3 of Docs/改造提案-UI数据流拆分方案.md):
    // the registered descriptor's declared names must equal the packet's
    // names. On mismatch the differences are listed, every binding is cleared
    // and the panel binds nothing - a silent position-based mis-bind becomes
    // an explicit error.
    protected bool ValidatePanelData(PanelData data, PanelLayoutHandles handles)
    {
        var descriptor = uIDefinition != null ? uIDefinition.Panel : null;
        if(descriptor == null || descriptor.Matches(data, out string mismatch))return true;
        Debug.LogError($"[{GetType().Name}] layout/data name mismatch: {mismatch}; bindings refused");
        handles.ClearBindings();
        return false;
    }

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    // Per-frame poll entry of the panel's own slots (reads mirrors; Unity
    // skips Update on inactive panels, so this may also run from a periodic
    // sweep where a panel has no Update).
    public virtual void Refresh() {}
}
