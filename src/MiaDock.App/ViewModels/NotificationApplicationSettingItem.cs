using CommunityToolkit.Mvvm.ComponentModel;

namespace MiaDock.App.ViewModels;

public sealed class NotificationApplicationSettingItem : ObservableObject
{
    private readonly Action<NotificationApplicationSettingItem> _changed;
    private bool _isVisible;
    private bool _showBody;
    private bool _synchronizing;

    public NotificationApplicationSettingItem(
        string id,
        string displayName,
        bool isVisible,
        bool showBody,
        Action<NotificationApplicationSettingItem> changed)
    {
        Id = id;
        DisplayName = displayName;
        _isVisible = isVisible;
        _showBody = showBody;
        _changed = changed;
    }

    public string Id { get; }
    public string DisplayName { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (!SetProperty(ref _isVisible, value)) return;
            if (!value) ShowBody = false;
            if (!_synchronizing) _changed(this);
        }
    }

    public bool ShowBody
    {
        get => _showBody;
        set
        {
            if (!SetProperty(ref _showBody, value)) return;
            if (value && !IsVisible)
            {
                _synchronizing = true;
                IsVisible = true;
                _synchronizing = false;
            }
            if (!_synchronizing) _changed(this);
        }
    }
}
