using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// Entity model editor mod panel (design doc Docs/EntityModel编辑器Mod设计方案.md
// §3/§5/§7). Title + status rows above a tab widget: the "Whole" page browses
// the complete model (drag to orbit, click a face to light it up); every part
// page hosts an EditorModelPreview of that part's subtree plus a UV editor
// (FaceEditor, §5.2/§5.3): the face-direction texture with the selected
// face's rect wireframe, and X/Y/W/H pixel inputs that apply on Enter / focus
// loss after range validation - writes land in the deep-copied session model
// and the preview rebuilds with the view kept. Nothing ever touches the
// registry model.
public class EntityModelEditorUI : UIBehavior
{
    private const string DefaultModelFullName = "minecraft:player";
    private const string DefaultExportDir = "Assets/Resources/Models/entity";   // the model source dir, design doc §6.2

    private EntityModel sessionModel;
    private Dictionary<string, string> faceTextureIds;

    private void Awake()
    {
        Current = this;   // page builders run during AddTab below; they need the session
        // Panel frame. OpenWithPlayerInventory=false, so no shared backpack
        // strip below and the panel can sit dead center.
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1000, 640);
        UIWidgetBackground.CreateNewBackground().transform.SetParent(transform, false);

        var rs = ResourceSystem.Instance;
        if(!rs.EntityModels.TryGetResourceWithFullName(DefaultModelFullName, out var source))
        {
            Debug.LogError($"[EntityModelEditor] model '{DefaultModelFullName}' is not registered (EntityModels)");
            return;
        }
        if(source.SourceType != EntityModelSourceType.Json)
        {
            Debug.LogError($"[EntityModelEditor] '{DefaultModelFullName}' is a {source.SourceType} source; the cube editor edits Json cube models only");
            return;
        }
        // Editing session = deep copy; the registry model (and the player that
        // renders it) must never see preview-side mutations (design doc §4.1).
        sessionModel = EntityModelEditorSession.DeepCopy(source);
        faceTextureIds = DefaultFaceTextures();

        // Fixed top rows: model identity + a status line the pages report into.
        var titleGo = UITextWidget.CreateNewText($"Entity Model Editor - {source.FullName}", gameObject);
        Place(titleGo, new Vector2(0, 300), new Vector2(960, 30));
        var statusGo = UITextWidget.CreateNewText("", gameObject);
        Place(statusGo, new Vector2(0, 258), new Vector2(900, 24));
        var status = statusGo.GetComponent<UITextWidget>();

        // Export row (design doc §6.2): directory input + export button with
        // confirm-overwrite gating; messages share the status line above.
        var dirGo = UITextInputWidget.CreateNewTextInput(DefaultExportDir, null, gameObject);
        Place(dirGo, new Vector2(-120, 213), new Vector2(340, 30));
        var dirInput = dirGo.GetComponent<UITextInputWidget>();

        var exportGo = UIButtonWidget.CreateNewButton("Export JSON", null, gameObject);
        Place(exportGo, new Vector2(155, 213), new Vector2(150, 30));
        var export = exportGo.GetComponent<UIButtonWidget>();
        string pendingOverwrite = null;   // path the Confirm overwrite click is armed for
        export.OnClick.AddListener(() =>
        {
            dirInput.SetInvalid(false);
            string dir = dirInput.GetText().Trim();
            if(dir.Length == 0)
            {
                dirInput.SetInvalid(true);
                status.SetText("enter an export directory first");
                return;
            }
            if(!Application.isEditor && dir.Contains("Assets"))
            {
                dirInput.SetInvalid(true);
                status.SetText("packaged builds cannot write into Assets - use an absolute path");
                return;
            }
            string path = $"{dir}/{sessionModel.name}.json";
            // A directory edit since the last click disarms the pending overwrite.
            if(pendingOverwrite != null && pendingOverwrite != path)
            {
                pendingOverwrite = null;
                export.SetText("Export JSON");
            }
            if(pendingOverwrite == null && File.Exists(path))
            {
                pendingOverwrite = path;
                export.SetText("Confirm overwrite");
                status.SetText($"{path} exists - click again to overwrite");
                return;
            }
            try
            {
                File.WriteAllText(path, EntityModelSerializer.ToJson(sessionModel));
                pendingOverwrite = null;
                export.SetText("Export JSON");
                status.SetText($"exported: {path}");
            }
            catch(System.Exception e)
            {
                pendingOverwrite = null;
                export.SetText("Export JSON");
                dirInput.SetInvalid(true);
                status.SetText($"export failed: {e.Message}");
            }
        });

