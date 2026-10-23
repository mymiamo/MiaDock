using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Globalization;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace MiaDock.App.Controls;

public sealed partial class BatteryStatusIcon : UserControl
{
    private readonly AccessibilitySettings _accessibility = new();
    private readonly UISettings _uiSettings = new();
    public static readonly DependencyProperty ChargePercentProperty = DependencyProperty.Register(nameof(ChargePercent), typeof(double),
        typeof(BatteryStatusIcon), new PropertyMetadata(0d, OnStateChanged));
    public static readonly DependencyProperty IsChargingProperty = DependencyProperty.Register(nameof(IsCharging), typeof(bool),
        typeof(BatteryStatusIcon), new PropertyMetadata(false, OnStateChanged));
    public static readonly DependencyProperty HasReadingProperty = DependencyProperty.Register(nameof(HasReading), typeof(bool),
        typeof(BatteryStatusIcon), new PropertyMetadata(true, OnStateChanged));
    public static readonly DependencyProperty IsBatteryPresentProperty = DependencyProperty.Register(nameof(IsBatteryPresent), typeof(bool),
        typeof(BatteryStatusIcon), new PropertyMetadata(true, OnStateChanged));
    public static readonly DependencyProperty ShowPercentageProperty = DependencyProperty.Register(nameof(ShowPercentage), typeof(bool),
        typeof(BatteryStatusIcon), new PropertyMetadata(false, OnStateChanged));

    public double ChargePercent { get => (double)GetValue(ChargePercentProperty); set => SetValue(ChargePercentProperty, value); }
    public bool IsCharging { get => (bool)GetValue(IsChargingProperty); set => SetValue(IsChargingProperty, value); }
    public bool HasReading { get => (bool)GetValue(HasReadingProperty); set => SetValue(HasReadingProperty, value); }
    public bool IsBatteryPresent { get => (bool)GetValue(IsBatteryPresentProperty); set => SetValue(IsBatteryPresentProperty, value); }
    public bool ShowPercentage { get => (bool)GetValue(ShowPercentageProperty); set => SetValue(ShowPercentageProperty, value); }

    public BatteryStatusIcon()
    {
        InitializeComponent();
        Loaded += (_, _) => { _uiSettings.ColorValuesChanged += OnColorsChanged; UpdateVisual(); };
        Unloaded += (_, _) => _uiSettings.ColorValuesChanged -= OnColorsChanged;
        ActualThemeChanged += (_, _) => UpdateVisual();
        UpdateVisual();
    }
    private static void OnStateChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args) => ((BatteryStatusIcon)owner).UpdateVisual();
    private void OnColorsChanged(UISettings sender, object args) => DispatcherQueue.TryEnqueue(UpdateVisual);
    private void UpdateVisual()
    {
        if (ChargeFill is null) return;
        var available = IsBatteryPresent && HasReading && double.IsFinite(ChargePercent);
        var charging = available && IsCharging;
        var charge = double.IsFinite(ChargePercent) ? Math.Clamp(ChargePercent, 0, 100) : 0;
        var highContrast = _accessibility.HighContrast;
        Brush stateBrush = highContrast
            ? new SolidColorBrush(_uiSettings.GetColorValue(UIColorType.Foreground))
            : charging ? new SolidColorBrush(Color.FromArgb(255, 76, 195, 95))
            : available && charge <= 10 ? new SolidColorBrush(Color.FromArgb(255, 255, 69, 58))
            : available && charge <= 20 ? new SolidColorBrush(Color.FromArgb(255, 255, 199, 0))
            : Foreground;
        BatteryBody.BorderBrush = BatteryTip.Background = ChargeFill.Background = stateBrush;
        ChargeFill.Width = available ? 22 * charge / 100 : 0;
        BatteryCanvas.Width = ShowPercentage ? 38 : 32;
        PercentageOverlay.Visibility = ShowPercentage ? Visibility.Visible : Visibility.Collapsed;
        PercentageText.Text = FilledPercentageText.Text = available
            ? Math.Round(charge, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)
            : IsBatteryPresent ? "?" : "–";
        PercentageText.Foreground = highContrast ? stateBrush : Foreground;
        FilledPercentageText.Foreground = new SolidColorBrush(highContrast
            ? _uiSettings.GetColorValue(UIColorType.Background) : Color.FromArgb(255, 20, 20, 20));
        PercentageFillClip.Rect = new Rect(3, 3, ChargeFill.Width, 8);
        ChargingBolt.Visibility = charging ? Visibility.Visible : Visibility.Collapsed;
        ChargingBolt.RenderTransform = ShowPercentage
            ? new CompositeTransform { ScaleX = 0.5, ScaleY = 0.5, TranslateX = 25, TranslateY = 4 }
            : null;
        ChargingBolt.Fill = highContrast ? stateBrush : new SolidColorBrush(Microsoft.UI.Colors.White);
        ChargingBolt.Stroke = highContrast
            ? new SolidColorBrush(_uiSettings.GetColorValue(UIColorType.Background)) : new SolidColorBrush(Color.FromArgb(255, 96, 96, 96));
        UnknownMark.Visibility = available || ShowPercentage ? Visibility.Collapsed : Visibility.Visible;
        UnknownMark.Text = IsBatteryPresent ? "?" : "–";
    }
}
