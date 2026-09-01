using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Tests.Calibration;

public sealed class AffineCalibrationTests
{
    private const double DeterminantThreshold = 1e-9;
    private const double Precision = 1e-10;

    [Fact]
    public void ShouldReproduceAllThreeCorrespondencePointsWhenPointsAreNonCollinear()
    {
        // Given: Three non-collinear world points and their image correspondences.
        CalibrationPoint[] points =
        [
            new(new WorldPoint(1, 2), new PixelPoint(13, 1)),
            new(new WorldPoint(4, 2), new PixelPoint(19, -2)),
            new(new WorldPoint(1, 6), new PixelPoint(17, 13)),
        ];

        // When: An affine calibration is created.
        var created = AffineCalibration.TryCreate(points, out var transform);

        // Then: Every correspondence point is reproduced exactly.
        Assert.True(created);
        foreach (var point in points)
        {
            AssertPixelPoint(point.Pixel, transform.TransformPosition(point.World));
        }
    }

    [Fact]
    public void ShouldIncludeTranslationWhenTransformingPosition()
    {
        // Given: A transform with a non-zero translation.
        var transform = CreateTransform(
            new PixelPoint(10, -20),
            new PixelPoint(12, -20),
            new PixelPoint(10, -17));

        // When: A world position is transformed.
        var actual = transform.TransformPosition(new WorldPoint(4, 5));

        // Then: The translation is included in the result.
        AssertPixelPoint(new PixelPoint(18, -5), actual);
    }

    [Fact]
    public void ShouldExcludeTranslationWhenTransformingDirection()
    {
        // Given: A transform with scale and a non-zero translation.
        var transform = CreateTransform(
            new PixelPoint(10, -20),
            new PixelPoint(12, -20),
            new PixelPoint(10, -17));

        // When: A world direction is transformed.
        var actual = transform.TransformDirection(new WorldPoint(4, 5));

        // Then: Only the linear component is applied.
        AssertPixelPoint(new PixelPoint(8, 15), actual);
    }

    [Fact]
    public void ShouldProjectRotationWhenTransformingCoordinates()
    {
        // Given: A 90-degree rotation calibration.
        var transform = CreateTransform(
            new PixelPoint(0, 0),
            new PixelPoint(0, 1),
            new PixelPoint(-1, 0));

        // When: A coordinate is projected.
        var actual = transform.TransformPosition(new WorldPoint(2, 3));

        // Then: The rotation is reflected in image coordinates.
        AssertPixelPoint(new PixelPoint(-3, 2), actual);
    }

    [Fact]
    public void ShouldProjectReflectionWhenTransformingCoordinates()
    {
        // Given: A reflection across the image Y axis.
        var transform = CreateTransform(
            new PixelPoint(0, 0),
            new PixelPoint(-1, 0),
            new PixelPoint(0, 1));

        // When: A coordinate is projected.
        var actual = transform.TransformPosition(new WorldPoint(2, 3));

        // Then: The reflected coordinate is returned.
        AssertPixelPoint(new PixelPoint(-2, 3), actual);
    }

    [Fact]
    public void ShouldProjectAnisotropicScaleWhenTransformingCoordinates()
    {
        // Given: Different scale factors for world X and Z.
        var transform = CreateTransform(
            new PixelPoint(0, 0),
            new PixelPoint(2, 0),
            new PixelPoint(0, 3));

        // When: A coordinate is projected.
        var actual = transform.TransformPosition(new WorldPoint(4, 5));

        // Then: Each axis uses its own scale factor.
        AssertPixelPoint(new PixelPoint(8, 15), actual);
    }

    [Fact]
    public void ShouldRejectCalibrationWhenWorldPointsContainDuplicates()
    {
        // Given: Duplicate world points with distinct image points.
        CalibrationPoint[] points =
        [
            new(new WorldPoint(0, 0), new PixelPoint(0, 0)),
            new(new WorldPoint(0, 0), new PixelPoint(1, 0)),
            new(new WorldPoint(0, 1), new PixelPoint(0, 1)),
        ];

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points, out _);

