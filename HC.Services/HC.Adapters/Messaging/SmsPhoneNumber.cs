using System.Text;

namespace HC.Adapters.Messaging;

/// <summary>
/// Reads a mobile number as a person typed it and gives back the E.164 form Twilio requires
/// ('+919845861860'). Twilio refuses anything else with a 21211, so the number is read here, once,
/// instead of on every caller's screen.
///
/// What is accepted, deliberately:
///
///  * A number with a country code, with or without the '+': '+91 98458 61860',
///    '+1 (737) 250-8034', '0091 98458 61860'. International dialling prefixes ('00') are dropped.
///  * A bare 10-digit number: '9845861860' is read in the configured default country
///    (<see cref="DefaultCountryCode"/>, '91' unless set), giving '+919845861860'. A leading trunk
///    '0' ('09845861860') is dropped first, because that is how the number is written on an Indian
///    form.
///  * Separators a person types - spaces, dashes, parentheses, dots, slashes - are ignored. Letters
///    and anything else are refused rather than guessed at.
///
/// The result is never longer than E.164 allows (15 digits) and never shorter than 8, which is the
/// shortest number any Twilio destination uses: a value outside that is a typo, not a number.
/// </summary>
public static class SmsPhoneNumber
{
    /// <summary>The country a bare number is read in when 'Twilio:DefaultCountryCode' is not set.</summary>
    public const string DefaultCountryCode = "91";

    /// <summary>The shortest E.164 number that is worth dialling (and that Twilio accepts).</summary>
    private const int MinDigits = 8;

    /// <summary>The longest number E.164 allows, country code included.</summary>
    private const int MaxDigits = 15;

    /// <summary>
    /// Reads <paramref name="number"/> into <paramref name="e164"/>, or answers false when it cannot
    /// be a mobile number at all. <paramref name="defaultCountryCode"/> is the country a bare number
    /// is read in; an unusable value falls back to <see cref="DefaultCountryCode"/>.
    /// </summary>
    public static bool TryNormalize(string? number, string? defaultCountryCode, out string e164)
    {
        e164 = string.Empty;

        var text = (number ?? string.Empty).Trim();

        if (text.Length == 0)
            return false;

        // The leading '+' is the one character that is not a digit or a separator: it says the country
        // code is already there. It is read first and taken off, so the loop below only has to skip the
        // separators a person types. A '+' anywhere else is not a number.
        var hasCountryCode = text.StartsWith('+');

        if (hasCountryCode)
            text = text[1..];

        var digits = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
            else if (character is not (' ' or '-' or '(' or ')' or '.' or '/'))
            {
                // A letter, a hash, an extension: not a number this adapter can trust to text.
                return false;
            }
        }

        var national = digits.ToString();

        if (national.Length == 0)
            return false;

        if (hasCountryCode)
            return IsDialable(national, out e164);

        // '00' is how the rest of the world writes '+'.
        if (national.StartsWith("00", StringComparison.Ordinal))
            return IsDialable(national[2..], out e164);

        // The trunk '0' of a domestic number ('09845861860') is not part of the number when dialled
        // from another country, and Twilio always dials from outside.
        if (national.Length == 11 && national[0] == '0')
            national = national[1..];

        if (national.Length == 10)
            national = CountryCode(defaultCountryCode) + national;

        return IsDialable(national, out e164);
    }

    /// <summary>
    /// The country code a bare number is read in: the configured digits when they are usable (one to
    /// three of them, e.g. '91' or '+1'), else <see cref="DefaultCountryCode"/>.
    /// </summary>
    public static string CountryCode(string? defaultCountryCode)
    {
        var digits = new string((defaultCountryCode ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

        return digits.Length is > 0 and <= 3 ? digits : DefaultCountryCode;
    }

    /// <summary>
    /// Whether these digits are a number E.164 can carry, and the '+'-prefixed value when they are.
    /// A number may not start with 0: there is no country code that does.
    /// </summary>
    private static bool IsDialable(string digits, out string e164)
    {
        e164 = string.Empty;

        if (digits.Length is < MinDigits or > MaxDigits || digits[0] == '0')
            return false;

        e164 = "+" + digits;
        return true;
    }
}
