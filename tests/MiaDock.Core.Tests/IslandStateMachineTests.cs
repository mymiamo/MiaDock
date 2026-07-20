using MiaDock.Core.Presentation;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class IslandStateMachineTests
{
    [TestMethod]
    public void NewMachine_StartsCollapsed()
    {
        var machine = new IslandStateMachine();

        Assert.AreEqual(IslandVisualState.Collapsed, machine.CurrentState);
        Assert.IsFalse(machine.IsPointerOver);
    }

    [TestMethod]
    public void PointerEntered_FromCollapsed_ShowsHover()
    {
        var machine = new IslandStateMachine();

        var transition = machine.Dispatch(IslandTrigger.PointerEntered);

        Assert.AreEqual(IslandVisualState.Hover, transition.CurrentState);
        Assert.IsTrue(machine.IsPointerOver);
        Assert.IsTrue(transition.Changed);
    }

    [TestMethod]
    public void PointerExited_FromHover_Collapses()
    {
        var machine = new IslandStateMachine();
        machine.Dispatch(IslandTrigger.PointerEntered);

        var transition = machine.Dispatch(IslandTrigger.PointerExited);

        Assert.AreEqual(IslandVisualState.Collapsed, transition.CurrentState);
        Assert.IsFalse(machine.IsPointerOver);
    }

    [TestMethod]
    public void PrimaryInvoked_FromRestingState_ExpandsMusic()
    {
        var machine = new IslandStateMachine();

        machine.Dispatch(IslandTrigger.PrimaryInvoked);

        Assert.AreEqual(IslandVisualState.ExpandedMusic, machine.CurrentState);
    }

    [TestMethod]
    public void TrackChanged_FromCollapsed_ShowsNotification()
    {
        var machine = new IslandStateMachine();

        machine.Dispatch(IslandTrigger.TrackChanged);

        Assert.AreEqual(IslandVisualState.TrackNotification, machine.CurrentState);
    }

    [TestMethod]
    public void TrackChanged_WhileExpanded_DoesNotReplaceUserView()
    {
        var machine = new IslandStateMachine();
        machine.Dispatch(IslandTrigger.PrimaryInvoked);

        var transition = machine.Dispatch(IslandTrigger.TrackChanged);

        Assert.AreEqual(IslandVisualState.ExpandedMusic, transition.CurrentState);
        Assert.IsFalse(transition.Changed);
    }

    [TestMethod]
    public void NotificationElapsed_WithPointerOver_ReturnsToHover()
    {
        var machine = new IslandStateMachine();
        machine.Dispatch(IslandTrigger.PointerEntered);
        machine.Dispatch(IslandTrigger.TrackChanged);

        machine.Dispatch(IslandTrigger.NotificationElapsed);

        Assert.AreEqual(IslandVisualState.Hover, machine.CurrentState);
    }

    [TestMethod]
    public void NotificationElapsed_AfterPointerExit_Collapses()
    {
        var machine = new IslandStateMachine();
        machine.Dispatch(IslandTrigger.PointerEntered);
        machine.Dispatch(IslandTrigger.TrackChanged);
        machine.Dispatch(IslandTrigger.PointerExited);

        machine.Dispatch(IslandTrigger.NotificationElapsed);

        Assert.AreEqual(IslandVisualState.Collapsed, machine.CurrentState);
    }

    [TestMethod]
    public void RepeatedPointerEntered_IsIdempotent()
    {
        var machine = new IslandStateMachine();
        machine.Dispatch(IslandTrigger.PointerEntered);

        var transition = machine.Dispatch(IslandTrigger.PointerEntered);

        Assert.AreEqual(IslandVisualState.Hover, transition.CurrentState);
        Assert.IsFalse(transition.Changed);
    }

    [TestMethod]
    public void CollapseRequested_UsesCurrentPointerState()
    {
        var machine = new IslandStateMachine();
        machine.Dispatch(IslandTrigger.PointerEntered);
        machine.Dispatch(IslandTrigger.PrimaryInvoked);

        machine.Dispatch(IslandTrigger.CollapseRequested);

        Assert.AreEqual(IslandVisualState.Hover, machine.CurrentState);
    }
}
