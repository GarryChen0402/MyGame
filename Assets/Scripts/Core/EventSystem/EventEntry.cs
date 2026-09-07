using System;
using System.Collections.Generic;

public class EventEntry
{
    public int Priority;
    public Delegate Handler;
}

public class EventGroup
{
    public List<EventEntry> Before = new();
    public List<EventEntry> Main = new();
    public List<EventEntry> After = new();

}

public enum EventStage
{
    Before,
    Main,
    After
}