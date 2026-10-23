using System.Diagnostics;
using System.Text.Json;
using MiaDock.App.Animations;
using MiaDock.App.Services;
using MiaDock.Core.Diagnostics;
using MiaDock.Core.Presentation;
using MiaDock.Core.Overlay;
using MiaDock.Platform.Windows.Display;
using MiaDock.Platform.Windows.Overlay;
using MiaDock.UI.Icons;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Hosting;

namespace MiaDock.NativeMotion;

/// <summary>Real WinUI coordinator with deterministic fake module/event content. No user settings or app instance is touched.</summary>
public sealed partial class App : Application
{
    private Window? _window;
    private readonly List<string> _failures = [];
    private readonly List<object> _runs = [];
    private readonly Preferences _preferences = new();
    private readonly Dictionary<IslandVisualState, FrameworkElement> _views = [];
    private readonly Grid _root = new();
    private readonly Border _surface = new() { Background = new SolidColorBrush(Colors.Black) };
    private IslandAnimationCoordinator? _coordinator;
    private IOverlayWindowController? _controller;
    private readonly DisplayTopologyService _displays = new();
    private readonly AnimationPerformanceMonitor _diagnostics = new();
    private IslandVisualState _state;
    private int _actions, _writes;
    private static TimeSpan Now => TimeSpan.FromSeconds(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);

