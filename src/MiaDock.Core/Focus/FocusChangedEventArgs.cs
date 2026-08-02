namespace MiaDock.Core.Focus;

public sealed class FocusChangedEventArgs(
    FocusSnapshot previous,
    FocusSnapshot current,
    FocusChangeReason reason) : EventArgs
{
    public FocusSnapshot Previous { get; } = previous;

    public FocusSnapshot Current { get; } = current;

    public FocusChangeReason Reason { get; } = reason;
}
