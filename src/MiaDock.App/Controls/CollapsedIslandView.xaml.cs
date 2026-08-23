using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class CollapsedIslandView : UserControl, IModuleViewActivationAware
{
    private MusicModuleViewModel? _music;
    private bool _isPresentationActive;

    public CollapsedIslandView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public void SetPresentationActive(bool isActive)
    {
        _isPresentationActive = isActive;
        UpdateMeterActivity();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        AttachMusic(DataContext as MusicModuleViewModel);
        UpdateMeterActivity();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _music?.SetAudioMeterActive(this, false);
        AttachMusic(null);
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (IsLoaded)
        {
            AttachMusic(args.NewValue as MusicModuleViewModel);
            UpdateMeterActivity();
        }
    }

    private void AttachMusic(MusicModuleViewModel? music)
    {
        if (ReferenceEquals(_music, music))
        {
            return;
        }

        _music?.SetAudioMeterActive(this, false);
        _music = music;
    }

    private void UpdateMeterActivity() =>
        _music?.SetAudioMeterActive(this, IsLoaded && _isPresentationActive);
}
