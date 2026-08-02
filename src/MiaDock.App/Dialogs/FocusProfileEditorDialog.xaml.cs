using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;

namespace MiaDock.App.Dialogs;

public sealed partial class FocusProfileEditorDialog : ContentDialog
{
    private readonly IAppLocalizationService _localization;
    private bool _synchronizingColor;

    public FocusProfileEditorDialog(
        FocusProfileEditorViewModel viewModel,
        IAppLocalizationService localization)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _localization = localization ??
            throw new ArgumentNullException(nameof(localization));
        InitializeComponent();
        DataContext = viewModel;
        _localization.LanguageChanged += OnLanguageChanged;
        ApplyLocalization();
    }

    public FocusProfileEditorViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs args) =>
        SynchronizeColorPicker();

    private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args) =>
        _localization.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        ApplyLocalization();

    private void ApplyLocalization()
    {
        Title = ViewModel.IsNew
            ? _localization.Get("Focus.Settings.Editor.NewTitle")
            : _localization.Get("Focus.Settings.Editor.EditTitle");
        PrimaryButtonText = _localization.Get("Focus.Settings.Editor.Save");
        CloseButtonText = _localization.Get("Common.Cancel");
        _localization.Apply(this);
    }

    private void OnColorChanged(
        ColorPicker sender,
        ColorChangedEventArgs args)
    {
        if (_synchronizingColor)
        {
            return;
        }

        ViewModel.ColorHex =
            $"#{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}";
    }

    private void OnAddScheduleClick(object sender, RoutedEventArgs args)
    {
        ViewModel.AddSchedule();
        _localization.Apply(this);
    }

    private void OnRemoveScheduleClick(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement
            {
                Tag: FocusScheduleEditorViewModel schedule
            })
        {
            ViewModel.RemoveSchedule(schedule);
        }
    }

    private void OnAddAutomationRuleClick(object sender, RoutedEventArgs args)
    {
        ViewModel.AddAutomationRule();
        _localization.Apply(this);
    }

    private void OnRemoveAutomationRuleClick(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement
            {
                Tag: FocusAutomationRuleEditorViewModel rule
            })
        {
            ViewModel.RemoveAutomationRule(rule);
        }
    }

    private void SynchronizeColorPicker()
    {
        _synchronizingColor = true;
        try
        {
            ProfileColorPicker.Color = ColorParser.ParseRgb(ViewModel.ColorHex);
        }
        finally
        {
            _synchronizingColor = false;
        }
    }
}
