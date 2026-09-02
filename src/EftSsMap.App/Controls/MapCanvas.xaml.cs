using EftSsMap.App.Imaging;
using EftSsMap.Core.Calibration;
using EftSsMap.Core.Viewport;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;

namespace EftSsMap.App.Controls;

public sealed class MapCanvas : Grid, IDisposable
{
    private const double ClickMovementTolerance = 4;
    private const double ZoomStep = 1.15;
    private const double MinimumFitScaleFactor = 0.25;
    private const double MaximumFitScaleFactor = 32;
    private readonly SKXamlCanvas Surface;
    private readonly CalibrationAnchorOverlay _calibrationAnchorOverlay = new();
    private readonly MarkerDragInteraction _markerDragInteraction = new();
    private LoadedMapImage? _mapImage;
    private ViewportTransform? _viewport;
    private PixelPoint? _markerPosition;
    private PixelPoint? _markerDirection;
    private Point _pointerPressedAt;
    private Point _previousPointerPosition;
    private int? _pressedCalibrationAnchorIndex;
    private bool _isDragging;
    private bool _disposed;

    public MapCanvas()
    {
        Surface = new SKXamlCanvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Surface.PaintSurface += OnPaintSurface;
        Surface.PointerCaptureLost += OnPointerCaptureLost;
        Surface.PointerMoved += OnPointerMoved;
        Surface.PointerPressed += OnPointerPressed;
        Surface.PointerReleased += OnPointerReleased;
        Surface.PointerWheelChanged += OnPointerWheelChanged;
        Surface.SizeChanged += OnSurfaceSizeChanged;
        Children.Add(Surface);
    }

    public event EventHandler<MapImagePixelClickedEventArgs>? ImagePixelClicked;

    public event EventHandler<MarkerCorrectionRequestedEventArgs>? MarkerCorrectionRequested;

    public event EventHandler<CalibrationAnchorSelectedEventArgs>? CalibrationAnchorSelected;

    public bool IsMarkerCorrectionEnabled
    {
        get => _markerDragInteraction.IsEnabled;
        set => _markerDragInteraction.IsEnabled = value;
    }

    public PixelPoint? MarkerPosition
    {
        get => _markerPosition;
        set
        {
            _markerPosition = value;
            Surface.Invalidate();
        }
    }

    public PixelPoint? MarkerDirection
    {
        get => _markerDirection;
        set
        {
            _markerDirection = value;
            Surface.Invalidate();
        }
    }

