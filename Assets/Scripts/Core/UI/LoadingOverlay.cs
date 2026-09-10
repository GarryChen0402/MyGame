using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Full-screen loading backdrop (Part B §5.1): dark tint + title / stage /
// percent texts built from the widget set. Hosted on UIManager's OverlayRoot
// (the topmost canvas child, added last) so it renders above the HUD and
// single-panel layers. No input handler is pushed and the cursor is untouched:
// while it shows, the logic underneath is frozen, so there is nothing to click
// or steer. Progress pulls from a caller-supplied source once per render frame
// (WorldSession's entry tracker during the enter-world gate; a bootstrapper
// stepper for the L1 startup later). v1 Hide() hides outright - the fade stays
// a v2 nicety.
public class LoadingOverlay : MonoBehaviour
{
    private static LoadingOverlay instance = null;
    public static LoadingOverlay Instance => instance;

    private UITextWidget titleText = null;
    private UITextWidget stageText = null;
    private UITextWidget percentText = null;
    private Func<float> progressSource = null;

    private void Awake()
    {
        if(instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Root: full-screen stretch, dark tint behind the texts.
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tint = gameObject.AddComponent<Image>();
        tint.color = new Color(0.05f, 0.06f, 0.08f, 0.8f);
        // Click-through: the failure page shows over the restored main menu,
        // so pointer events must reach the panel below the backdrop.
        tint.raycastTarget = false;

        titleText = MakeText("Txt_Title", 42f, new Color(0.95f, 0.95f, 0.95f, 1f), new Vector2(0f, 24f));
        stageText = MakeText("Txt_Stage", 18f, new Color(0.7f, 0.7f, 0.72f, 1f), new Vector2(0f, -14f));
        percentText = MakeText("Txt_Percent", 24f, new Color(0.95f, 0.95f, 0.95f, 1f), new Vector2(0f, -54f));
    }

    private UITextWidget MakeText(string name, float fontSize, Color color, Vector2 anchoredPos)
    {
        var go = UITextWidget.CreateNewText("", gameObject);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(640f, 52f);
        rt.anchoredPosition = anchoredPos;
        var widget = go.GetComponent<UITextWidget>();
        widget.SetFontSize(fontSize);
        widget.SetColor(color);
        widget.SetAlignment(TextAlignmentOptions.Center);
        return widget;
    }

    // Progress source is polled per render frame while shown (no event wiring
    // needed between the overlay and whatever readiness gate is running).
    public static void Show(string title, string stageText, Func<float> progressSource)
    {
        if(instance == null)
        {
            Debug.Log($"[LoadingOverlay] Show skipped (no host in scene): {title}");
            return;
        }
        instance.titleText.SetText(title);
        instance.stageText.SetText(stageText);
        instance.percentText.SetText("");
        instance.progressSource = progressSource;
        instance.gameObject.SetActive(true);
    }

    public static void Hide()
    {
        if(instance == null)
        {
            Debug.Log("[LoadingOverlay] Hide skipped (no host in scene)");
            return;
        }
        instance.gameObject.SetActive(false);
    }

    private void Update()
    {
        if(progressSource == null)return;
        percentText.SetText(Mathf.RoundToInt(progressSource() * 100f) + "%");
    }
}
