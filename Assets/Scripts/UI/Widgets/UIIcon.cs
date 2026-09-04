using UnityEngine.UI;
using UnityEngine;

public class UIIconWidget : MonoBehaviour
{
    private Image icon = null;
    private void Awake()
    {
        //var bgGo = new GameObject("Background");
        //bgGo.transform.SetParent(transform, false);
        icon = gameObject.AddComponent<Image>();
        // GO comes with a RectTransform from the factory (Awake-time transform
        // replacement is deferred by Unity, so never AddComponent one here).
        RectTransform rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    public void SetSprite(Sprite sprite)
    {
        if (sprite == null) return;
        icon.sprite = sprite;
    }

    public static GameObject CreateNewIcon(Sprite sprite = null, GameObject parent = null)
    {
        GameObject icongo = new GameObject("Icon", typeof(RectTransform));
        icongo.AddComponent<UIIconWidget>().SetSprite(sprite);
        if(parent!=null)icongo.transform.SetParent(parent.transform, false);
        return icongo;
    }
}