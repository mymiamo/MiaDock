using MiaDock.Modules.Media.Services;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class MusicModuleViewModelTests
{
    [TestMethod]
    public void LimitedScenario_DisablesUnsupportedCommands()
    {
        var service = new FakeMediaService();
        using var viewModel = new MusicModuleViewModel(service);

        viewModel.SelectedScenario = viewModel.Scenarios.Single(item => item.Id == "limited-controls");

        Assert.IsFalse(viewModel.PreviousCommand.CanExecute(null));
        Assert.IsFalse(viewModel.NextCommand.CanExecute(null));
        Assert.IsTrue(viewModel.PlayPauseCommand.CanExecute(null));
    }

    [TestMethod]
    public void ProgressPercent_SeeksFakeMedia()
    {
        var service = new FakeMediaService();
        using var viewModel = new MusicModuleViewModel(service);

        viewModel.ProgressPercent = 50;

        Assert.AreEqual(50, viewModel.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void SnapshotChange_RaisesFormattedPropertyNotifications()
    {
        var service = new FakeMediaService();
        using var viewModel = new MusicModuleViewModel(service);
        var changedProperties = new HashSet<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? string.Empty);

        service.TogglePlayback();

        Assert.IsTrue(changedProperties.Contains(nameof(viewModel.PlaybackGlyph)));
        Assert.IsTrue(changedProperties.Contains(nameof(viewModel.PositionText)));
    }
}