        var tabGo = UITabWidget.CreateNewTab(gameObject);
        tabGo.transform.localPosition = new Vector3(0, -70, 0);
        ((RectTransform)tabGo.transform).sizeDelta = new Vector2(1000, 500);
        var tab = tabGo.GetComponent<UITabWidget>();
        tab.AddTab("whole", "Whole", () => BuildWholePage(sessionModel, faceTextureIds));
        foreach(string part in OrderedParts(sessionModel))
        {
            string id = part;   // capture: the loop variable would otherwise close over
            tab.AddTab(id, id, () => BuildPartPage(id, sessionModel, faceTextureIds, status));
        }
    }

    private void OnDestroy()
    {
        if(Current == this)Current = null;
    }

    // ---- page 1: whole-model preview (no edit area, decision E) ----

    private static GameObject BuildWholePage(EntityModel model, Dictionary<string, string> faceTextures)
    {
        var page = NewPage();
        var title = UITextWidget.CreateNewText("Whole model - drag to orbit / click to pick", page);
        Place(title, new Vector2(0, 208), new Vector2(700, 24));

        var previewGo = new GameObject("Whole Preview", typeof(RectTransform));
        previewGo.transform.SetParent(page.transform, false);
        Place(previewGo, new Vector2(0, -20), new Vector2(360, 360));
        var preview = previewGo.AddComponent<EditorModelPreview>();
        preview.Setup(model, faceTextures);

        var pickedGo = UITextWidget.CreateNewText("picked: (none)", page);
        Place(pickedGo, new Vector2(0, -218), new Vector2(700, 20));
        var picked = pickedGo.GetComponent<UITextWidget>();
        preview.OnFacePicked.AddListener((cube, face) =>
        {
            if(cube == null)preview.ClearHighlight();
            else preview.SelectFace(cube, face);
            picked.SetText(cube == null ? "picked: (none)" : $"picked: {cube} / {face}");
            Debug.Log($"[EntityModelEditor] picked {cube} / {face}");
        });
        return page;
    }

    // ---- part pages (tab 2+): subtree preview + UV editor (design doc §5) ----

    private static GameObject BuildPartPage(string partId, EntityModel model,
                                            Dictionary<string, string> faceTextures, UITextWidget status)
    {
        var page = NewPage();

        var title = UITextWidget.CreateNewText("subtree - drag to orbit / click to pick a face", page);
        Place(title, new Vector2(-250, 208), new Vector2(420, 24));

        var previewGo = new GameObject($"{partId} Preview", typeof(RectTransform));
        previewGo.transform.SetParent(page.transform, false);
        Place(previewGo, new Vector2(-250, -12), new Vector2(360, 360));
        var preview = previewGo.AddComponent<EditorModelPreview>();
        preview.Setup(model, faceTextures, partId);

        // UV editor column; the page's pick events flow through it.
        var editor = new FaceEditor(page, model, faceTextures, preview, status);

        // Default selection: front when the part has one, else the first face
        // in the canonical bake order - a freshly opened page shows editable
        // data right away (§5.3).
        string defaultFace = null;
        if(model.Cubes.TryGetValue(partId, out var cube))
        {
            if(cube.Faces.ContainsKey("front"))defaultFace = "front";
            else foreach(string key in EditorModelPreview.FaceOrder)
                    if(cube.Faces.ContainsKey(key)){ defaultFace = key; break; }
        }
        if(defaultFace != null)editor.Select(partId, defaultFace);
        else status.SetText($"{partId}: no faces to edit");
        return page;
    }

    // ---- helpers ----

    // Parts in stable DFS order (roots first, then hierarchy children), only
    // nodes that actually carry a cube - organizer-only group nodes get no
    // page. Falls back to insertion order when a node is not a hierarchy key.
    private static List<string> OrderedParts(EntityModel model)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        void Walk(string id)
        {
            if(!visited.Add(id))return;
            if(model.Cubes.ContainsKey(id))result.Add(id);
            if(!model.Hierarchy.TryGetValue(id, out var children))return;
            foreach(string child in children)Walk(child);
        }
        foreach(string root in model.Roots)Walk(root);
        return result;
    }

    private static Dictionary<string, string> DefaultFaceTextures() => new()
    {
        ["top"] = "minecraft:firefly",
        ["bottom"] = "minecraft:firefly",
        ["front"] = "minecraft:firefly",
        ["back"] = "minecraft:firefly",
        ["left"] = "minecraft:firefly",
        ["right"] = "minecraft:firefly"
    };

    private static GameObject NewPage()
    {
        var page = new GameObject("Page", typeof(RectTransform));
        var prt = (RectTransform)page.transform;
        prt.anchorMin = Vector2.zero;   // stretch over the tab widget's content area
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        return page;
    }

    private static void Place(GameObject go, Vector2 position, Vector2 size)
    {
        go.transform.localPosition = position;
        ((RectTransform)go.transform).sizeDelta = size;
    }

    // The session lives in the panel instance; the definition factory creates
    // one panel at a time. Null when Awake bailed (model missing).
    public static EntityModelEditorUI Current { get; private set; }
    public EntityModel SessionModel => sessionModel;
    public Dictionary<string, string> FaceTextures => faceTextureIds;

    // The UV editor of one part page (design doc §5.2/§5.3): the texture of
    // the selected face's direction is shown with the uvRect as a wireframe
    // overlay; X/Y/W/H pixel inputs (baseline = that texture's pixel size)
    // live-update the overlay on every keystroke and apply on Enter / focus
    // loss, after range validation paints the offending frame red. Commit
    // writes the expanded UVs back into the session cube face and rebuilds
    // the preview, keeping the selection lit.
    private sealed class FaceEditor
    {
        private const float ColX = 240f;         // right column center
        private const float TexDispH = 190f;     // texture display height in px
        private const float MaxDispW = 330f;

        private readonly EntityModel model;
        private readonly Dictionary<string, string> faceTextures;
        private readonly EditorModelPreview preview;
        private readonly UITextWidget status;
        private readonly UITextWidget texLabel;
        private readonly RectTransform textureRt;
        private readonly RawImage textureImage;
        private readonly RectTransform overlay;
        private readonly UITextWidget selText;
        private readonly UIIntegerInputWidget xIn, yIn, wIn, hIn;

        private string cubeId, faceKey;
        private Texture2D tex;
        private int texW, texH;

        public FaceEditor(GameObject page, EntityModel model,
                          Dictionary<string, string> faceTextures,
                          EditorModelPreview preview, UITextWidget status)
        {
            this.model = model;
            this.faceTextures = faceTextures;
            this.preview = preview;
            this.status = status;

            texLabel = Text(page, "texture: -", new Vector2(ColX, 190), new Vector2(380, 24));

            textureRt = NewRect(page, "Texture", new Vector2(ColX, 55), new Vector2(100, TexDispH));
            textureImage = textureRt.gameObject.AddComponent<RawImage>();
            textureImage.raycastTarget = false;
            overlay = BuildOverlay(textureRt);

            selText = Text(page, "selected: (none)", new Vector2(ColX, -75), new Vector2(380, 22));
            var inputs = BuildInputRow(page);
            xIn = inputs[0]; yIn = inputs[1]; wIn = inputs[2]; hIn = inputs[3];
            foreach(var input in inputs)
            {
                input.OnValueChanged.AddListener(_ => OnAnyInput());
                input.OnSubmit.AddListener(_ => TryCommit());
            }
            Text(page, "Enter / blur applies - out-of-range values are rejected", new Vector2(ColX, -195), new Vector2(400, 20));

            preview.OnFacePicked.AddListener(OnFacePicked);
        }

        // ---- selection ----

        public void Select(string cube, string face)
        {
            cubeId = cube;
            faceKey = face;
            preview.SelectFace(cube, face);
            selText.SetText($"selected: {cube} / {face}");

            // The pixel baseline is the pixel size of the texture this face
            // direction maps to (each direction may sit on a different sheet).
            if(!faceTextures.TryGetValue(face, out string fullName) ||
               !ResourceSystem.Instance.Textures.TryGetResourceWithFullName(fullName, out var res))
            {
                status.SetText($"no texture mapped for face '{face}'");
                return;
            }
            tex = res.Atlas;
            texW = tex.width;
            texH = tex.height;
            texLabel.SetText($"{res.FullName} {texW}x{texH}");
            textureImage.texture = tex;
            float ratio = texH > 0 ? texW / (float)texH : 1f;
            textureRt.sizeDelta = new Vector2(Mathf.Min(MaxDispW, Mathf.Max(40f, TexDispH * ratio)), TexDispH);

            // Seed the inputs from the face's current pixel rect.
            var fd = model.Cubes[cube].Faces[face];
            var rect = EntityModelEditorSession.UvRectToPixels(
                EntityModelEditorSession.UvRectFromFaceUvs(fd.uv), texW, texH);
            xIn.SetValue(rect.x);
            yIn.SetValue(rect.y);
            wIn.SetValue(rect.width);
            hIn.SetValue(rect.height);
            ClearValidation();
            ShowRect(rect);
        }

        public void Clear()
        {
            cubeId = null;
            faceKey = null;
            preview.ClearHighlight();
            selText.SetText("selected: (none)");
            SetOverlayVisible(false);
        }

        private void OnFacePicked(string cube, string face)
        {
            if(cube == null || face == null){ Clear(); return; }
            Debug.Log($"[EntityModelEditor] picked {cube} / {face}");
            Select(cube, face);
        }

        // ---- live input -> overlay ----

        private void OnAnyInput()
        {
            ClearValidation();   // editing again lifts the red frames
            if(cubeId == null || faceKey == null || tex == null)return;
            ShowRect(CurrentRect());
        }

        private RectInt CurrentRect() => new(xIn.GetValue(), yIn.GetValue(), wIn.GetValue(), hIn.GetValue());

        private void ShowRect(RectInt rect)
        {
            if(rect.width <= 0 || rect.height <= 0)
            {
                SetOverlayVisible(false);
                return;
            }
            SetOverlayVisible(true);
            var uv = EntityModelEditorSession.PixelsToUvRect(rect, texW, texH);
            // A code-created RectTransform keeps its engine-default offsets
            // (each side inset -50 = a leftover 100x100 sizeDelta) across anchor
            // assignments, so the box must zero them after re-anchoring or it
            // drifts past the uvRect span it should cover.
            overlay.anchorMin = new Vector2(uv.x, uv.y);
            overlay.anchorMax = new Vector2(uv.x + uv.z, uv.y + uv.w);
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
        }

        // ---- commit (Enter / focus loss, decision D) ----

        private void TryCommit()
        {
            if(cubeId == null || faceKey == null || tex == null)
            {
                status.SetText("select a face in the preview first");
                return;
            }
            int x = xIn.GetValue(), y = yIn.GetValue(), w = wIn.GetValue(), h = hIn.GetValue();
            // Range contract of §5.3: x in [0, texW-w], y in [0, texH-h],
            // w/h in (0, remaining]. Each input is flagged when it is the one
            // pushing the rect out of the texture.
            bool errX = x < 0 || x > texW || (w > 0 && x + w > texW);
            bool errY = y < 0 || y > texH || (h > 0 && y + h > texH);
            bool errW = w <= 0 || x < 0 || x + w > texW;
            bool errH = h <= 0 || y < 0 || y + h > texH;
            xIn.SetInvalid(errX);
            yIn.SetInvalid(errY);
            wIn.SetInvalid(errW);
            hIn.SetInvalid(errH);
            if(errX || errY || errW || errH)
            {
                status.SetText($"out of range: keep X,Y,W,H inside {texW}x{texH}");
                return;
            }
            ClearValidation();   // a successful commit lifts any earlier red frames

            // Write back the expanded UV list (ModelFaceData is a struct of
            // list references, so the whole struct must be re-assigned).
            var faces = model.Cubes[cubeId].Faces;
            var fd = faces[faceKey];
            fd.uv = EntityModelParser.ExpandFaceUv(faceKey,
                EntityModelEditorSession.PixelsToUvRect(new RectInt(x, y, w, h), texW, texH));
            faces[faceKey] = fd;

            status.SetText($"applied: {cubeId}/{faceKey} rect ({x},{y}) {w}x{h}");
            preview.Rebuild();
            preview.SelectFace(cubeId, faceKey);
            ShowRect(CurrentRect());   // explicit sync in case the last keystroke never fired the change event
        }

        private void ClearValidation()
        {
            xIn.SetInvalid(false);
            yIn.SetInvalid(false);
            wIn.SetInvalid(false);
            hIn.SetInvalid(false);
        }

        // ---- small builders ----

        private static RectTransform NewRect(GameObject parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            Place(go, position, size);
            return (RectTransform)go.transform;
        }

        private static UITextWidget Text(GameObject parent, string content, Vector2 position, Vector2 size)
        {
            var go = UITextWidget.CreateNewText(content, parent);
            Place(go, position, size);
            return go.GetComponent<UITextWidget>();
        }

        private static UIIntegerInputWidget Input(GameObject parent, float x, float y)
        {
            var go = UIIntegerInputWidget.CreateNewIntegerInput(0, parent);
            Place(go, new Vector2(x, y), new Vector2(62, 30));
            return go.GetComponent<UIIntegerInputWidget>();
        }

        // "X  [ ] Y  [ ] W  [ ] H  [ ]" - one labelled field per uvRect axis.
        private static UIIntegerInputWidget[] BuildInputRow(GameObject page)
        {
            string[] keys = { "X", "Y", "W", "H" };
            var result = new UIIntegerInputWidget[keys.Length];
            float x = 55f;
            for(int i = 0; i < keys.Length; i++)
            {
                Text(page, keys[i], new Vector2(x + 10f, -135f), new Vector2(20, 28));
                result[i] = Input(page, x + 55f, -135f);
                x += 100f;
            }
            return result;
        }

        // Wireframe over the texture image: a tinted translucent fill plus
        // four 2px bars, all anchored by uvRect fractions of the image rect
        // (v grows upward both in UV space and in UI space).
        private static RectTransform BuildOverlay(RectTransform parent)
        {
            var go = new GameObject("UV Overlay", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            var fill = go.AddComponent<Image>();
            fill.raycastTarget = false;
            fill.color = new Color(1f, 1f, 1f, 0.18f);
            AddBar(rt, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 2));      // bottom
            AddBar(rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), new Vector2(0, 0));      // top
            AddBar(rt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(2, 0));      // left
            AddBar(rt, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-2, 0), new Vector2(0, 0));     // right
            go.SetActive(false);
            return rt;
        }

        private static void AddBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
                                   Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Bar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.96f, 0.72f, 1f);
        }

        private void SetOverlayVisible(bool visible) => overlay.gameObject.SetActive(visible);
    }

    public static UIDefinition editorUIDefinition = new()
    {
        modId = "entity_model_editor",
        name = "entity_model_editor",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:ui_input_handler",
        OpenWithPlayerInventory = false,
        Factory = () =>
        {
            var go = new GameObject("Entity Model Editor UI", typeof(EntityModelEditorUI));
            return go;
        }
    };
}
