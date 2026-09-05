using System;
using UnityEngine;

// Render shell sync + semantic animation driver for one MobEntity (design doc:
// Mob渲染动画控制-代码设计). Reads entity data one-way every frame - shells
// never write entity fields. Position maps the main box pivot; rotation is a
// pure mapping of the heading (Euler(pitch, yaw, 0), PlayerRenderer precedent)
// so heading evolution stays in the data/behavior layer.
//
// Animation: the prefab's own Animator is driven from a semantic state ladder
// (dead -> attack window -> hurt stun -> moving -> idle). State existence is
// probed once at Bind (HasState); missing clips degrade gracefully instead of
// erroring - adding a clip later needs no code change. The attack and die
// clips fire exactly once each (window flags), never restarted by the ladder.
public class MobVisualSync : MonoBehaviour
{
    private MobEntity entity;
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

    public void Bind(MobEntity entity, EntityVisual visual)
    {
        this.entity = entity;
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
    // ladder below waits for the clip to finish before falling back.
    private void OnInteractionStart(InteractionSessionContextStartEvent evt)
    {
        if(animator == null || !hasAttack || attackActive)return;
        if(evt.Operator != (Entity)entity || !evt.Ctx.IsAIControlled)return;
        attackActive = true;
        Unfreeze();
        animator.Play(attackHash, 0, 0f);
    }

    private void Update()
    {
        if(entity == null || entity.AABBs.Count == 0)return;
        transform.position = entity.MainBox.Pivot;
        // Pure mapping of entity heading - never writes entity data back.
        transform.rotation = Quaternion.Euler(entity.pitch, entity.yaw, 0f);
        if(animator != null)UpdateAnimation();
    }

    // Head swivel override runs in LateUpdate - after the Animator evaluated
    // the skeleton this frame - so the mapped pose survives while the clips
    // own the head as soon as the lock ends (wander) or the entity dies.
    private void LateUpdate()
    {
        if(entity == null || entity.AABBs.Count == 0 || headPart == null)return;
        if(entity.IsDead || !entity.HeadLocked)return;   // dead/unlocked: the animation owns the head
        // Pure mapping of the data-layer heading, clamped to the rig's neck
        // range - a visual constraint, never a write-back to entity data.
        float swivel = Mathf.Clamp(Mathf.DeltaAngle(entity.yaw, entity.HeadYaw),
                                   -HeadSwivelRange, HeadSwivelRange);
        headPart.localRotation = Quaternion.Euler(0f, swivel, 0f);
    }

    // Semantic ladder, highest priority first. "Freeze" pins the current frame
    // (animator.speed = 0) and thaws when the reason ends - a corpse never
    // thaws. Every Play is guarded by the HasState probe cached at Bind.
    private void UpdateAnimation()
    {
        if(entity.IsDead)
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

        if(entity.InvincibleTimer > 0f)   // hurt stun: knockback slide plays out frozen
        {
            SetFrozen(true);
            return;
        }

        Vector3 m = entity.Motion;
        float hSpeed = Mathf.Sqrt(m.x * m.x + m.z * m.z);
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
