using System.Globalization;
using System.Numerics;
using EftSsMap.Core.Observations;

namespace EftSsMap.Core.Tests.Observations;

public sealed class ScreenshotFileNameParserTests
{
    public static TheoryData<string, DateTime, Vector3, Quaternion, string, int?> ActualSampleFileNames => new()
    {
        {
            "2026-09-01[20-19]_516.22, -19.36, 261.96_0.02673, 0.51521, -0.01609, 0.85650_10.33 (0).png",
            new DateTime(2026, 9, 1, 20, 19, 0),
            new Vector3(516.22f, -19.36f, 261.96f),
            new Quaternion(0.02673f, 0.51521f, -0.01609f, 0.85650f),
            "10.33",
            0
        },
        {
            "2026-09-01[20-20]_515.87, -19.36, 261.95_-0.00414, 0.92787, -0.01030, -0.37275_10.37 (0).png",
            new DateTime(2026, 9, 1, 20, 20, 0),
            new Vector3(515.87f, -19.36f, 261.95f),
            new Quaternion(-0.00414f, 0.92787f, -0.01030f, -0.37275f),
            "10.37",
            0
        },
        {
            "2026-09-01[20-20]_533.24, -20.52, 275.30_0.00678, 0.87005, 0.01198, -0.49277_10.41 (0).png",
            new DateTime(2026, 9, 1, 20, 20, 0),
            new Vector3(533.24f, -20.52f, 275.30f),
            new Quaternion(0.00678f, 0.87005f, 0.01198f, -0.49277f),
            "10.41",
            0
        },
        {
            "2026-09-01[20-20]_533.56, -20.52, 275.26_0.00394, 0.47432, -0.00204, 0.88034_10.41 (0).png",
            new DateTime(2026, 9, 1, 20, 20, 0),
            new Vector3(533.56f, -20.52f, 275.26f),
            new Quaternion(0.00394f, 0.47432f, -0.00204f, 0.88034f),
            "10.41",
            0
        },
    };

    [Theory]
    [MemberData(nameof(ActualSampleFileNames))]
    public void Should_parse_every_observation_field_from_actual_sample_file_name(
        string fileName,
        DateTime expectedCapturedAt,
        Vector3 expectedPosition,
        Quaternion expectedRotation,
        string expectedGameTime,
        int? expectedSequenceNumber)
    {
        // Given: An actual EFT screenshot file name from the four supplied samples.

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.NotNull(observation);
        Assert.Equal(expectedCapturedAt, observation.CapturedAt);
        Assert.Equal(expectedPosition, observation.Position);
        Assert.Equal(expectedRotation, observation.Rotation);
        Assert.Equal(expectedGameTime, observation.GameTime);
        Assert.Equal(expectedSequenceNumber, observation.SequenceNumber);
    }

