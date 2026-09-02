using UnityEngine;

public class EnergyDataContainer : DataContainer
{
    public int Current;
    public int Max;
    public void Add(int amount)
    {
        Current = Mathf.Min(Current + amount, Max);
    }
    public bool TryConsume(int amount)
    {
        if(Current >= amount)
        {
            Current -= amount;
            return true;
        }
        return false;
    }
}