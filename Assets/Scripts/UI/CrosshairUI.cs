using UnityEngine;
using UnityEngine.UI;

// Vanilla-MC style crosshair: a 15x15 pixel texture (1px black cross with a
// white outline and a transparent 3x3 center gap) rendered at screen center.
// Attach to the Canvas GameObject.
public class CrosshairUI : MonoBehaviour
{
    private static CrosshairUI instance;
    public static CrosshairUI Instance => instance;

    private void Awake()
    {
        if(instance != null) { Destroy(gameObject); return; }
        instance = this;

        var go = new GameObject("Crosshair");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // screen center
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(30, 30);   // 15px texture doubled for visibility

        var img = go.AddComponent<Image>();
        img.sprite = Sprite.Create(BuildCrosshairTexture(), new Rect(0, 0, 15, 15), new Vector2(0.5f, 0.5f), 15f);
    }

    // 15x15: black cross (3px wide incl. white outline) with a transparent 3x3
    // center so the player can see what the crosshair points at.
    private static Texture2D BuildCrosshairTexture()
    {
        const int s = 15;
        const int c = 7;   // center pixel
        var pixels = new Color32[s * s];
        for(int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
        void Set(int x, int y, Color32 col)
        {
            if(x < 0 || x >= s || y < 0 || y >= s) return;
            pixels[y * s + x] = col;
        }
        Color32 white = new(255, 255, 255, 255);
        Color32 black = new(0, 0, 0, 255);
        for(int i = 0; i < s; i++)
        {
            if(i < c - 1 || i > c + 1)   // skip the center gap
            {
                Set(i, c - 1, white); Set(i, c, black); Set(i, c + 1, white);   // horizontal arm
                Set(c - 1, i, white); Set(c, i, black); Set(c + 1, i, white);   // vertical arm
            }
        }
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
