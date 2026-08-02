namespace MiaDock.Core.Focus;

public interface IApplicationActivityService : IDisposable
{
    ApplicationActivitySnapshot Current { get; }

    Exception? LastFailure { get; }

    event EventHandler<ApplicationActivitySnapshot>? ActivityChanged;

    void Start();

    void Refresh();
}
