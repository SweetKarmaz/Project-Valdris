using System.Collections;
using UnityEngine;

// Sets (or clears) a world flag — e.g. met_prisoner, interrogated.
public class SetFlagStep : EventStep
{
    public string flag;
    public bool   value = true;

    public override IEnumerator Run()
    {
        if (!string.IsNullOrEmpty(flag)) WorldStateSystem.Instance?.SetFlag(flag, value);
        yield break;
    }
}
