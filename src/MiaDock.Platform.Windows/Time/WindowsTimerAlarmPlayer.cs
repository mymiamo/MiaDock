using System.Runtime.InteropServices;
using MiaDock.Modules.Time.Services;

namespace MiaDock.Platform.Windows.Time;

public sealed class WindowsTimerAlarmPlayer : ITimerAlarmPlayer
{
    private const uint SoundAlias = 0x00010000;
    private const uint SoundSystem = 0x00200000;
    private int _isPlaying;

    public void Play()
    {
        if (Interlocked.Exchange(ref _isPlaying, 1) != 0)
        {
            return;
        }

        _ = Task.Run(PlayAlarmSequence);
    }

    private void PlayAlarmSequence()
    {
        try
        {
            for (var index = 0; index < 3; index++)
            {
                PlaySound("SystemExclamation", nint.Zero, SoundAlias | SoundSystem);
                if (index < 2)
                {
                    Thread.Sleep(180);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isPlaying, 0);
        }
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string sound, nint module, uint flags);
}
