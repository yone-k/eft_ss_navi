using System.Security.Cryptography;
using System.Text;

namespace EftSsNavi.Sharing.Protocol;

public static class RoomCode
{
    public const int Length = 16;

    private const string AllowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string RoomIdPrefix = "eftssnavi-room:";

    public static string Generate()
    {
        return string.Create(Length, (object?)null, static (characters, _) =>
        {
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = AllowedCharacters[RandomNumberGenerator.GetInt32(AllowedCharacters.Length)];
            }
        });
    }

    public static bool IsValid(string? code)
    {
        if (code is null || code.Length != Length)
        {
            return false;
        }

        foreach (var character in code)
        {
            if (!AllowedCharacters.Contains(character, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static string Format(string code)
    {
        if (!IsValid(code))
        {
            throw new ArgumentException("The room code is invalid.", nameof(code));
        }

        return string.Create(Length + 3, code, static (result, value) =>
        {
            var sourceIndex = 0;
            for (var targetIndex = 0; targetIndex < result.Length; targetIndex++)
            {
                if (targetIndex is 4 or 9 or 14)
                {
                    result[targetIndex] = '-';
                }
                else
                {
                    result[targetIndex] = value[sourceIndex++];
                }
            }
        });
    }

    public static bool TryNormalize(string? input, out string code)
    {
        code = string.Empty;
        if (input is null)
        {
            return false;
        }

        Span<char> normalized = stackalloc char[Length];
        var length = 0;
        foreach (var character in input.Trim())
        {
            if (character is '-' or ' ' or '\u3000')
            {
                continue;
            }

            if (length == Length)
            {
                return false;
            }

            normalized[length++] = char.ToUpperInvariant(character);
        }

        if (length != Length)
        {
            return false;
        }

        var candidate = new string(normalized);
        if (!IsValid(candidate))
        {
            return false;
        }

        code = candidate;
        return true;
    }

    public static string DeriveRoomId(string code)
    {
        if (!IsValid(code))
        {
            throw new ArgumentException("The room code is invalid.", nameof(code));
        }

        var input = Encoding.UTF8.GetBytes(RoomIdPrefix + code);
        return Convert.ToHexStringLower(SHA256.HashData(input));
    }
}
