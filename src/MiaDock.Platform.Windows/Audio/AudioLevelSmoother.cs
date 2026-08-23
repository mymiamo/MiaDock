using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Audio;

public sealed class AudioLevelSmoother
{
    private double _left;
    private double _right;

    public MediaAudioLevelSnapshot Update(double peak) => Update(peak, peak);

    public MediaAudioLevelSnapshot Update(double leftPeak, double rightPeak)
    {
        var shapedLeft = Shape(leftPeak);
        var shapedRight = Shape(rightPeak);
        _left = Follow(_left, shapedLeft, 0.88, 0.28);
        _right = Follow(_right, shapedRight, 0.88, 0.28);
        return MediaAudioLevelSnapshot.FromSessionPeak(_left, _right);
    }

    public MediaAudioLevelSnapshot Reset()
    {
        _left = _right = 0;
        return MediaAudioLevelSnapshot.Silent;
    }

    private static double Shape(double peak)
    {
        var normalized = Math.Clamp(peak, 0, 1);
        return Math.Sqrt(normalized);
    }

    private static double Follow(double current, double target, double attack, double release) =>
        current + (target - current) * (target >= current ? attack : release);
}
