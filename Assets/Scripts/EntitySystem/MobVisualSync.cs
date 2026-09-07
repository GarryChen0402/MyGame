using System;
using UnityEngine;

// Render shell sync + semantic animation driver for one mob (design doc:
// Mob渲染动画控制-代码设计, rule R-C2-2). Reads the entity mirror one-way
// every frame - shells never touch entity data. Position maps the main box
// pivot; rotation is a pure mapping of the heading (Euler(pitch, yaw, 0),
// PlayerRenderer precedent) so heading evolution stays in the data/behavior
// layer.
//
// Animation: the prefab's own Animator is driven from a semantic state ladder
// (dead -> attack window -> hurt stun -> moving -> idle). State existence is
// probed once at Bind (HasState); missing clips degrade gracefully instead of
// erroring - adding a clip later needs no code change. The attack and die
// clips fire exactly once each (window flags), never restarted by the ladder.
public class MobVisualSync : MonoBehaviour
{
    private EntityMirror mirror;
    private Animator animator;
    private Transform headPart;   // semantic "head" node of the rig; null = nothing to swivel

    private bool hasWalk, hasAttack, hasIdle, hasDie;
    private int walkHash, attackHash, idleHash, dieHash;

    private bool attackActive;   // true from AI session start until the attack clip finishes
    private bool dieStarted;     // death clip launched once; a corpse never animates again
    private bool speedFrozen;

    private const float WalkSpeedThreshold = 0.3f;   // m/s; below this a mob is standing
    private const string HeadPartName = "head";      // rig naming convention (zombie glTF node)
    private const float HeadSwivelRange = 75f;       // deg off the body heading; a neck-rig constraint

    private Action<InteractionSessionContextStartEvent> attackHandler;   // kept for unsubscribe by reference

    // Called by EntityRenderManager's spawn handler once the mob's data is
    // complete - the mirror was registered and snapshot at that point, so no
    // "box not ready yet" state exists (the former AABBs.Count gate is gone).
    public void Bind(EntityMirror mirror, EntityVisual visual)
    {
        this.mirror = mirror;
        if(visual != null)
        {
            animator = visual.Animator;
            if(visual.PartTransforms != null)
                visual.PartTransforms.TryGetValue(HeadPartName, out headPart);
        }
        animator ??= GetComponentInChildren<Animator>(true);
        if(animator != null)
        {
            walkHash = Animator.StringToHash("walk");
            attackHash = Animator.StringToHash("attack");
            idleHash = Animator.StringToHash("idle");
            dieHash = Animator.StringToHash("die");
            hasWalk = animator.HasState(0, walkHash);
            hasAttack = animator.HasState(0, attackHash);
            hasIdle = animator.HasState(0, idleHash);
            hasDie = animator.HasState(0, dieHash);
        }
        attackHandler = OnInteractionStart;
        EventBus.Instance.Subscribe(attackHandler);
    }

    private void OnDestroy()
    {
        if(attackHandler != null)EventBus.Instance.Unsubscribe(attackHandler);
    }

    // Attack trigger: the mob's own AI sessions publish this at SetSession
    // (IsAIControlled is set just before). Player sessions carry Operator =
    // Player and never drive a mob's clip. Plays exactly once per swing; the
    // ladder below waits for the clip to finish before falling back. Identity
    // compares on the DTO operatorId (rule R-C2-3), never a logic reference.
    private void OnInteractionStart(InteractionSessionContextStartEvent evt)
    {
        if(animator == null || !hasAttack || attackActive)return;
        if(evt.Data.operatorId != mirror?.EntityId || !evt.Data.isAIControlled)return;
        attackActive = true;
        Unfreeze();
        animator.Play(attackHash, 0, 0f);
    }

    private void Update()
    {
        if(mirror == null)return;
        // Partial-tick interpolation between tick states (design doc 固定Tick
        // 时钟与渲染插值改造-代码设计.md §4): logic steps at 20Hz, so the shell
        // lerps prev->current with GameClock.Alpha. Pure mapping - never writes
        // entity data back.
        float alpha = GameClock.Alpha;
        transform.position = Vector3.Lerp(mirror.PrevPosition, mirror.Position, alpha);
        transform.rotation = Quaternion.Euler(
            Mathf.LerpAngle(mirror.PrevPitch, mirror.Pitch, alpha),
            Mathf.LerpAngle(mirror.PrevYaw, mirror.Yaw, alpha), 0f);
        if(animator != null)UpdateAnimation();
    }

    // Head swivel override runs in LateUpdate - after the Animator evaluated
    // the skeleton this frame - so the mapped pose survives while the clips
    // own the head as soon as the lock ends (wander) or the entity dies.
    private void LateUpdate()
    {
        if(mirror == null || headPart == null)return;
        if(mirror.IsDead || !mirror.HeadLocked)return;   // dead/unlocked: the animation owns the head
        // Pure mapping of the data-layer heading, clamped to the rig's neck
        // range - a visual constraint, never a write-back. Both angles are
        // interpolated first so a 27deg/tick head turn stays smooth.
        float alpha = GameClock.Alpha;
        float bodyYaw = Mathf.LerpAngle(mirror.PrevYaw, mirror.Yaw, alpha);
        float headYaw = Mathf.LerpAngle(mirror.PrevHeadYaw, mirror.HeadYaw, alpha);
        float swivel = Mathf.Clamp(Mathf.DeltaAngle(bodyYaw, headYaw),
                                   -HeadSwivelRange, HeadSwivelRange);
        headPart.localRotation = Quaternion.Euler(0f, swivel, 0f);
    }

    // Semantic ladder, highest priority first. "Freeze" pins the current frame
    // (animator.speed = 0) and thaws when the reason ends - a corpse never
    // thaws. Every Play is guarded by the HasState probe cached at Bind.
    private void UpdateAnimation()
    {
        if(mirror.IsDead)
        {
            if(dieStarted)return;   // single-shot clip plays out in place
            dieStarted = true;
            if(!hasDie)   // no die clip: corpse freezes instantly
            {
                SetFrozen(true);
                return;
            }
            Unfreeze();   // may be pinned by the hurt stun that killed it
            animator.Play(dieHash, 0, 0f);
            return;
        }

        if(attackActive)
        {
            // Ends when the single-shot attack clip has actually finished, so
            // the ladder never replays or interrupts a swing.
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if(info.shortNameHash == attackHash && info.normalizedTime >= 1f)
                attackActive = false;
            Unfreeze();
            return;
        }

        if(mirror.InvincibleTimer > 0f)   // hurt stun: knockback slide plays out frozen
        {
            SetFrozen(true);
            return;
        }

        var m = mirror.MotionXZ;
        float hSpeed = Mathf.Sqrt(m.x * m.x + m.y * m.y);
        if(hSpeed > WalkSpeedThreshold)
        {
            Unfreeze();
            if(hasWalk && !IsCurrent(walkHash))animator.Play(walkHash, 0, 0f);
            return;
        }

        if(hasIdle)
        {
            Unfreeze();
            if(!IsCurrent(idleHash))animator.Play(idleHash, 0, 0f);
        }
        else SetFrozen(true);   // no idle clip yet: pin the last frame
    }

    private bool IsCurrent(int stateHash) => animator.GetCurrentAnimatorStateInfo(0).shortNameHash == stateHash;

    private void Unfreeze() => SetFrozen(false);

    private void SetFrozen(bool frozen)
    {
        if(speedFrozen == frozen)return;
        speedFrozen = frozen;
        animator.speed = frozen ? 0f : 1f;
    }
}
