using UnityEngine;
[RequireComponent(typeof(EntityAABBRenderer))]
public class BoxRenderTest : MonoBehaviour
{
    private EntityAABBRenderer boxRenderer = null;
    // private Player player = null;
    private void Awake()
    {
        boxRenderer = gameObject.GetComponent<EntityAABBRenderer>();
        PhysicsManager.Instance.GetAllCollisionBoxes();
    }

    private void Start()
    {
        // player = new Player();
        Player.Instance.Move(new Vector3(0, 16, 0));
    }


}