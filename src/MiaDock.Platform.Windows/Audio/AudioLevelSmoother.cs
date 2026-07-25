using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Audio;

public sealed class AudioLevelSmoother
{
    private const double MinimumScale = 0.18;
    private double _left = MinimumScale;
    private double _center = MinimumScale;
    private double _right = MinimumScale;

    public MediaAudioLevelSnapshot Update(double peak) => Update(peak, peak);

    public MediaAudioLevelSnapshot Update(double leftPeak, double rightPeak)
    {
        var shapedLeft = Shape(leftPeak);
        var shapedRight = Shape(rightPeak);
        var shapedCenter = Math.Max(shapedLeft, shapedRight);
        _left = Follow(_left, shapedLeft, 0.88, 0.28);
        _center = Follow(_center, shapedCenter, 0.94, 0.34);
        _right = Follow(_right, shapedRight, 0.88, 0.28);
        return new MediaAudioLevelSnapshot(true, _left, _center, _right);
    }

    public MediaAudioLevelSnapshot Reset()
    {
        _left = _center = _right = MinimumScale;
        return MediaAudioLevelSnapshot.Silent;
    }

    private static double Shape(double peak)
    {
        var normalized = Math.Clamp(peak, 0, 1);
        return MinimumScale + Math.Sqrt(normalized) * (1 - MinimumScale);
    }

    private static double Follow(double current, double target, double attack, double release) =>
        current + (target - current) * (target >= current ? attack : release);
}
