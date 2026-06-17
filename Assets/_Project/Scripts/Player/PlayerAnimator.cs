using UnityEngine;

// Thin adapter so PlayerController can call SetSpeed() without knowing about
// CharacterAnimator directly. All logic lives in CharacterAnimator.
[RequireComponent(typeof(CharacterAnimator))]
public class PlayerAnimator : MonoBehaviour
{
    CharacterAnimator _charAnim;

    void Awake() => _charAnim = GetComponent<CharacterAnimator>();

    public void SetSpeed(float normalisedSpeed) => _charAnim.SetSpeed(normalisedSpeed);
}
