using UnityEngine;

public class UIBehavior : MonoBehaviour
{
    public UIDefinition uIDefinition;
    public virtual void SetData(object data){}

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    // Per-frame poll entry of the panel's own slots (reads mirrors; Unity
    // skips Update on inactive panels, so this may also run from a periodic
    // sweep where a panel has no Update).
    public virtual void Refresh() {}
}
