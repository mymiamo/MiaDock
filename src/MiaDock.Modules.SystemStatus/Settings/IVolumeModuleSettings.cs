namespace MiaDock.Modules.SystemStatus.Settings;

public interface IVolumeModuleSettings
{
    VolumeModuleOptions Current { get; }

    event EventHandler<VolumeModuleOptions>? Changed;
}
