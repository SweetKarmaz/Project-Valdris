using UnityEngine;
using System;
using System.Collections.Generic;

// Tracks the buffs and afflictions currently affecting a character.
// Attach to the player and to any NPC/enemy that can be buffed or cursed.
public class CharacterBuffs : MonoBehaviour
{
    [Serializable]
    public class AppliedBuff
    {
        public BuffData data;
        public float remainingSeconds; // only meaningful for Timed buffs
        public float tickTimer;        // accumulates toward the next DoT tick
        public bool revealed;          // hidden buffs stay invisible until detected

        public AppliedBuff(BuffData data)
        {
            this.data = data;
            remainingSeconds = data.durationSeconds;
            revealed = !data.isHidden;
        }
    }

    private readonly List<AppliedBuff> _active = new();
    private IDamageable _damageable;

    public event Action<BuffData> OnBuffApplied;
    public event Action<BuffData> OnBuffRemoved;

    public IReadOnlyList<AppliedBuff> Active => _active;

    // What UI and inspection should show: everything except unrevealed
    // hidden buffs. Hidden buffs still apply their effects either way.
    public List<AppliedBuff> GetVisible() => _active.FindAll(b => b.revealed);

    public event Action<BuffData> OnBuffRevealed;

    // Reveals hidden buffs of the given status type (e.g. Detect Corruption
    // revealing Corruption taints). Returns how many were newly revealed.
    public int RevealHidden(StatusEffectType type)
    {
        int count = 0;
        foreach (AppliedBuff buff in _active)
        {
            if (buff.revealed || buff.data.statusType != type) continue;
            buff.revealed = true;
            count++;
            OnBuffRevealed?.Invoke(buff.data);
        }
        return count;
    }

    private void OnEnable()
    {
        QuestSystem.OnQuestCompleted += HandleQuestCompleted;
    }

    private void OnDisable()
    {
        QuestSystem.OnQuestCompleted -= HandleQuestCompleted;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        List<DamageInfo> pendingDot = null;

        // Tick durations + DoT; iterate backwards so removal is safe.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            AppliedBuff buff = _active[i];

            // Damage-over-time (Burning, Corruption, …).
            if (buff.data.dotDamagePerTick > 0f)
            {
                float interval = Mathf.Max(0.1f, buff.data.dotTickInterval);
                buff.tickTimer += dt;
                while (buff.tickTimer >= interval)
                {
                    buff.tickTimer -= interval;
                    (pendingDot ??= new List<DamageInfo>())
                        .Add(new DamageInfo(buff.data.dotDamagePerTick, buff.data.dotDamageType));
                }
            }

            if (buff.data.durationType == BuffDurationType.Timed)
            {
                buff.remainingSeconds -= dt;
                if (buff.remainingSeconds <= 0f) RemoveAt(i);
            }
        }