        // Then: No calibration is created.
        Assert.False(created);
    }

    [Fact]
    public void ShouldRejectCalibrationWhenWorldPointsAreCollinear()
    {
        // Given: Three collinear world points.
        CalibrationPoint[] points =
        [
            new(new WorldPoint(0, 0), new PixelPoint(0, 0)),
            new(new WorldPoint(1, 1), new PixelPoint(1, 0)),
            new(new WorldPoint(2, 2), new PixelPoint(0, 1)),
        ];

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points, out _);

        // Then: No calibration is created.
        Assert.False(created);
    }

    [Fact]
    public void ShouldRejectCalibrationWhenPixelPointsContainDuplicates()
    {
        // Given: Distinct world points mapped to duplicate image points.
        CalibrationPoint[] points =
        [
            new(new WorldPoint(0, 0), new PixelPoint(0, 0)),
            new(new WorldPoint(1, 0), new PixelPoint(0, 0)),
            new(new WorldPoint(0, 1), new PixelPoint(0, 1)),
        ];

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points, out _);

        // Then: No calibration is created.
        Assert.False(created);
    }

    [Fact]
    public void ShouldRejectCalibrationWhenPixelPointsAreCollinear()
    {
        // Given: Non-collinear world points mapped to collinear image points.
        CalibrationPoint[] points =
        [
            new(new WorldPoint(0, 0), new PixelPoint(0, 0)),
            new(new WorldPoint(1, 0), new PixelPoint(1, 1)),
            new(new WorldPoint(0, 1), new PixelPoint(2, 2)),
        ];

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points, out _);

        // Then: No calibration is created.
        Assert.False(created);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void ShouldRejectCalibrationWhenCorrespondenceCountIsNotThree(int pointCount)
    {
        // Given: A correspondence collection whose count is not three.
        CalibrationPoint[] points =
        [
            new(new WorldPoint(0, 0), new PixelPoint(0, 0)),
            new(new WorldPoint(1, 0), new PixelPoint(1, 0)),
            new(new WorldPoint(0, 1), new PixelPoint(0, 1)),
            new(new WorldPoint(1, 1), new PixelPoint(1, 1)),
        ];

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points[..pointCount], out _);

        // Then: No calibration is created.
        Assert.False(created);
    }

    public static TheoryData<CalibrationPoint[]> NonFiniteCalibrationPoints => new()
    {
        CreatePointsWithWorldX(double.NaN),
        CreatePointsWithWorldZ(double.PositiveInfinity),
        CreatePointsWithPixelX(double.NegativeInfinity),
        CreatePointsWithPixelY(double.NaN),
    };

    [Theory]
    [MemberData(nameof(NonFiniteCalibrationPoints))]
    public void ShouldRejectCalibrationWhenAnyCoordinateIsNotFinite(CalibrationPoint[] points)
    {
        // Given: Calibration points containing one non-finite coordinate.

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points, out _);

        // Then: No calibration is created.
        Assert.False(created);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ShouldRejectCalibrationWhenAbsoluteLinearDeterminantIsBelowThreshold(int sign)
    {
        // Given: A transform whose determinant magnitude is below the threshold.
        var determinant = sign * Math.BitDecrement(DeterminantThreshold);
        var points = CreateScaleCalibration(determinant);

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points, out _);

        // Then: No calibration is created.
        Assert.False(created);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ShouldRejectCalibrationWhenAbsoluteLinearDeterminantEqualsThreshold(int sign)
    {
        // Given: A transform whose determinant magnitude equals the threshold.
        var points = CreateScaleCalibration(sign * DeterminantThreshold);

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points, out _);

        // Then: No calibration is created.
        Assert.False(created);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ShouldAcceptCalibrationWhenAbsoluteLinearDeterminantExceedsThreshold(int sign)
    {
        // Given: A transform whose determinant magnitude exceeds the threshold.
        var determinant = sign * Math.BitIncrement(DeterminantThreshold);
        var points = CreateScaleCalibration(determinant);

        // When: Calibration is attempted.
        var created = AffineCalibration.TryCreate(points, out _);

        // Then: A calibration is created.
        Assert.True(created);
    }

    private static AffineTransform2D CreateTransform(
        PixelPoint origin,
        PixelPoint worldX,
        PixelPoint worldZ)
    {
        CalibrationPoint[] points =
        [
            new(new WorldPoint(0, 0), origin),
            new(new WorldPoint(1, 0), worldX),
            new(new WorldPoint(0, 1), worldZ),
        ];

        Assert.True(AffineCalibration.TryCreate(points, out var transform));
        return transform;
    }

    private static CalibrationPoint[] CreateScaleCalibration(double zScale) =>
    [
        new(new WorldPoint(0, 0), new PixelPoint(0, 0)),
        new(new WorldPoint(1, 0), new PixelPoint(1, 0)),
        new(new WorldPoint(0, 1), new PixelPoint(0, zScale)),
    ];

    private static CalibrationPoint[] CreatePointsWithWorldX(double value) =>
    [
        new(new WorldPoint(value, 0), new PixelPoint(0, 0)),
        new(new WorldPoint(1, 0), new PixelPoint(1, 0)),
        new(new WorldPoint(0, 1), new PixelPoint(0, 1)),
    ];

    private static CalibrationPoint[] CreatePointsWithWorldZ(double value) =>
    [
        new(new WorldPoint(0, value), new PixelPoint(0, 0)),
        new(new WorldPoint(1, 0), new PixelPoint(1, 0)),
        new(new WorldPoint(0, 1), new PixelPoint(0, 1)),
    ];

    private static CalibrationPoint[] CreatePointsWithPixelX(double value) =>
    [
        new(new WorldPoint(0, 0), new PixelPoint(value, 0)),
        new(new WorldPoint(1, 0), new PixelPoint(1, 0)),
        new(new WorldPoint(0, 1), new PixelPoint(0, 1)),
    ];

    private static CalibrationPoint[] CreatePointsWithPixelY(double value) =>
    [
        new(new WorldPoint(0, 0), new PixelPoint(0, value)),
        new(new WorldPoint(1, 0), new PixelPoint(1, 0)),
        new(new WorldPoint(0, 1), new PixelPoint(0, 1)),
    ];

    private static void AssertPixelPoint(PixelPoint expected, PixelPoint actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
    }
}