    public void SetImage(LoadedMapImage? image)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(_mapImage, image))
        {
            return;
        }

        var previous = _mapImage;
        _mapImage = image;
        FitToView();
        previous?.Dispose();
    }

    public void SetMarker(PixelPoint? position, PixelPoint? direction)
    {
        _markerPosition = position;
        _markerDirection = direction;
        Surface.Invalidate();
    }

    public void ShowCalibrationAnchors(
        IReadOnlyList<CalibrationPoint> points,
        int replacementIndex)
    {
        _calibrationAnchorOverlay.Show(points, replacementIndex);
        Surface.Invalidate();
    }

    public void HideCalibrationAnchors()
    {
        _calibrationAnchorOverlay.Hide();
        Surface.Invalidate();
    }

    public void FitToView()
    {
        if (_mapImage is null || Surface.ActualWidth <= 0 || Surface.ActualHeight <= 0)
        {
            _viewport = null;
            Surface.Invalidate();
            return;
        }

        _viewport = ViewportTransform.Fit(
            new Size2D(_mapImage.Fingerprint.Width, _mapImage.Fingerprint.Height),
            new Size2D(Surface.ActualWidth, Surface.ActualHeight));
        Surface.Invalidate();
    }

    public bool TryViewToImage(Point viewPoint, out PixelPoint imagePixel)
    {
        imagePixel = default;
        if (_mapImage is null || _viewport is null)
        {
            return false;
        }

        var converted = _viewport.ViewToImage(new PixelPoint(viewPoint.X, viewPoint.Y));
        if (converted.X < 0
            || converted.Y < 0
            || converted.X >= _mapImage.Fingerprint.Width
            || converted.Y >= _mapImage.Fingerprint.Height)
        {
            return false;
        }

        imagePixel = converted;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Surface.PaintSurface -= OnPaintSurface;
        Surface.PointerCaptureLost -= OnPointerCaptureLost;
        Surface.PointerMoved -= OnPointerMoved;
        Surface.PointerPressed -= OnPointerPressed;
        Surface.PointerReleased -= OnPointerReleased;
        Surface.PointerWheelChanged -= OnPointerWheelChanged;
        Surface.SizeChanged -= OnSurfaceSizeChanged;
        _mapImage?.Dispose();
        _mapImage = null;
        _viewport = null;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs args)
    {
        var canvas = args.Surface.Canvas;
        canvas.Clear(new SKColor(21, 21, 21));

        if (_mapImage is null
            || _viewport is null
            || Surface.ActualWidth <= 0
            || Surface.ActualHeight <= 0)
        {
            return;
        }

        // ViewportTransform uses XAML DIPs. Scale once so Skia surface pixels and pointer DIPs agree.
        canvas.Save();
        canvas.Scale(
            args.Info.Width / (float)Surface.ActualWidth,
            args.Info.Height / (float)Surface.ActualHeight);

        var topLeft = _viewport.ImageToView(default);
        var bottomRight = _viewport.ImageToView(new PixelPoint(
            _mapImage.Fingerprint.Width,
            _mapImage.Fingerprint.Height));
        var destination = new SKRect(
            (float)topLeft.X,
            (float)topLeft.Y,
            (float)bottomRight.X,
            (float)bottomRight.Y);
        canvas.DrawImage(
            _mapImage.Image,
            destination,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));

        DrawCalibrationAnchors(canvas);
        DrawMarker(canvas);
        canvas.Restore();
    }

    private void DrawMarker(SKCanvas canvas)
    {
        if (_viewport is null || _markerPosition is not { } position)
        {
            return;
        }

        var viewPosition = _viewport.ImageToView(position);
        var center = new SKPoint((float)viewPosition.X, (float)viewPosition.Y);
        var cursor = NavigationCursorGeometry.Create(
            new PixelPoint(center.X, center.Y),
            _markerDirection);

        using var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(ToSkPoint(cursor.Tip));
        pathBuilder.LineTo(ToSkPoint(cursor.RearLeft));
        pathBuilder.LineTo(ToSkPoint(cursor.Notch));
        pathBuilder.LineTo(ToSkPoint(cursor.RearRight));
        pathBuilder.Close();
        using var path = pathBuilder.Detach();

        using var outlinePaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = NavigationCursorGeometry.OutlineStrokeWidth,
        };
        using var markerPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(238, 50, 66),
            Style = SKPaintStyle.Fill,
        };

        canvas.DrawPath(path, markerPaint);
        canvas.DrawPath(path, outlinePaint);
    }

    private static SKPoint ToSkPoint(PixelPoint point) => new((float)point.X, (float)point.Y);

    private void DrawCalibrationAnchors(SKCanvas canvas)
    {
        if (_viewport is null)
        {
            return;
        }

        foreach (var anchor in _calibrationAnchorOverlay.Anchors)
        {
            var viewPosition = _viewport.ImageToView(anchor.Position);
            var center = new SKPoint((float)viewPosition.X, (float)viewPosition.Y);
            var color = anchor.WillBeReplaced
                ? new SKColor(255, 184, 48)
                : new SKColor(40, 190, 220);
            using var fillPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(24, 24, 24, 220),
                Style = SKPaintStyle.Fill,
            };
            using var outlinePaint = new SKPaint
            {
                IsAntialias = true,
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = anchor.WillBeReplaced ? 4 : 3,
            };
            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = color,
            };
            using var typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);
            using var font = new SKFont(typeface, 16);

            if (anchor.WillBeReplaced)
            {
                canvas.DrawCircle(center, 17, outlinePaint);
            }

            canvas.DrawCircle(center, 12, fillPaint);
            canvas.DrawCircle(center, 12, outlinePaint);
            canvas.DrawText(
                anchor.Number.ToString(CultureInfo.InvariantCulture),
                center.X,
                center.Y + 5,
                SKTextAlign.Center,
                font,
                textPaint);
        }
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_mapImage is null || _viewport is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(Surface);
        var factor = point.Properties.MouseWheelDelta > 0 ? ZoomStep : 1 / ZoomStep;
        var fit = ViewportTransform.Fit(
            new Size2D(_mapImage.Fingerprint.Width, _mapImage.Fingerprint.Height),
            new Size2D(Surface.ActualWidth, Surface.ActualHeight));
        _viewport = _viewport.ZoomAt(
            new PixelPoint(point.Position.X, point.Position.Y),
            factor,
            fit.Scale * MinimumFitScaleFactor,
            fit.Scale * MaximumFitScaleFactor);
        Surface.Invalidate();
        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Surface);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointerPressedAt = point.Position;
        _previousPointerPosition = point.Position;
        if (TryHitCalibrationAnchor(point.Position, out var anchorIndex)
            && !_calibrationAnchorOverlay.Anchors[anchorIndex].WillBeReplaced)
        {
            _pressedCalibrationAnchorIndex = anchorIndex;
            e.Handled = Surface.CapturePointer(e.Pointer);
            if (!e.Handled)
            {
                _pressedCalibrationAnchorIndex = null;
            }

            return;
        }

        if (_viewport is not null && _markerPosition is { } markerPosition)
        {
            var markerView = _viewport.ImageToView(markerPosition);
            if (_markerDragInteraction.TryBegin(
                new PixelPoint(point.Position.X, point.Position.Y),
                markerView))
            {
                e.Handled = Surface.CapturePointer(e.Pointer);
                if (!e.Handled)
                {
                    _markerDragInteraction.Cancel();
                }

                return;
            }
        }

        _isDragging = Surface.CapturePointer(e.Pointer);
        e.Handled = _isDragging;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pressedCalibrationAnchorIndex is not null)
        {
            e.Handled = true;
            return;
        }

        if (_markerDragInteraction.IsDragging)
        {
            var markerViewPosition = e.GetCurrentPoint(Surface).Position;
            if (TryViewToImage(markerViewPosition, out var markerImagePixel))
            {
                _markerPosition = markerImagePixel;
                Surface.Invalidate();
            }

            e.Handled = true;
            return;
        }

        if (!_isDragging || _viewport is null)
        {
            return;
        }

        var position = e.GetCurrentPoint(Surface).Position;
        _viewport = _viewport.Pan(
            position.X - _previousPointerPosition.X,
            position.Y - _previousPointerPosition.Y);
        _previousPointerPosition = position;
        Surface.Invalidate();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pressedCalibrationAnchorIndex is { } anchorIndex)
        {
            var anchorReleasePosition = e.GetCurrentPoint(Surface).Position;
            var anchorDeltaX = anchorReleasePosition.X - _pointerPressedAt.X;
            var anchorDeltaY = anchorReleasePosition.Y - _pointerPressedAt.Y;
            _pressedCalibrationAnchorIndex = null;
            Surface.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
            if ((anchorDeltaX * anchorDeltaX) + (anchorDeltaY * anchorDeltaY)
                <= ClickMovementTolerance * ClickMovementTolerance)
            {
                CalibrationAnchorSelected?.Invoke(
                    this,
                    new CalibrationAnchorSelectedEventArgs(anchorIndex));
            }

            return;
        }

        if (_markerDragInteraction.IsDragging)
        {
            var markerViewPosition = e.GetCurrentPoint(Surface).Position;
            e.Handled = true;
            if (TryViewToImage(markerViewPosition, out var markerImagePixel))
            {
                var completed = _markerDragInteraction.TryRelease(
                    markerImagePixel,
                    () => Surface.ReleasePointerCapture(e.Pointer),
                    out var correctedPixel);
                if (completed)
                {
                    MarkerCorrectionRequested?.Invoke(
                        this,
                        new MarkerCorrectionRequestedEventArgs(correctedPixel));
                }
            }
            else
            {
                _markerDragInteraction.Cancel();
                Surface.ReleasePointerCapture(e.Pointer);
            }

            return;
        }

        if (!_isDragging)
        {
            return;
        }

        var position = e.GetCurrentPoint(Surface).Position;
        _isDragging = false;
        Surface.ReleasePointerCapture(e.Pointer);
        e.Handled = true;

        var deltaX = position.X - _pointerPressedAt.X;
        var deltaY = position.Y - _pointerPressedAt.Y;
        if ((deltaX * deltaX) + (deltaY * deltaY) <= ClickMovementTolerance * ClickMovementTolerance
            && TryViewToImage(position, out var imagePixel))
        {
            ImagePixelClicked?.Invoke(this, new MapImagePixelClickedEventArgs(imagePixel));
        }
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        _pressedCalibrationAnchorIndex = null;
        _markerDragInteraction.Cancel();
    }

    private bool TryHitCalibrationAnchor(Point viewPoint, out int anchorIndex)
    {
        anchorIndex = -1;
        return _viewport is not null
            && _calibrationAnchorOverlay.TryHitTest(
                new PixelPoint(viewPoint.X, viewPoint.Y),
                _viewport.ImageToView,
                out anchorIndex);
    }

    private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewport is null && _mapImage is not null)
        {
            FitToView();
        }
        else
        {
            Surface.Invalidate();
        }
    }
}
