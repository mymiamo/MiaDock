using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
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
            [OnboardingStep.Startup] = Page(new StartupStepView()),
            [OnboardingStep.Appearance] = Page(new AppearanceStepView()),
            [OnboardingStep.Media] = Page(new MediaStepView()),
            [OnboardingStep.Display] = Page(new DisplayStepView()),
            [OnboardingStep.Interaction] = Page(new InteractionStepView()),
            [OnboardingStep.Fullscreen] = Page(new FullscreenStepView()),
            [OnboardingStep.Modules] = Page(new ModulesStepView()),
            [OnboardingStep.Summary] = Page(new SummaryStepView())
        };
        AppWindow.Resize(new SizeInt32(960, 680));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
            presenter.SetBorderAndTitleBar(true, true);
        }

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
        Title = _localization.Text("MiaDock İlk Kurulum", "MiaDock Setup");
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
