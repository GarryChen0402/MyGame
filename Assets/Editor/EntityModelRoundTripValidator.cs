using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// P1 acceptance harness for the model editor pipeline (design doc §9 P1):
// serializer round trip on the stock player model (export -> re-import ->
// semantic equality), deep-copy isolation from the registry model, the pixel-
// edit round trip (pixels -> uvRect -> canonical expansion onto the baked UVs
// -> export -> import), and 1px-grid quantization. Pure data checks - no play
// mode needed: menu item Tools/Entity Model Editor/Round-trip Verify (P1).
public static class EntityModelRoundTripValidator
{
    private const string ModelResource = "Models/entity/player";   // Resources/Models/entity/player.json
    private const int TestTexW = 64, TestTexH = 64;                 // pixel grid of the test sheet
    private const float Epsilon = 1e-4f;

    private static int failures;

    [MenuItem("Tools/Entity Model Editor/Round-trip Verify (P1)")]
    public static void Verify()
    {
        failures = 0;

        var source = Resources.Load<TextAsset>(ModelResource);
        if(source == null)
        {
            Debug.LogError($"[EntityModelRoundTrip] cannot load {ModelResource}");
            return;
        }
        var original = EntityModelParser.Parse(source.text);
        if(!original.Cubes.TryGetValue("head", out var head) || !head.Faces.ContainsKey("front") ||
           !original.Cubes.TryGetValue("body", out var body) || !body.Faces.ContainsKey("front"))
        {
            Debug.LogError("[EntityModelRoundTrip] stock model no longer has head/front & body/front; adjust the probe cubes");
            return;
        }
        Debug.Log($"[EntityModelRoundTrip] source: {original.FullName}, {original.Cubes.Count} cubes, " +
                  $"{original.Roots.Count} roots, {original.Hierarchy.Count} hierarchy nodes");

        // 1) Serializer round trip: export -> re-import -> semantic equality.
        string jsonOut = EntityModelSerializer.ToJson(original);
        string dumpPath = Path.Combine(Path.GetTempPath(), "entity_model_roundtrip_export.json");
        File.WriteAllText(dumpPath, jsonOut);
        Debug.Log($"[EntityModelRoundTrip] exported json ({jsonOut.Length} chars) written to {dumpPath} for visual check");
        var reimported = EntityModelParser.Parse(jsonOut);
        CompareModels(original, reimported, "export/re-import");

        // 2) Deep-copy isolation: mutating the session copy must not touch the
        //    model the registry (and the player) still holds. Mutating a list
        //    element goes through the shared list reference of the struct copy.
        var copy = EntityModelEditorSession.DeepCopy(original);
        var headFront = copy.Cubes["head"].Faces["front"];
        var registryUv = original.Cubes["head"].Faces["front"].uv[0];
        headFront.uv[0] = new Vector2(0.9f, 0.1f);
        Expect(registryUv == original.Cubes["head"].Faces["front"].uv[0],
               "deep copy: registry head/front uv untouched by a session edit");
        Expect(original.Cubes["head"].Faces["front"].uv[0] != headFront.uv[0],
               "deep copy: session head/front uv actually changed");

        // 3) Pixel-edit round trip: the exact path the editor UI will drive.
        //    ModelFaceData is a struct of list references, so the replaced uv
        //    list must be written back into the Faces dictionary.
        var rect = new RectInt(8, 0, 32, 32);   // 8px in from the left, lower half of the sheet
        var face = copy.Cubes["head"].Faces["front"];
        face.uv = EntityModelParser.ExpandFaceUv("front",
            EntityModelEditorSession.PixelsToUvRect(rect, TestTexW, TestTexH));
        copy.Cubes["head"].Faces["front"] = face;

        var edited = EntityModelParser.Parse(EntityModelSerializer.ToJson(copy));
        var backRect = EntityModelEditorSession.UvRectToPixels(
            EntityModelEditorSession.UvRectFromFaceUvs(edited.Cubes["head"].Faces["front"].uv),
            TestTexW, TestTexH);
        Expect(backRect.Equals(rect), $"pixel-edit round trip: expected {rect}, got {backRect}");

        // Only head/front moved: a sibling face of the edited model still equals
        // the registry value.
        var bodyFrontA = EntityModelEditorSession.UvRectFromFaceUvs(body.Faces["front"].uv);
        var bodyFrontB = EntityModelEditorSession.UvRectFromFaceUvs(edited.Cubes["body"].Faces["front"].uv);
        Expect(Approx(bodyFrontA, bodyFrontB), $"unrelated face drifted: body/front {bodyFrontA} vs {bodyFrontB}");

        // 4) 1px-grid quantization: an off-grid hand-written uvRect snaps onto
        //    the pixel grid and stays there; grid values pass through unchanged.
        var snapped = EntityModelEditorSession.QuantizeToPixelGrid(new Vector4(0.1f, 0.2f, 0.35f, 0.45f), 96, 64);
        var expected = new Vector4(10f / 96f, 13f / 64f, 34f / 96f, 29f / 64f);
        Expect(Approx(snapped, expected), $"quantize: expected {expected}, got {snapped}");
        var reSnapped = EntityModelEditorSession.QuantizeToPixelGrid(snapped, 96, 64);
        Expect(Approx(reSnapped, snapped), $"quantize: not idempotent ({reSnapped})");
        var aligned = EntityModelEditorSession.PixelsToUvRect(new RectInt(8, 0, 32, 32), 64, 64);
        Expect(Approx(EntityModelEditorSession.QuantizeToPixelGrid(aligned, 64, 64), aligned),
               "quantize: grid-aligned value changed");

        // 5) Subtree builder (design doc decision A): BuildFrom("waist") covers
        //    the torso group and its children only (no legs); a leaf part
        //    builds a single node. Edit-mode scene objects, destroyed right away.
        VerifySubtreeBuild(original, "waist", new[] { "waist", "head", "body", "right_arm", "left_arm" });
        VerifySubtreeBuild(original, "right_leg", new[] { "right_leg" });

        if(failures == 0)
            Debug.Log($"[EntityModelRoundTrip] P1 PASS: serializer round trip, deep-copy isolation, " +
                      $"pixel-edit closure and uvRect quantization verified on {original.FullName}");
        else
            Debug.LogError($"[EntityModelRoundTrip] P1 FAILED: {failures} check(s) failed (see errors above)");
    }