    [Fact]
    public void Should_parse_capture_date_and_time_when_file_name_is_valid()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_533.56, -20.52, 275.26_0.00394, 0.47432, -0.00204, 0.88034_10.41 (0).png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 9, 1, 20, 20, 0), observation!.CapturedAt);
    }

    [Fact]
    public void Should_parse_world_position_when_file_name_contains_signed_coordinates()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_533.56, -20.52, 275.26_0.00394, 0.47432, -0.00204, 0.88034_10.41 (0).png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal(new Vector3(533.56f, -20.52f, 275.26f), observation!.Position);
    }

    [Fact]
    public void Should_parse_quaternion_when_file_name_is_valid()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_533.56, -20.52, 275.26_0.00394, 0.47432, -0.00204, 0.88034_10.41 (0).png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal(new Quaternion(0.00394f, 0.47432f, -0.00204f, 0.88034f), observation!.Rotation);
    }

    [Fact]
    public void Should_parse_game_time_when_file_name_is_valid()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_533.56, -20.52, 275.26_0.00394, 0.47432, -0.00204, 0.88034_10.41 (0).png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal("10.41", observation!.GameTime);
    }

    [Fact]
    public void Should_parse_optional_sequence_number_when_file_name_contains_it()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_1, 2, 3_0, 0, 0, 1_10.41 (0).png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal(0, observation!.SequenceNumber);
    }

    [Fact]
    public void Should_accept_file_name_when_sequence_number_is_absent()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_1, 2, 3_0, 0, 0, 1_10.41.png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Null(observation!.SequenceNumber);
    }

    [Fact]
    public void Should_accept_file_name_when_sequence_number_has_multiple_digits()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_1, 2, 3_0, 0, 0, 1_10.41 (123).png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal(123, observation!.SequenceNumber);
    }

    [Fact]
    public void Should_accept_file_name_when_png_extension_is_uppercase()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_1, 2, 3_0, 0, 0, 1_10.41.PNG";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out _);

        // Then
        Assert.True(parsed);
    }

    [Fact]
    public void Should_accept_file_name_when_all_numeric_components_are_negative()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_-1.25, -2.5, -3.75_-0.1, -0.2, -0.3, -0.4_-10.41.png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal(new Vector3(-1.25f, -2.5f, -3.75f), observation!.Position);
    }

    [Fact]
    public void Should_accept_file_name_when_capture_time_is_at_valid_date_boundary()
    {
        // Given
        const string fileName = "2024-02-29[23-59]_1, 2, 3_0, 0, 0, 1_10.41.png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal(new DateTime(2024, 2, 29, 23, 59, 0), observation!.CapturedAt);
    }

    [Theory]
    [InlineData("screenshot.png")]
    [InlineData("2026-09-01[20-20]_1, 2, 3_0, 0, 0, 1_10.41.jpg")]
    public void Should_reject_file_name_when_whole_name_does_not_match(string fileName)
    {
        // Given: a file name outside the EFT PNG grammar

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.False(parsed);
        Assert.Null(observation);
    }

    [Theory]
    [InlineData("prefix-2026-09-01[20-20]_1, 2, 3_0, 0, 0, 1_10.41.png")]
    [InlineData("2026-09-01[20-20]_1, 2, 3_0, 0, 0, 1_10.41.png.bak")]
    public void Should_reject_file_name_when_extra_characters_surround_valid_shape(string fileName)
    {
        // Given: an otherwise valid shape with extra characters

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.False(parsed);
        Assert.Null(observation);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Should_reject_file_name_when_position_is_not_finite(string invalidNumber)
    {
        // Given
        var fileName = $"2026-09-01[20-20]_{invalidNumber}, 2, 3_0, 0, 0, 1_10.41.png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.False(parsed);
        Assert.Null(observation);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Should_reject_file_name_when_quaternion_is_not_finite(string invalidNumber)
    {
        // Given
        var fileName = $"2026-09-01[20-20]_1, 2, 3_{invalidNumber}, 0, 0, 1_10.41.png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.False(parsed);
        Assert.Null(observation);
    }

    [Fact]
    public void Should_reject_file_name_when_number_is_malformed()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_1.2.3, 2, 3_0, 0, 0, 1_10.41.png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.False(parsed);
        Assert.Null(observation);
    }

    [Fact]
    public void Should_reject_file_name_when_quaternion_has_zero_length()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_1, 2, 3_0, 0, 0, 0_10.41.png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.False(parsed);
        Assert.Null(observation);
    }

    [Fact]
    public void Should_parse_numbers_using_invariant_culture_when_current_culture_uses_decimal_comma()
    {
        // Given
        const string fileName = "2026-09-01[20-20]_1.25, 2.5, 3.75_0, 0, 0, 1_10.41.png";
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            // When
            var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

            // Then
            Assert.True(parsed);
            Assert.Equal(new Vector3(1.25f, 2.5f, 3.75f), observation!.Position);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Should_keep_position_observation_when_horizontal_forward_has_zero_length()
    {
        // Given: +90 degrees around X points Unity +Z vertically downward
        const string fileName = "2026-09-01[20-20]_1, 2, 3_0.70710678, 0, 0, 0.70710678_10.41.png";

        // When
        var parsed = ScreenshotFileNameParser.TryParse(fileName, out var observation);

        // Then
        Assert.True(parsed);
        Assert.Equal(new Vector3(1, 2, 3), observation!.Position);
        Assert.Null(observation.HorizontalForward);
    }
}

