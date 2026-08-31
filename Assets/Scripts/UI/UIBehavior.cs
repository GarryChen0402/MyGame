using UnityEngine;

public class UIBehavior : MonoBehaviour
{
    public UIDefinition uIDefinition;
    public virtual void SetData(object data){}

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
}