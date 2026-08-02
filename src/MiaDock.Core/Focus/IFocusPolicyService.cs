namespace MiaDock.Core.Focus;

public interface IFocusPolicyService : IDisposable
{
    FocusPolicySnapshot Current { get; }

    event EventHandler? PolicyChanged;
}
