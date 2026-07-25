namespace MiaDock.Core.Modules;

public sealed record ModuleOrchestratorStatistics(
    long ReceivedEvents,
    long CoalescedEvents,
    long DroppedEvents,
    long ExpiredEvents,
    int PendingEvents);
