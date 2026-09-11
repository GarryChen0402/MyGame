using UnityEngine;

public class EnergyDataContainer : DataContainer
{
    public int Current;
    public int Max;

    // Pack (P2, initial scope): the current energy as a bare integer, consumed
    // by a channel binding entry (IntDataParser) once the first machine using
    // this container lands - no consumer exists yet.
    public override string GetPackData() => Current.ToString();

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