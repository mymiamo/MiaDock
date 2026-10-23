namespace MiaDock.Core.Overlay;

public sealed class DockPositionEditSession
{
    private double _originalOffset;
    private double _dragStartX;
    private double _dragStartOffset;
    private double _dragScale;
    public bool IsEditing { get; private set; }
    public bool IsDragging { get; private set; }
    public double PreviewOffset { get; private set; }
    public event EventHandler? EditRequested;
    public event EventHandler? EditingChanged;
    public event EventHandler<double>? OffsetChanged;
    public event EventHandler<bool>? Completed;

    public void RequestEdit() { if (!IsEditing) EditRequested?.Invoke(this, EventArgs.Empty); }
    public void Begin(double offset)
    {
        if (IsEditing || !double.IsFinite(offset)) return;
        _originalOffset = PreviewOffset = offset;
        IsEditing = true;
        EditingChanged?.Invoke(this, EventArgs.Empty);
    }
    public void BeginDrag(double physicalX, double scale)
    {
        EndDrag();
        if (!IsEditing || !double.IsFinite(physicalX) || !double.IsFinite(scale) || scale <= 0) return;
        _dragStartX = physicalX; _dragStartOffset = PreviewOffset; _dragScale = scale; IsDragging = true;
    }
    public void DragTo(double physicalX)
    {
        if (IsDragging && double.IsFinite(physicalX)) SetPreview(_dragStartOffset + (physicalX - _dragStartX) / _dragScale);
    }
    public void EndDrag() => IsDragging = false;
    public void Nudge(double dips) { if (double.IsFinite(dips)) SetPreview(PreviewOffset + dips); }
    public void Reset() { EndDrag(); SetPreview(0); }
    public void Save() => Complete(true);
    public void Cancel() => Complete(false);
    private void SetPreview(double value)
    {
        if (!IsEditing) return;
        PreviewOffset = Math.Clamp(value, -8192, 8192);
        OffsetChanged?.Invoke(this, PreviewOffset);
    }
    private void Complete(bool save)
    {
        if (!IsEditing) return;
        EndDrag();
        if (!save) SetPreview(_originalOffset);
        Completed?.Invoke(this, save);
        IsEditing = false;
        EditingChanged?.Invoke(this, EventArgs.Empty);
    }
}
