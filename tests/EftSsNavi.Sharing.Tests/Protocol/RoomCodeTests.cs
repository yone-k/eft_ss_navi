using EftSsNavi.Sharing.Protocol;

namespace EftSsNavi.Sharing.Tests.Protocol;

public sealed class RoomCodeTests
{
    private const string AllowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    [Fact]
    public void ShouldGenerateSixteenCharacterCodeFromAllowedAlphabet()
    {
        // Given: The room-code generator.

        // When: Multiple room codes are generated.
        var codes = Enumerable.Range(0, 128).Select(_ => RoomCode.Generate()).ToArray();

        // Then: Every code is eight characters and uses only the unambiguous alphabet.
        Assert.All(codes, code =>
        {
            Assert.Equal(16, code.Length);
            Assert.All(code, character => Assert.Contains(character, AllowedCharacters));
        });
    }

    [Theory]
    [InlineData("ABCDEFGHJKLMNPQR")]
    [InlineData("2345678923456789")]
    [InlineData("A2B3C4D5E6F7G8H9")]
    public void ShouldAcceptCodeWhenExactlySixteenAllowedCharacters(string code)
    {
        // Given: An eight-character code made only from the allowed alphabet.

        // When: The code is validated.
        var isValid = RoomCode.IsValid(code);

        // Then: The code is accepted.
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCDEFGHJKLMNPQ")]
    [InlineData("ABCDEFGHJKLMNPQRS")]
    [InlineData("ABCDEFGHJKLMNPQ0")]
    [InlineData("ABCDEFGHJKLMNPQO")]
    [InlineData("ABCDEFGHJKLMNPQ1")]
    [InlineData("ABCDEFGHJKLMNPQI")]
    [InlineData("abcdefghjklmnpqr")]
    [InlineData("ABCDEFGH JKLMNPQ")]
    public void ShouldRejectCodeWhenFormatIsInvalid(string? code)
    {
        // Given: A code that violates the exact room-code format.

        // When: The code is validated.
        var isValid = RoomCode.IsValid(code);

        // Then: The code is rejected without normalization.
        Assert.False(isValid);
    }

    [Fact]
    public void ShouldDeriveExpectedLowercaseSha256RoomId()
    {
        // Given: A known valid room code.
        const string code = "ABCDEFGHJKLMNPQR";

        // When: Its broker-safe room ID is derived.
        var roomId = RoomCode.DeriveRoomId(code);

        // Then: It is SHA256("eftssnavi-room:" + code) as lowercase hexadecimal.
        Assert.Equal("30bec0ac44f7f6c3bf5eadfeeb023e9265fbd4c497e86cc55a4beba6ef53f38b", roomId);
    }

    [Fact]
    public void ShouldFormatCodeInFourCharacterGroups()
    {
        // Given: A normalized room code.
        const string code = "ABCDEFGHJKLMNPQR";

        // When: The code is formatted for display.
        var formatted = RoomCode.Format(code);

        // Then: Four groups are separated by hyphens.
        Assert.Equal("ABCD-EFGH-JKLM-NPQR", formatted);
    }

    [Theory]
    [InlineData("  abcd-efgh-jklm-npqr  ")]
    [InlineData("ABCD EFGH JKLM NPQR")]
    [InlineData("ABCD　EFGH　JKLM　NPQR")]
    [InlineData("A B-C D E F-G H J K-L M N P-Q R")]
    public void ShouldNormalizeHumanEnteredCode(string input)
    {
        // Given: A valid code containing display separators and lowercase letters.

        // When: The input is normalized.
        var accepted = RoomCode.TryNormalize(input, out var code);

        // Then: The canonical sixteen-character code is returned.
        Assert.True(accepted);
        Assert.Equal("ABCDEFGHJKLMNPQR", code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCD-EFGH-JKLM-NPQ")]
    [InlineData("ABCD-EFGH-JKLM-NPQ0")]
    [InlineData("ABCD_EFGH_JKLM_NPQR")]
    public void ShouldRejectInvalidNormalizedInput(string? input)
    {
        // Given: Input that cannot become a valid room code.

        // When: Normalization is attempted.
        var accepted = RoomCode.TryNormalize(input, out var code);

        // Then: It fails without returning a partial code.
        Assert.False(accepted);
        Assert.Equal(string.Empty, code);
    }
}
