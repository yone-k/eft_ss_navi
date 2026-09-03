using EftSsNavi.Sharing.Session;

namespace EftSsNavi.Sharing.Tests.Session;

public sealed class PartyPositionValidationTests
{
    [Theory]
    [InlineData(double.NaN, 2, 3)]
    [InlineData(1, double.PositiveInfinity, 3)]
    [InlineData(1, 2, double.NegativeInfinity)]
    public void ShouldRejectNonFiniteWorldCoordinate(double x, double y, double z)
    {
        // Given: A party position with a non-finite world coordinate.

        // When: The position model is created.
        var exception = Record.Exception(() => new PartyPosition(
            x, y, z, null, null, DateTimeOffset.Parse("2026-09-03T00:00:00Z"), "Woods"));

        // Then: Invalid coordinates cannot enter session state.
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Theory]
    [InlineData(1.0, null)]
    [InlineData(null, 1.0)]
    public void ShouldRejectIncompleteForwardPair(double? forwardX, double? forwardZ)
    {
        // Given: Only one component of the horizontal direction is available.

        // When: The position model is created.
        var exception = Record.Exception(() => new PartyPosition(
            1, 2, 3, forwardX, forwardZ, DateTimeOffset.Parse("2026-09-03T00:00:00Z"), "Woods"));

        // Then: The direction must be fully present or fully absent.
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void ShouldRejectNonUtcCapturedAt()
    {
        // Given: A captured timestamp with a non-zero offset.

        // When: The position model is created.
        var exception = Record.Exception(() => new PartyPosition(
            1, 2, 3, null, null, DateTimeOffset.Parse("2026-09-03T09:00:00+09:00"), "Woods"));

        // Then: Only UTC timestamps enter shared session state.
        Assert.IsType<ArgumentException>(exception);
    }
}
