using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class ClipboardPeekExpandedView : UserControl, IModuleViewActivationAware
{
    public ClipboardPeekExpandedView() => InitializeComponent();

    public void SetPresentationActive(bool isActive)
    {
        if (!isActive && DataContext is ClipboardPeekViewModel viewModel)
            viewModel.ClearReveal();
    }
}
