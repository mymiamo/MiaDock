using MiaDock.Core.Overlay;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class DockPositionEditTests
{
    [TestMethod]
    [DataRow(1d)]
    [DataRow(1.5d)]
    [DataRow(2d)]
    public void ScreenCoordinates_MoveWindowWithoutFeedback_AtEachScale(double scale)
    {
        var session = new DockPositionEditSession();
        var calculator = new OverlayPlacementCalculator();
        var request = new OverlayLayoutRequest(new(-2560, 0, 2560, 1440), new(550, 360),
            (uint)(96 * scale), OverlayPosition.TopCenter);
        var origin = calculator.Calculate(request);
        var placement = origin;
        session.OffsetChanged += (_, offset) => placement = calculator.Calculate(
            request with { HorizontalOffsetInDips = offset });
        session.Begin(0);
        session.BeginDrag(-900, scale);
        session.DragTo(-780);
        Assert.AreEqual(origin.X + 120, placement.X);
        // A window move must not be counted as another cursor movement.
        session.DragTo(-780);
        Assert.AreEqual(origin.X + 120, placement.X);
        session.DragTo(-1020);
        Assert.AreEqual(origin.X - 120, placement.X);
        session.EndDrag();
        session.DragTo(-600);
        Assert.AreEqual(origin.X - 120, placement.X);
        Assert.IsTrue(session.IsEditing);
        Assert.IsFalse(session.IsDragging);
    }

    [TestMethod]
    public void ReleaseAndCaptureLoss_KeepEditing_AndNextDragStartsAtLastPreview()
    {
        var session = new DockPositionEditSession();
        var completed = 0;
        session.Completed += (_, _) => completed++;
        session.Begin(20);
        session.BeginDrag(500, 1.5);
        session.DragTo(590);
        session.EndDrag();
        session.DragTo(900);
        Assert.AreEqual(80, session.PreviewOffset);
        Assert.IsTrue(session.IsEditing);
        Assert.AreEqual(0, completed);
        session.BeginDrag(590, 1.5);
        session.DragTo(560);
        Assert.AreEqual(60, session.PreviewOffset);
        session.Save();
        session.DragTo(800);
        Assert.AreEqual(60, session.PreviewOffset);
        Assert.IsFalse(session.IsDragging);
        Assert.AreEqual(1, completed);
    }

    [TestMethod]
    public void InvalidPointerReading_DoesNotMoveOrContinueAnOldDrag()
    {
        var session = new DockPositionEditSession();
        session.Begin(20);
        session.BeginDrag(100, 1);
        session.DragTo(double.NaN);
        session.DragTo(double.PositiveInfinity);
        Assert.AreEqual(20, session.PreviewOffset);
        session.BeginDrag(double.NaN, 1);
        session.DragTo(200);
        Assert.AreEqual(20, session.PreviewOffset);
        Assert.IsFalse(session.IsDragging);
        session.BeginDrag(100, 0);
        session.DragTo(200);
        Assert.AreEqual(20, session.PreviewOffset);
    }

    [TestMethod]
    public void ResetAndCancel_StopCapturedDrag_AndDoNotSaveUntilConfirmed()
    {
        var session = new DockPositionEditSession();
        var saves = new List<double>();
        session.Completed += (_, save) => { if (save) saves.Add(session.PreviewOffset); };
        session.Begin(40);
        session.BeginDrag(100, 1);
        session.DragTo(180);
        session.Reset();
        session.DragTo(220);
        Assert.AreEqual(0, session.PreviewOffset);
        Assert.IsTrue(session.IsEditing);
        Assert.IsEmpty(saves);
        session.Cancel();
        Assert.AreEqual(40, session.PreviewOffset);
        Assert.IsEmpty(saves);
        session.Begin(40);
        session.Reset();
        session.Save();
        Assert.AreEqual(0, saves.Single());
    }

    [TestMethod]
    [DataRow(96u)]
    [DataRow(144u)]
    [DataRow(192u)]
    public void EveryAnchor_StaysInSelectedMonitor_WhenDraggedBeyondBothEdges(uint dpi)
    {
        var calculator = new OverlayPlacementCalculator();
        foreach (var anchor in Enum.GetValues<OverlayPosition>())
        {
            var request = new OverlayLayoutRequest(new(-2560, -200, 2560, 1440), new(550, 360), dpi, anchor);
            var left = calculator.Calculate(request with { HorizontalOffsetInDips = -8192 });
            var right = calculator.Calculate(request with { HorizontalOffsetInDips = 8192 });
            Assert.AreEqual(-2560, left.X, anchor.ToString());
            Assert.AreEqual(-right.Width, right.X, anchor.ToString());
            Assert.AreEqual(left.Y, right.Y);
        }
    }

    [TestMethod]
    public void DragAndKeyboardPreview_UseDips_SaveAndCancelAreDistinct()
    {
        var session = new DockPositionEditSession();
        var saved = new List<double>();
        session.Completed += (_, save) => { if (save) saved.Add(session.PreviewOffset); };
        session.Begin(20); session.BeginDrag(100, 1.5); session.DragTo(160);
        Assert.AreEqual(60, session.PreviewOffset);
        session.Nudge(-10); session.Cancel();
        Assert.AreEqual(20, session.PreviewOffset);
        Assert.AreEqual(0, saved.Count);
        session.Begin(20); session.Nudge(10); session.Save();
        Assert.AreEqual(30, saved.Single());
        session.Begin(30); session.Reset(); session.Save();
        Assert.AreEqual(0, saved.Last());
        Assert.IsFalse(session.IsEditing);
    }

    [TestMethod]
    public void PositionOffset_IsRelativeToAnchor_ClampsToMonitor_AndScalesWithDpi()
    {
        var calculator = new OverlayPlacementCalculator();
        var request = new OverlayLayoutRequest(new(-1920, 0, 1920, 1080), new(300, 50), 144, OverlayPosition.TopCenter);
        var origin = calculator.Calculate(request);
        Assert.AreEqual(origin.X + 150, calculator.Calculate(request with { HorizontalOffsetInDips = 100 }).X);
        Assert.AreEqual(-1920, calculator.Calculate(request with { HorizontalOffsetInDips = -8192 }).X);
        Assert.AreEqual(-450, calculator.Calculate(request with { HorizontalOffsetInDips = 8192 }).X);
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(request with { HorizontalOffsetInDips = double.NaN }));
        Assert.AreEqual(0, SettingsValidator.Normalize(MiaDockSettings.Default with
            { Appearance = AppearanceSettings.Default with { HorizontalOffset = double.NaN } }).Appearance.HorizontalOffset);
    }
}
