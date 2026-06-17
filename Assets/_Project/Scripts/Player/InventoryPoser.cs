using System.Collections.Generic;
using UnityEngine;

// While the inventory is open, poses the player model with a weapon-appropriate
// idle:
//   • two-handed weapon in Main Hand → twoHandedIdlePose
//   • everything else (one-handed, thrown, shield, unarmed) → oneHandedIdlePose
//
// Rather than driving the rig directly (which breaks humanoid retargeting), it
// swaps in an AnimatorOverrideController that replaces the controller's clips
// with the chosen idle — so the Animator's own (working) retargeting plays it.
// The Animator is put on unscaled time so it animates while the game is paused,
// and everything is restored when the inventory closes.
[DisallowMultipleComponent]
public class InventoryPoser : MonoBehaviour
{
    public AnimationClip oneHandedIdlePose;
    public AnimationClip twoHandedIdlePose;

    Animator                  _animator;
    AnimatorOverrideController _override;
    RuntimeAnimatorController  _savedController;
    AnimationClip             _current;
    bool                      _posing;

    readonly List<KeyValuePair<AnimationClip, AnimationClip>> _overrideList = new();

    void LateUpdate()
    {
        AnimationClip want = GameUI.IsOpen ? SelectClip() : null;
        if (want != _current)
        {
            if (want != null) PlayPose(want);
            else              StopPose();
            _current = want;
        }

        // The game is paused (timeScale 0) while the inventory is open, so the
        // Animator won't tick itself — advance it manually on unscaled time.
        if (_posing && _animator != null && _animator.isActiveAndEnabled)
            _animator.Update(Time.unscaledDeltaTime);
    }

    AnimationClip SelectClip()
    {
        var mainHand = InventorySystem.Instance?.GetEquippedLoot(EquipSlot.MainHand);
        return (mainHand != null && mainHand.isTwoHanded) ? twoHandedIdlePose : oneHandedIdlePose;
    }

    void PlayPose(AnimationClip clip)
    {
        if (clip == null) { StopPose(); return; }
        var animator = ResolveAnimator();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        if (!_posing)
        {
            _savedController = animator.runtimeAnimatorController;
            _override        = new AnimatorOverrideController(_savedController);
            animator.runtimeAnimatorController = _override;
            _posing = true;
        }

        // Replace every clip in the controller with the chosen idle so whatever
        // state is active shows it.
        _override.GetOverrides(_overrideList);
        for (int i = 0; i < _overrideList.Count; i++)
            _overrideList[i] = new KeyValuePair<AnimationClip, AnimationClip>(_overrideList[i].Key, clip);
        _override.ApplyOverrides(_overrideList);
    }

    void StopPose()
    {
        if (!_posing) return;
        _posing = false;
        if (_animator != null)
            _animator.runtimeAnimatorController = _savedController;
        _override = null;
    }

    Animator ResolveAnimator()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        return _animator;
    }

    void OnDisable() => StopPose();

    // Called by PlayerManager after a model swap so we re-bind to the new rig.
    public void RefreshAnimator()
    {
        StopPose();
        _current  = null;
        _animator = null;
    }
}
