using UnityEngine;
using UnityEngine.UI;

public class ItemIconRenderTest : MonoBehaviour
{
    public ItemIconRenderer iconRenderer;
    public ushort id;
    private void Awake()
    {
        iconRenderer.SetItem(id);
    }


}