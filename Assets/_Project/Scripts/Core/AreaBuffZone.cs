using UnityEngine;

// Trigger volume that applies an area-bound buff/affliction while a character
// is inside, and removes it on exit. Requires a trigger collider.
[RequireComponent(typeof(Collider))]
public class AreaBuffZone : MonoBehaviour
{
    [Tooltip("Must match the areaId on the WhileInArea buff(s) this zone applies.")]
    public string areaId;
    public BuffData[] areaBuffs;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<CharacterBuffs>(out var buffs)) return;
        foreach (BuffData buff in areaBuffs)
            if (buff != null && buff.durationType == BuffDurationType.WhileInArea)
                buffs.Apply(buff);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent<CharacterBuffs>(out var buffs)) return;
        buffs.RemoveAreaBuffs(areaId);
    }
}
