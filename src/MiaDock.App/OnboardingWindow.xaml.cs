using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition.SystemBackdrops;
using MiaDock.App.Models;
using MiaDock.App.ViewModels;
using MiaDock.App.Views.Onboarding;
using Windows.Graphics;
using MiaDock.App.Infrastructure;
using MiaDock.App.Services;

namespace MiaDock.App;

public sealed partial class OnboardingWindow : Window
{
    private readonly OnboardingViewModel _viewModel;
    private readonly IAppLocalizationService _localization;
    private readonly IReadOnlyDictionary<OnboardingStep, UserControl> _pages;
    private readonly HashSet<string> _approvedModuleIds =
        new(StringComparer.Ordinal);
    private bool _allowClose;
    private bool _closePromptOpen;

    public OnboardingWindow(OnboardingViewModel viewModel, IAppLocalizationService localization)
    {
        InitializeComponent();
        WindowBranding.ApplyIcon(this);
        _viewModel = viewModel;
        _localization = localization;
        Root.DataContext = viewModel;
        _pages = new Dictionary<OnboardingStep, UserControl>
        {
            [OnboardingStep.Welcome] = Page(new WelcomeStepView()),
            [OnboardingStep.Personalization] = Page(new PersonalizationStepView()),
            [OnboardingStep.Interaction] = Page(new UsageStepView()),
            [OnboardingStep.FeaturesAndPrivacy] = Page(new FeaturesAndPrivacyStepView()),
            [OnboardingStep.Ready] = Page(new ReadyStepView())
        };
        AppWindow.Resize(new SizeInt32(1040, 700));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.SetBorderAndTitleBar(true, true);
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        TryApplySystemBackdrop();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        AppWindow.Closing += OnAppWindowClosing;
        ApplyLocalization();
        UpdateStep();
    }

    public event EventHandler? Completed;

    public event EventHandler? Cancelled;

    public void AllowCloseAndClose()
    {
        _allowClose = true;
        DetachEvents();
        Close();
    }

    private T Page<T>(T page) where T : UserControl
    {
        page.DataContext = _viewModel;
        page.Loaded += OnPageLoaded;
        return page;
    }

    private void ApplyLocalization()
    {
        Title = _localization.Text("MiaDock Başlarken", "MiaDock Get Started");
        _localization.Apply(Root);
        foreach (var page in _pages.Values) _localization.Apply(page);
        UpdateStep();
    }

    private void OnLanguageChanged(object? sender, EventArgs args) => ApplyLocalization();

    private void OnPageLoaded(object sender, RoutedEventArgs args) =>
        _localization.Apply((DependencyObject)sender);

    private void OnBackClick(object sender, RoutedEventArgs args)
    {
        _viewModel.MoveBack();
        UpdateStep();
    }

    private async void OnNextClick(object sender, RoutedEventArgs args)
    {
        if (_viewModel.CurrentStep == OnboardingStep.FeaturesAndPrivacy)
        {
            var selected = _viewModel.ModuleOptions
                .Where(option =>
                    option.CanSelectDuringOnboarding &&
                    option.IsSelected &&
                    !_approvedModuleIds.Contains(option.ModuleId))
                .Select(option =>
                    ModuleServiceDisclosureCatalog.Get(
                        option.ModuleId,
                        _localization))
                .Where(disclosure =>
                    !disclosure.RequiresWindowsPermission)
                .ToArray();
            if (selected.Length > 0)
            {
                var consentDialog = new Dialogs.ModuleServiceConsentDialog(
                    selected,
                    _localization,
                    isOnboarding: true)
                {
                    XamlRoot = Root.XamlRoot
                };
                if (await consentDialog.ShowAsync() !=
                    ContentDialogResult.Primary)
                {
                    return;
                }

                foreach (var disclosure in selected)
                {
                    _approvedModuleIds.Add(disclosure.ModuleId);
                }
            }
        }

        if (_viewModel.IsLastStep)
        {
            NextButton.IsEnabled = false;
            if (await _viewModel.CompleteAsync())
            {
                _allowClose = true;
                DetachEvents();
                Completed?.Invoke(this, EventArgs.Empty);
                Close();
                return;
            }

            NextButton.IsEnabled = true;
            return;
        }

        _viewModel.MoveNext();
        UpdateStep();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(OnboardingViewModel.CurrentStep))
        {
            UpdateStep();
        }
    }

    private void UpdateStep()
    {
        StepHost.Content = _pages[_viewModel.CurrentStep];
        BackButton.IsEnabled = !_viewModel.IsFirstStep && !_viewModel.IsBusy;
        NextButton.Content = _viewModel.IsLastStep
            ? _localization.Get("Onboarding.Button.Finish")
            : _localization.Get("Onboarding.Button.Next");
        AutomationProperties.SetName(
            NextButton,
            _viewModel.IsLastStep
                ? _localization.Get("Onboarding.Button.Finish.Automation")
                : _localization.Get("Onboarding.Button.Next.Automation"));
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var compact = args.NewSize.Width < 720;
        RailColumn.Width = new GridLength(compact ? 0 : 272);
        StepRail.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactProgress.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        StepScrollViewer.Padding = compact
            ? new Thickness(20, 64, 20, 24)
            : new Thickness(40, 28, 40, 28);
    }

    private void TryApplySystemBackdrop()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        catch
        {
            SystemBackdrop = null;
        }
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        if (_closePromptOpen)
        {
            return;
        }

        _closePromptOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot,
                Title = _localization.Get("Onboarding.Dialog.Incomplete.Title"),
                Content = _localization.Get("Onboarding.Dialog.Incomplete.Description"),
                PrimaryButtonText = _localization.Get("Onboarding.Dialog.Return"),
                SecondaryButtonText = _localization.Get("Onboarding.Dialog.Exit"),
                DefaultButton = ContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Secondary)
            {
                _viewModel.RestorePreviewTheme();
                _allowClose = true;
                DetachEvents();
                Cancelled?.Invoke(this, EventArgs.Empty);
                Close();
            }
        }
        finally
        {
            _closePromptOpen = false;
        }
    }

    private void DetachEvents()
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        foreach (var page in _pages.Values) page.Loaded -= OnPageLoaded;
        AppWindow.Closing -= OnAppWindowClosing;
    }
}
