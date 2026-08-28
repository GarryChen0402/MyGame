using UnityEngine;

public class Player : Entity
{
    public float FlySpeed = 10f;

    public Player()
    {
        // Occupies a 0.6 x 1.8 x 0.6 box, centered on X/Z, standing on Y.
        CollisionBox.Add(new AABB(-0.3f, 0f, -0.3f, 0.3f, 1.8f, 0.3f));
    }

    // Flight movement, called once per frame by the driver.
    // WASD moves horizontally, E ascends, Q descends; collides against blocks
    // via PhysicsManager.
    public MoveResult FlyMove(float dt)
    {
        Vector3 input = new(
            Input.GetAxisRaw("Horizontal"),
            (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
            Input.GetAxisRaw("Vertical"));

        if (input.sqrMagnitude == 0f) return default;

        Vector3 motion = input.normalized * FlySpeed * dt;
        return PhysicsManager.Instance.MoveEntity(this, motion);
    }
}