public sealed class QuaternionDirectionCalculatorTests
{
    [Fact]
    public void Should_return_unit_positive_z_when_quaternion_is_identity()
    {
        // Given
        var rotation = Quaternion.Identity;

        // When
        var calculated = QuaternionDirectionCalculator.TryCalculateHorizontalForward(rotation, out var direction);

        // Then
        Assert.True(calculated);
        AssertVectorApproximatelyEqual(new Vector2(0, 1), direction);
    }

    [Fact]
    public void Should_return_unit_positive_x_when_rotation_around_y_is_positive_ninety_degrees()
    {
        // Given
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

        // When
        var calculated = QuaternionDirectionCalculator.TryCalculateHorizontalForward(rotation, out var direction);

        // Then
        Assert.True(calculated);
        AssertVectorApproximatelyEqual(new Vector2(1, 0), direction);
    }

    [Fact]
    public void Should_return_unit_negative_x_when_rotation_around_y_is_negative_ninety_degrees()
    {
        // Given
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2);

        // When
        var calculated = QuaternionDirectionCalculator.TryCalculateHorizontalForward(rotation, out var direction);

        // Then
        Assert.True(calculated);
        AssertVectorApproximatelyEqual(new Vector2(-1, 0), direction);
    }

    [Fact]
    public void Should_normalize_quaternion_and_horizontal_direction_when_input_is_not_normalized()
    {
        // Given: a scaled +90 degree rotation around Y
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2) * 7;

        // When
        var calculated = QuaternionDirectionCalculator.TryCalculateHorizontalForward(rotation, out var direction);

        // Then
        Assert.True(calculated);
        AssertVectorApproximatelyEqual(new Vector2(1, 0), direction);
        Assert.InRange(direction.Length(), 0.99999f, 1.00001f);
    }

    [Fact]
    public void Should_return_false_when_horizontal_forward_has_zero_length()
    {
        // Given: +90 degrees around X points Unity +Z vertically downward
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);

        // When
        var calculated = QuaternionDirectionCalculator.TryCalculateHorizontalForward(rotation, out _);

        // Then
        Assert.False(calculated);
    }

    [Fact]
    public void Should_return_direction_when_horizontal_forward_is_nonzero_below_previous_tolerance()
    {
        // Given: A rotation whose horizontal forward is non-zero but shorter than 1e-6.
        var rotation = Quaternion.CreateFromAxisAngle(
            Vector3.UnitX,
            (MathF.PI / 2) - 5e-7f);

        // When
        var calculated = QuaternionDirectionCalculator.TryCalculateHorizontalForward(rotation, out var direction);

        // Then
        Assert.True(calculated);
        AssertVectorApproximatelyEqual(new Vector2(0, 1), direction);
    }

    [Fact]
    public void Should_normalize_finite_nonzero_quaternion_when_largest_component_is_subnormal()
    {
        // Given: A finite, non-zero quaternion whose reciprocal would overflow a float.
        var rotation = new Quaternion(float.Epsilon, 0, 0, 0);

        // When
        var calculated = QuaternionDirectionCalculator.TryCalculateHorizontalForward(rotation, out var direction);

        // Then
        Assert.True(calculated);
        AssertVectorApproximatelyEqual(new Vector2(0, -1), direction);
    }

    private static void AssertVectorApproximatelyEqual(Vector2 expected, Vector2 actual)
    {
        const float tolerance = 0.00001f;
        Assert.InRange(actual.X, expected.X - tolerance, expected.X + tolerance);
        Assert.InRange(actual.Y, expected.Y - tolerance, expected.Y + tolerance);
    }
}
