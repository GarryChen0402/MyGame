using UnityEngine;
using UnityEngine.UI;

// UI slot component: renders the block model of an item into its own
// RenderTexture, displayed through a RawImage.
public class ItemIconRenderer : MonoBehaviour
{
    [SerializeField]
    private RawImage iconImage;

    [SerializeField]
    private int iconSize = 128;

    private RenderTexture rt;
    private ushort currentItemId;

    private void Awake()
    {
        EnsureRT();
    }

    private void OnDestroy()
    {
        if(rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }

    public void SetItem(ushort itemId)
    {
        currentItemId = itemId;
        EnsureRT();
        ItemIconRenderSystem.RenderItemIcon(itemId, rt);
    }

    // Re-renders the current item (e.g. after its block model changed).
    public void ForceRefresh() => ItemIconRenderSystem.RenderItemIcon(currentItemId, rt);

    private void EnsureRT()
    {
        if(rt != null)return;
        if(iconImage == null) iconImage = GetComponent<RawImage>();
        rt = new RenderTexture(iconSize, iconSize, 0, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;   // MSAA smooths block-edge aliasing; must be set before Create
        rt.Create();
        if(iconImage != null) iconImage.texture = rt;
    }
}
