using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.UI;

public class FurnaceUI : UIBehavior
{
    // private BlockEntity targetFurance = null;

    private void Awake()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.25f);
        rt.anchorMax = new Vector2(0.5f, 0.25f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920, 420);
        rt.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        rt.localPosition += new Vector3(0, 50, 0);

        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        var image = bgGo.AddComponent<Image>();
        rt = bgGo.GetOrAddComponent<RectTransform>();
        image.type = Image.Type.Sliced;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        image.sprite = Resources.Load<Sprite>("Textures/UI/universal_bg");


    }

    public override void SetData(object data)
    {
        // if (data is not BlockEntity be) return;
        // targetFurance = be;
    }

    public static UIDefinition furanceUIDefinition = new()
    {
        modId = "minecraft",
        name = "furnace",
        Kind = UIKind.SinglePanel,
        OpenWithPlayerInventory = true,
        Factory = () =>{
            var go = new GameObject("Furnace UI", typeof(FurnaceUI));
            return null;
        }
    };
}