using System.Collections.Generic;
using UnityEngine;

// Standalone test for the entity model & animation system: builds the player
// model at a fixed world position so it can be inspected in the scene view.
// Walk runs on real time (visible while standing still); J starts an attack
// swing, blended with the walk per the fusion rules.
public class EntityVisualTest : MonoBehaviour
{
    private EntityVisual visual;
    private readonly List<(EntityAnimation anim, float t)> animationLayers = new();
    private float attackTime = -1f;   // < 0: no attack in progress

    private void Start()
    {
        if(!ResourceSystem.Instance.EntityModels.TryGetResourceWithFullName("minecraft:player", out var model))return;
        var faceTextureIds = new Dictionary<string, string>
        {
            ["top"] = "minecraft:firefly",
            ["bottom"] = "minecraft:firefly",
            ["front"] = "minecraft:firefly",
            ["back"] = "minecraft:firefly",
            ["left"] = "minecraft:firefly",
            ["right"] = "minecraft:firefly"
        };
        visual = EntityVisualBuilder.Build(model, faceTextureIds, null, null);
        visual.Root.position = new Vector3(0f, 32f, 0f);
        AddFrontIndicator();
    }

    // A cone at the model's base pointing +Z (the front face) marks the facing,
    // so the model orientation is readable in the scene view.
    private void AddFrontIndicator()
    {
        var indicator = new GameObject("FrontIndicator");
        indicator.AddComponent<MeshFilter>().sharedMesh = CreateConeMesh();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        indicator.AddComponent<MeshRenderer>().sharedMaterial = new Material(shader)
        {
            color = new Color(1f, 0.3f, 0.1f)
        };
        indicator.transform.SetParent(visual.Root);
        indicator.transform.localPosition = new Vector3(0f, 0.25f, 1.25f);
        indicator.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // cone tip +Y -> +Z
        indicator.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f);
    }

    // Unit cone with the tip on +Y (Unity has no Cone primitive).
    private static Mesh CreateConeMesh(int sides = 16)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        verts.Add(new Vector3(0f, 0.5f, 0f));    // tip
        verts.Add(Vector3.zero);                 // base center
        for(int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
        }
        for(int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            tris.Add(0); tris.Add(2 + next); tris.Add(2 + i);         // side
            tris.Add(1); tris.Add(2 + i); tris.Add(2 + next);         // base
        }
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void Update()
    {
        if(visual == null)return;
        if(Input.GetKeyDown(KeyCode.J)) attackTime = 0f;

        animationLayers.Clear();
        if(ResourceSystem.Instance.EntityAnimations.TryGetResourceWithFullName("minecraft:player_walk", out var walk))
            animationLayers.Add((walk, Time.time));
        if(attackTime >= 0f)
        {
            attackTime += Time.deltaTime;
            if(ResourceSystem.Instance.EntityAnimations.TryGetResourceWithFullName("minecraft:player_attack", out var attack))
            {
                if(attackTime <= attack.Duration)
                    animationLayers.Add((attack, attackTime));
                else
                    attackTime = -1f;   // animation finished, drop the layer
            }
        }
        EntityAnimator.Apply(visual, animationLayers);
    }
}
