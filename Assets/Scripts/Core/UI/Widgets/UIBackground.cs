using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIWidgetBackground : MonoBehaviour
{
    private Image bg_image = null;
    private void Awake()
    {
        //var bgGo = new GameObject("Background");
        //bgGo.transform.SetParent(transform, false);
        bg_image = gameObject.AddComponent<Image>();
        RectTransform rt = gameObject.GetOrAddComponent<RectTransform>();
        bg_image.type = Image.Type.Sliced;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        bg_image.sprite = UISprites.Resolve("minecraft:universal_bg");
    }

    public void SetSprite(Sprite sprite)
    {
        if (sprite == null) return;
        bg_image.sprite = sprite;
    }

    public void SetColor(Color color)
    {
        bg_image.color = color;
    }

    public static GameObject CreateNewBackground(Sprite sprite = null)
    {
        GameObject background = new GameObject("Background");
        background.AddComponent<UIWidgetBackground>().SetSprite(sprite);
        return background;
    }


}