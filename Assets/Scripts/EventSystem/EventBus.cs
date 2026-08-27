using System;
using System.Collections.Generic;

public class EventBus
{
    private static readonly EventBus instanace = new();
    public static EventBus Instance => instanace;

    public readonly Dictionary<Type, Delegate> Events = new();

    public void Subscribe<T>(Action<T> evt) where T : GameEvent
    {
        Type type = typeof(T);
        if(Events.TryGetValue(type, out var d))Events[type] = Delegate.Combine(d, evt);
        else Events[type] = evt;
    }

    public void Unsubscribe<T>(Action<T> evt) where T : GameEvent
    {
        if(Events.TryGetValue(typeof(T), out var d))
        {
            d = Delegate.Remove(d, evt);
            if(d == null)Events.Remove(typeof(T));
            else Events[typeof(T)] = d;
        }
    }

    public void Publish<T>(T evt) where T : GameEvent
    {
        if(Events.TryGetValue(typeof(T), out var d))
            ((Action<T>)d).Invoke(evt);
    }
}