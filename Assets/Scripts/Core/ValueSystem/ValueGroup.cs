using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

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

public class ValueEntry
{
    public Dictionary<string, ValueModifierUnit> MultiParts {get; private set;}= new();
    private float baseValue;
    public float CurrentValue => GetValue();

    public ValueEntry(float baseVal) => baseValue = baseVal;

    private float GetValue()
    {
        float target = baseValue;
        foreach(var unit in MultiParts)target = unit.Value.Apply(target);
        return target;
    }

    public bool AddNewPart(string id, ValueModifierUnit unit)
    {
        if(MultiParts.ContainsKey(id))return false;
        MultiParts[id] = unit;
        return true;
    }

    public bool TryGetParam(string unitId, string paramKey, out float value)
    {
        value = 0;
        if(!MultiParts.ContainsKey(unitId))return false;
        return MultiParts[unitId].TryGetParam(paramKey, out value);
    }

    public bool TrySetParam(string unitId, string paramKey, float value)
    {
        if(!MultiParts.ContainsKey(unitId))return false;
        return MultiParts[unitId].TrySetParam(paramKey, value);
    }

    public void SetBaseValue(float val)
        => baseValue = val;
    
    
}