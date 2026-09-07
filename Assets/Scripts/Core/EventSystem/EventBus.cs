using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;

public class EventBus
{
    private static readonly EventBus instanace = new();
    public static EventBus Instance => instanace;

    // public readonly Dictionary<Type, Delegate> Events = new();
    public readonly Dictionary<Type, EventGroup> EventHandlers = new();
    public readonly Dictionary<Type, List<EventEntry>> PendingBeforeEventHandlers = new();
    public readonly Dictionary<Type, List<EventEntry>> PendingMainEventHandlers = new();
    public readonly Dictionary<Type, List<EventEntry>> PendingAfterEventHandlers = new();

    public readonly Dictionary<Type, List<Delegate>> PendingRemoveHandlers = new();
    private int publishDepth;
    public bool IsPublishing => publishDepth > 0;   // true while any (possibly nested) publish is in flight
    public void Subscribe<T>(Action<T> evt) where T : GameEvent
    {
        Type type = typeof(T);
        // if(Events.TryGetValue(type, out var d))Events[type] = Delegate.Combine(d, evt);
        // else Events[type] = evt;


        Subscribe<T>(evt, 0, EventStage.Main);
        

    }

    public void Subscribe<T>(Action<T> evt, int priority, EventStage stage)
    {
        Type type = typeof(T);

        var entry = new EventEntry()
        {
            Priority = priority,
            Handler = evt
        };
        if (IsPublishing) // Some Event is Publishing
        {
            var pending = stage == EventStage.Before ? PendingBeforeEventHandlers
                        : stage == EventStage.Main ? PendingMainEventHandlers
                        : PendingAfterEventHandlers;
            if(!pending.TryGetValue(type, out var list))
                pending[type] = list = new();
            list.Add(entry);
            return;
        }

        if(EventHandlers.TryGetValue(type, out var group))
        {

            if(stage == EventStage.Before){
                group.Before.Add(entry);
                group.Before.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
            else if(stage == EventStage.Main)
            {
                group.Main.Add(entry);
                group.Main.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
            else
            {
                group.After.Add(entry);
                group.After.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }
        else
        {
            var newGroup = new EventGroup();
            if(stage == EventStage.Before)newGroup.Before.Add(entry);
            else if(stage == EventStage.Main)newGroup.Main.Add(entry);
            else newGroup.After.Add(entry);
            EventHandlers[type] = newGroup;
        }
    }

    public void Unsubscribe<T>(Action<T> evt) where T : GameEvent
    {
        // Legacy registration (Delegate.Combine chain used by the old Publish).
        // if(Events.TryGetValue(typeof(T), out var d))
        // {
        //     d = Delegate.Remove(d, evt);
        //     if(d == null)Events.Remove(typeof(T));
        //     else Events[typeof(T)] = d;
        // }
        if (IsPublishing)
        {
            if(!PendingRemoveHandlers.TryGetValue(typeof(T), out var list))
                PendingRemoveHandlers[typeof(T)] = list = new();
            list.Add(evt);
            return;
        }

        // New registration (Main group of the stage entry).
        if(EventHandlers.TryGetValue(typeof(T), out var group))
        {
            group.Before.RemoveAll(e => e.Handler == evt);
            group.Main.RemoveAll(e => e.Handler == evt);
            group.After.RemoveAll(e => e.Handler == evt);
        }
    }

    public void Publish<T>(T evt) where T : GameEvent
    {
        publishDepth++;
        try
        {
            if(EventHandlers.TryGetValue(typeof(T), out var group))
            {
                foreach(var handler in group.Before)
                {
                    ((Action<T>)handler.Handler).Invoke(evt);
                    if(evt.IsCanceled)break;
                }
                if(evt.IsCanceled)return;
                foreach(var handler in group.Main)
                {
                    ((Action<T>)handler.Handler).Invoke(evt);
                    if(evt.IsCanceled)break;
                }
                if(evt.IsCanceled)return;
                foreach(var handler in group.After)
                {
                    ((Action<T>)handler.Handler).Invoke(evt);
                    if(evt.IsCanceled)break;
                }
            }
        }
        finally
        {
            publishDepth--;
            // Flush deferred registrations only when the outermost publish ends:
            // inner publishes must keep IsPublishing true, otherwise a nested
            // publish could write into the group list the outer publish is
            // still iterating.
            if(publishDepth == 0)ProcessPendingEventHandlers();
        }
    }

    // Runs after a publish ends: subscriptions/unsubscriptions queued while
    // publishing (IsPublishing) are flushed into the stage groups. A pending
    // type may have no group yet (its main event registered during the same
    // publish), so the group is created on demand.
    private void ProcessPendingEventHandlers()
    {
        ProcessPendingSubscriptions(PendingBeforeEventHandlers, EventStage.Before);
        ProcessPendingSubscriptions(PendingMainEventHandlers, EventStage.Main);
        ProcessPendingSubscriptions(PendingAfterEventHandlers, EventStage.After);

        foreach(var kv in PendingRemoveHandlers)
        {
            if(!EventHandlers.TryGetValue(kv.Key, out var group))continue;
            foreach(var handler in kv.Value)
            {
                group.Before.RemoveAll(e => e.Handler == handler);
                group.Main.RemoveAll(e => e.Handler == handler);
                group.After.RemoveAll(e => e.Handler == handler);
            }
        }
        PendingRemoveHandlers.Clear();
    }

    private void ProcessPendingSubscriptions(Dictionary<Type, List<EventEntry>> pending, EventStage stage)
    {
        foreach(var kv in pending)
        {
            if(!EventHandlers.TryGetValue(kv.Key, out var group))
            {
                group = new EventGroup();
                EventHandlers[kv.Key] = group;
            }
            var list = stage == EventStage.Before ? group.Before
                    : stage == EventStage.Main ? group.Main
                    : group.After;
            list.AddRange(kv.Value);
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        pending.Clear();
    }
}