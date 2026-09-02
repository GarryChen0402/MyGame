using System.Collections.Generic;
using UnityEngine;

public class UIBehavior : MonoBehaviour
{
    public UIDefinition uIDefinition;
    public virtual void SetData(object data){}

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    public virtual void Refresh() {}

    // Panels that host container slots (furnace input/fuel/output) expose them
    // here so UIManager can shift-move backpack items into the open container.
    public virtual IReadOnlyList<ISlotAccess> ContainerSlots => null;
}