    public App() => InitializeComponent();
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var output = arguments.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? throw new ArgumentException("An absolute output JSON path is required.");
        var measure = arguments.Contains("--measure", StringComparer.Ordinal);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _window = new Window { Title = "MiaDock native motion validation (fake providers)" };
            _root.Children.Add(_surface);
            foreach (var state in Enum.GetValues<IslandVisualState>())
            {
                var view = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Padding = new Thickness(12) };
                view.Children.Add(new DockIcon { IconKey = "musical-notes", FontSize = 22, Foreground = new SolidColorBrush(Colors.White) });
                view.Children.Add(new TextBlock { Text = $"Fake provider / {state}", Foreground = new SolidColorBrush(Colors.White) });
                _views[state] = view;
                _root.Children.Add(view);
            }
            _window.Content = _root;
            _displays.Start();
            _controller = new OverlayWindowControllerFactory(new OverlayPlacementCalculator(), _displays).Create(_window, OverlayWindowOptions.Default);
            var constructor = typeof(IslandAnimationCoordinator).GetConstructors().Single();
            var dependencies = new List<object> { _root, _surface, _views, (Action<IslandVisualMetrics>)Apply,
                _preferences, IslandMotionOptions.Default, IslandLayoutOptions.Default };
            if (constructor.GetParameters().Length == 8) dependencies.Add(_diagnostics);
            _coordinator = (IslandAnimationCoordinator)constructor.Invoke(dependencies.ToArray());
            _coordinator.ApplyInitialState(IslandVisualState.Collapsed);
            _controller.ShowNoActivate();
            await Task.Delay(1000);
            if (!arguments.Contains("--baseline", StringComparer.Ordinal)) VerifyPlacementCoalescing();
            var ready = stopwatch.Elapsed.TotalMilliseconds;
            for (var i = 0; i < 1000; i++) { Act(i); await Task.Delay(8); }
            SetState(IslandVisualState.Collapsed);
            await Task.Delay(1000);
            VerifyFinal(IslandVisualState.Collapsed, "1000 rapid interactions");
            if (!arguments.Contains("--baseline", StringComparer.Ordinal)) await VerifyCancellationOwnership();
            _preferences.Set(false);
            await Task.Delay(50);
            SetState(IslandVisualState.ExpandedModule);
            VerifyFinal(IslandVisualState.ExpandedModule, "reduced motion");
            _preferences.Set(true);
            if (!arguments.Contains("--baseline", StringComparer.Ordinal))
            {
                _diagnostics.Start(); _diagnostics.Start();
                SetState(IslandVisualState.Hover); await Task.Delay(450);
                SetState(IslandVisualState.Collapsed); await Task.Delay(450);
                if (_diagnostics.Snapshot()!.CompletedTransitions < 2) _failures.Add("Completed transition timings were not recorded");
                _diagnostics.Stop();
            }
            if (measure)
            {
                for (var run = 0; run < 3; run++)
                {
                    _diagnostics.Start();
                    var clock = Stopwatch.StartNew();
                    while (clock.Elapsed < TimeSpan.FromSeconds(60)) { Act(_actions); await Task.Delay(73); }
                    var summary = _diagnostics.Snapshot()!;
                    if (summary.IsRunning) _failures.Add("Measurement deadline did not stop automatically");
                    _runs.Add(summary);
                }
            }
            SetState(IslandVisualState.Collapsed);
            await Task.Delay(1000);
            VerifyFinal(IslandVisualState.Collapsed, "final");
            _coordinator.Dispose();
            if (_preferences.Subscribers != 0) _failures.Add("Preference subscription leaked after disposal");
            var writesAfterDispose = _writes;
            _preferences.Set(false);
            await Task.Delay(400);
            if (_writes != writesAfterDispose || _coordinator.IsAnimating) _failures.Add("Animation continued after disposal");
            await Task.Run(async () => await _diagnostics.DisposeAsync());
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Passed = _failures.Count == 0,
                Failures = _failures, Actions = _actions, MetricWrites = _writes, FixtureSetupAndWarmupMilliseconds = ready,
                Scale = _root.XamlRoot.RasterizationScale, Runs = _runs,
                Scope = "Native coordinator + fake provider UI + actual overlay HWND. Not whole-app soak or GPU presentation FPS." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            _failures.Add(exception.ToString());
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Passed = false, Failures = _failures }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            _coordinator?.Dispose(); await _diagnostics.DisposeAsync(); _controller?.Dispose(); _displays.Dispose(); _preferences.Dispose();
            Environment.ExitCode = _failures.Count == 0 ? 0 : 1;
            _window?.Close(); Exit();
        }
    }

    private void Apply(IslandVisualMetrics metrics)
    {
        _writes++;
        _root.Width = metrics.Width; _root.Height = metrics.Height;
        _surface.CornerRadius = new(metrics.CornerRadii.TopLeft, metrics.CornerRadii.TopRight, metrics.CornerRadii.BottomRight, metrics.CornerRadii.BottomLeft);
        _controller?.UpdateLayout(new(metrics.Width, metrics.Height), metrics.CornerRadii);
    }

    private void VerifyPlacementCoalescing()
    {
        var property = _controller!.GetType().GetProperty("NativePlacementOperations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var before = (long)property.GetValue(_controller)!;
        var size = new OverlaySize(_root.Width, _root.Height);
        var corners = IslandLayoutOptions.Default.EffectiveCornerRadii;
        for (var i = 0; i < 1000; i++) _controller.UpdateLayout(size, corners);
        _controller.UpdateLayout(size, DockCornerRadii.Uniform(10));
        if ((long)property.GetValue(_controller)! != before) _failures.Add("Unchanged physical bounds caused redundant native placement");
        _controller.UpdateLayout(new(size.Width + 20, size.Height), corners);
        if ((long)property.GetValue(_controller)! != before + 1) _failures.Add("Changed bounds did not perform exactly one placement");
        _controller.UpdateLayout(size, corners);
    }

    private void Act(int index)
    {
        _actions++;
        switch (index % 8)
        {
            case 0: SetState(IslandVisualState.Hover); break;
            case 1: SetState(IslandVisualState.ExpandedModule); break;
            case 2: _coordinator!.RequestModuleTransition(_views[_state], MotionDirection.Next, IslandLayoutOptions.Default with { ExpandedHeight = 320 }); break;
            case 3: _coordinator!.RequestModuleTransition(_views[_state], MotionDirection.Previous, IslandLayoutOptions.Default); break;
            case 4: SetState(IslandVisualState.ModuleNotification); break;
            case 5: SetState(IslandVisualState.ExpandedModule); break;
            case 6: _coordinator!.RequestContentRefresh(_views[_state]); break;
            default: SetState(IslandVisualState.Collapsed); break;
        }
    }

    private void SetState(IslandVisualState target)
    {
        _coordinator!.RequestTransition(new(_state, target, target == IslandVisualState.ModuleNotification ? IslandTrigger.ModuleEventReceived : IslandTrigger.PrimaryInvoked));
        _state = target;
    }

    private void VerifyFinal(IslandVisualState expected, string name)
    {
        var metrics = IslandAnimationProfile.ForState(expected);
        if (_coordinator!.IsAnimating || Math.Abs(_root.Width - metrics.Width) > .01 || Math.Abs(_root.Height - metrics.Height) > .01)
            _failures.Add($"{name}: geometry or running state mismatch");
        foreach (var (state, view) in _views)
        {
            var visual = ElementCompositionPreview.GetElementVisual(view);
            if ((view.Visibility == Visibility.Visible) != (state == expected) || Math.Abs(visual.Opacity - 1) > .01)
                _failures.Add($"{name}: visibility/opacity mismatch for {state}");
        }
    }

    private async Task VerifyCancellationOwnership()
    {
        using var animator = new IslandBoundsAnimator();
        using var cancellation = new CancellationTokenSource();
        var from = IslandAnimationProfile.ForState(IslandVisualState.Collapsed);
        var to = IslandAnimationProfile.ForState(IslandVisualState.Hover);
        var old = animator.AnimateAsync(from, to, TimeSpan.FromSeconds(1), _ => { }, cancellation.Token);
        await Task.Run(cancellation.Cancel);
        var replacement = animator.AnimateAsync(from, to, TimeSpan.FromMilliseconds(100), _ => { }, CancellationToken.None);
        try { await old; } catch (OperationCanceledException) { }
        try { await replacement; } catch (OperationCanceledException) { _failures.Add("Old worker cancellation cancelled a replacement animation"); }
        if (animator.IsRunning) _failures.Add("Bounds rendering subscription leaked");
        // Retarget inside the final apply callback: completing the old session must not complete the new one.
        Task? reentrant = null;
        var replacementApplied = false;
        var first = animator.AnimateAsync(from, to, TimeSpan.FromMilliseconds(40), value =>
        {
            if (value == to) reentrant = animator.AnimateAsync(to, from, TimeSpan.FromMilliseconds(80), current => { replacementApplied = current == from; }, CancellationToken.None);
        }, CancellationToken.None);
        try { await first; } catch (OperationCanceledException) { }
        if (reentrant is null) _failures.Add("Reentrant callback did not run");
        else await reentrant.WaitAsync(TimeSpan.FromSeconds(3));
        if (!replacementApplied) _failures.Add("Reentrant replacement completed without applying its final geometry");
        var element = _views[IslandVisualState.ExpandedModule];
        var factory = new ToolkitAnimationFactory();
        using var oldCancellation = new CancellationTokenSource();
        using var newCancellation = new CancellationTokenSource();
        var oldContent = factory.RefreshAsync(element, TimeSpan.FromSeconds(1), IslandAnimationKind.ScaleFade, .7, oldCancellation.Token);
        oldCancellation.Cancel();
        var newContent = factory.RefreshAsync(element, TimeSpan.FromSeconds(1), IslandAnimationKind.ScaleFade, .7, newCancellation.Token);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Opacity = .42f;
        try { await oldContent; } catch (OperationCanceledException) { }
        await Task.Delay(20);
        if (Math.Abs(visual.Opacity - .42) > .01) _failures.Add("Cancelled toolkit completion reset a new owner's visual");
        newCancellation.Cancel();
        try { await newContent; } catch (OperationCanceledException) { }
        ToolkitAnimationFactory.Reset(visual);
    }

    private sealed class Preferences : IAnimationPreferenceService
    {
        private EventHandler? _changed;
        public int Subscribers { get; private set; }
        public bool AnimationsEnabled { get; private set; } = true;
        public event EventHandler? AnimationsEnabledChanged { add { _changed += value; Subscribers++; } remove { _changed -= value; Subscribers--; } }
        public void Set(bool enabled) { AnimationsEnabled = enabled; _changed?.Invoke(this, EventArgs.Empty); }
        public void Dispose() { _changed = null; }
    }
}