        // Apply DoT AFTER the loop — TakeDamage may modify _active (e.g. break-on-
        // damage buffs), which would corrupt the iteration above.
        if (pendingDot != null)
        {
            _damageable ??= GetComponent<IDamageable>();
            if (_damageable != null)
                foreach (var d in pendingDot) _damageable.TakeDamage(d);
        }
    }

    public void Apply(BuffData data) => Apply(data, -1f);

    // durationOverride > 0 replaces the asset's durationSeconds (e.g. a Sleep
    // whose length scales with the caster's Intelligence).
    public void Apply(BuffData data, float durationOverride)
    {
        if (data == null || Has(data)) return; // no stacking of the same buff
        var buff = new AppliedBuff(data);
        if (durationOverride > 0f) buff.remainingSeconds = durationOverride;
        _active.Add(buff);
        OnBuffApplied?.Invoke(data);
    }

    // Attempts to apply a status-effect buff, honoring this character's
    // immunities and resist chance. Plain buffs (statusType None) skip
    // the roll entirely.
    public StatusApplyResult TryApplyStatus(BuffData data, float durationOverride = -1f)
    {
        if (data == null || Has(data)) return StatusApplyResult.Failed;

        if (data.statusType != StatusEffectType.None &&
            TryGetComponent<CharacterResistances>(out var resistances))
        {
            if (resistances.IsImmune(data.statusType)) return StatusApplyResult.Immune;
            if (UnityEngine.Random.Range(0f, 100f) < resistances.ResistChance(data.statusType))
                return StatusApplyResult.Resisted;
        }

        Apply(data, durationOverride);
        return StatusApplyResult.Applied;
    }

    // ---- Control state (movement/ability locks) ----

    public bool IsMovementPrevented
    {
        get
        {
            foreach (AppliedBuff buff in _active)
                if (buff.data.preventsMovement) return true;
            return false;
        }
    }

    public bool AreAbilitiesPrevented
    {
        get
        {
            foreach (AppliedBuff buff in _active)
                if (buff.data.preventsAbilities) return true;
            return false;
        }
    }

    // Call when this character takes damage; breaks Sleep-style effects.
    public void NotifyDamageTaken()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            if (_active[i].data.breaksOnDamage) RemoveAt(i);
    }

    public void Remove(BuffData data)
    {
        int index = _active.FindIndex(b => b.data == data);
        if (index >= 0) RemoveAt(index);
    }

    public bool Has(BuffData data) => _active.Exists(b => b.data == data);

    // Net flat change to a stat from every active buff and affliction.
    public float TotalFlat(StatType stat) => Total(stat, ModifierMode.Flat);

    // Net percent change to a stat (additive: +15 and +10 give +25).
    public float TotalPercent(StatType stat) => Total(stat, ModifierMode.Percent);

    private float Total(StatType stat, ModifierMode mode)
    {
        float total = 0f;
        foreach (AppliedBuff buff in _active)
            foreach (StatModifier mod in buff.data.modifiers)
                if (mod.stat == stat && mod.mode == mode) total += mod.amount;
        return total;
    }

    // Net percent change to one specific spell from all active buffs/afflictions.
    public float SpellPercent(SpellData spell, Func<SpellModifier, float> selector)
    {
        float total = 0f;
        foreach (AppliedBuff buff in _active)
            foreach (SpellModifier mod in buff.data.spellModifiers)
                if (mod.spell == spell) total += selector(mod);
        return total;
    }

    // Called by AreaBuffZone when the character leaves a zone.
    public void RemoveAreaBuffs(string areaId)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            if (_active[i].data.durationType == BuffDurationType.WhileInArea &&
                _active[i].data.areaId == areaId)
                RemoveAt(i);
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            if (_active[i].data.durationType == BuffDurationType.UntilQuestComplete &&
                _active[i].data.linkedQuest == quest)
                RemoveAt(i);
    }

    private void RemoveAt(int index)
    {
        BuffData removed = _active[index].data;
        _active.RemoveAt(index);
        OnBuffRemoved?.Invoke(removed);
    }

    // ---- Save/load ----

    public List<SavedBuff> CaptureState()
    {
        var saved = new List<SavedBuff>();
        foreach (AppliedBuff buff in _active)
        {
            // Area buffs are not saved; re-entering the zone re-applies them.
            if (buff.data.durationType == BuffDurationType.WhileInArea) continue;
            saved.Add(new SavedBuff { buffName = buff.data.name, remainingSeconds = buff.remainingSeconds, revealed = buff.revealed });
        }
        return saved;
    }

    public void RestoreState(List<SavedBuff> saved, GameDatabase database)
    {
        _active.Clear();
        if (saved == null) return;
        foreach (SavedBuff entry in saved)
        {
            BuffData data = database.FindBuff(entry.buffName);
            if (data == null) { Debug.LogWarning($"Saved buff not found: {entry.buffName}"); continue; }
            var buff = new AppliedBuff(data) { remainingSeconds = entry.remainingSeconds, revealed = entry.revealed };
            _active.Add(buff);
            OnBuffApplied?.Invoke(data);
        }
    }
}

[Serializable]
public class SavedBuff
{
    public string buffName;
    public float remainingSeconds;
    public bool revealed;
}

public enum StatusApplyResult
{
    Applied,
    Resisted, // resist roll succeeded
    Immune,   // target is flat-out immune
    Failed    // invalid target/already affected
}
