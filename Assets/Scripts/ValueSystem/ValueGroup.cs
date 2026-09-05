using System.Collections.Generic;
using UnityEngine;

public class ValueModifierUnit
{
    public Dictionary<string, float> Params{get; protected set;} = null;

    public virtual float Apply(float input){return input;}
    public virtual bool TryGetParam(string key, out float value)
    {
        value = 0;
        if(Params == null)return false;
        return Params.TryGetValue(key, out value);
    }

    public virtual bool TrySetParam(string key, float value) {return false;}
}

public class ChancedModifier : ValueModifierUnit
{
    public ChancedModifier()
    {
        Params = new()
        {
            ["chance"] = 0.05f,
            ["baseRatio"] = 1.0f,
            ["extraRatio"] = 0.5f
        };
    }
    private void SetChance(float val) => Params["chance"] = Mathf.Clamp01(val);
    private void SetBaseRatio(float val) => Params["baseRatio"] = Mathf.Max(val, 0);
    private void SetExtraRatio(float val) => Params["extraRatio"] = Mathf.Max(val, 0);
    public override bool TrySetParam(string key, float value)
    {
        if(!Params.ContainsKey(key))return false;
        switch (key)
        {
            case "chance":     SetChance(value);     break;
            case "baseRatio":  SetBaseRatio(value);  break;
            case "extraRatio": SetExtraRatio(value); break;
            default: return false; 
        }
        return true;
    }

    public override float Apply(float input)
    {
        if(Random.Range(0f, 1f) >= Params["chance"])return input;
        else return input * (Params["baseRatio"] + Params["extraRatio"]);
    }
}