    private static void CompareModels(EntityModel a, EntityModel b, string label)
    {
        Expect(a.modId == b.modId && a.name == b.name, $"{label}: modId/name");
        Expect(SameStrings(a.Roots, b.Roots), $"{label}: roots [{Join(a.Roots)}] vs [{Join(b.Roots)}]");
        Expect(a.Cubes.Count == b.Cubes.Count, $"{label}: cube count {a.Cubes.Count} vs {b.Cubes.Count}");
        foreach(var kv in a.Cubes)
        {
            if(!b.Cubes.TryGetValue(kv.Key, out var other))
            {
                Expect(false, $"{label}: cube '{kv.Key}' missing after round trip");
                continue;
            }
            var c = kv.Value;
            Expect(Approx(c.Size, other.Size) && Approx(c.Pivot, other.Pivot) && Approx(c.Position, other.Position),
                   $"{label}: cube '{kv.Key}' geometry {c.Size}/{c.Pivot}/{c.Position} vs " +
                   $"{other.Size}/{other.Pivot}/{other.Position}");
            Expect(c.Faces.Count == other.Faces.Count,
                   $"{label}: cube '{kv.Key}' face count {c.Faces.Count} vs {other.Faces.Count}");
            foreach(var fk in c.Faces.Keys)
            {
                if(!other.Faces.TryGetValue(fk, out var of))
                {
                    Expect(false, $"{label}: cube '{kv.Key}' face '{fk}' missing after round trip");
                    continue;
                }
                var faceA = c.Faces[fk];
                var faceB = of;
                var rectA = EntityModelEditorSession.UvRectFromFaceUvs(faceA.uv);
                var rectB = EntityModelEditorSession.UvRectFromFaceUvs(faceB.uv);
                Expect(Approx(rectA, rectB), $"{label}: cube '{kv.Key}' face '{fk}' uvRect {rectA} vs {rectB}");
                Expect(faceA.colors.Count == faceB.colors.Count && Approx(faceA.colors[0], faceB.colors[0]),
                       $"{label}: cube '{kv.Key}' face '{fk}' color mismatch");
                Expect(faceA.canBeOccluded == faceB.canBeOccluded,
                       $"{label}: cube '{kv.Key}' face '{fk}' canBeOccluded mismatch");
            }
        }
        Expect(a.Hierarchy.Count == b.Hierarchy.Count,
               $"{label}: hierarchy count {a.Hierarchy.Count} vs {b.Hierarchy.Count}");
        foreach(var kv in a.Hierarchy)
        {
            if(!b.Hierarchy.TryGetValue(kv.Key, out var other))
            {
                Expect(false, $"{label}: hierarchy node '{kv.Key}' missing after round trip");
                continue;
            }
            Expect(SameStrings(kv.Value, other), $"{label}: hierarchy '{kv.Key}' children [{Join(kv.Value)}] vs [{Join(other)}]");
        }
    }

    private static bool SameStrings(List<string> a, List<string> b)
    {
        if(a == null || b == null || a.Count != b.Count)return false;
        for(int i = 0; i < a.Count; i++)
            if(a[i] != b[i])return false;
        return true;
    }

    private static string Join(List<string> list)
        => list == null ? "" : string.Join(",", list);

    private static bool Approx(Vector3 a, Vector3 b) => Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z);
    private static bool Approx(Vector4 a, Vector4 b) => Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z) && Near(a.w, b.w);
    private static bool Approx(Color a, Color b) => Near(a.r, b.r) && Near(a.g, b.g) && Near(a.b, b.b) && Near(a.a, b.a);
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < Epsilon;

    private static void VerifySubtreeBuild(EntityModel model, string rootId, string[] expectedParts)
    {
        // Pass an explicit material: ResourceSystem.Instance may not exist in
        // edit mode (Builder would otherwise fall back to BlockMaterial).
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture"));
        var built = EntityVisualBuilder.BuildFrom(model, rootId, null, material, null);
        bool ok = built.PartTransforms.Count == expectedParts.Length;
        foreach(var part in expectedParts)
            ok &= built.PartTransforms.ContainsKey(part);
        Expect(ok, $"BuildFrom('{rootId}'): expected [{string.Join(",", expectedParts)}], " +
                   $"built [{string.Join(",", built.PartTransforms.Keys)}]");
        Object.DestroyImmediate(built.Root.gameObject);
        Object.DestroyImmediate(material);
    }

    private static void Expect(bool condition, string label)
    {
        if(!condition)
        {
            failures++;
            Debug.LogError($"[EntityModelRoundTrip] FAIL: {label}");
        }
    }
}
