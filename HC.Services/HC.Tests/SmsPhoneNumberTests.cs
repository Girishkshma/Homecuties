using HC.Adapters.Messaging;
using Xunit;

namespace HC.Tests;

/// <summary>
/// How a number typed by a person becomes the E.164 number Twilio requires. Twilio refuses anything
/// else with a 21211 ('The phone number is invalid'), and a refusal is a message that never arrives -
/// so the reading is pinned here, once, for every shape an Indian (or international) customer writes.
/// </summary>
public class SmsPhoneNumberTests
{
    /// <summary>A number with its country code is already what Twilio wants; the plus is kept.</summary>
    [Theory]
    [InlineData("+919845861860", "+919845861860")]
    [InlineData("+91 98458 61860", "+919845861860")]
    [InlineData("+91-98458-61860", "+919845861860")]
    [InlineData("+1 (737) 250-8034", "+17372508034")]
    [InlineData("  +91 98458 61860  ", "+919845861860")]
    public void ANumberWithACountryCodeIsKeptAsItIs(string typed, string expected)
    {
        Assert.True(SmsPhoneNumber.TryNormalize(typed, "91", out var e164));
        Assert.Equal(expected, e164);
    }

    /// <summary>
    /// A bare number is read in the configured country - '9845861860' is an Indian mobile number here -
    /// and the trunk '0' an Indian form puts in front of it is dropped first, because that zero is not
    /// part of the number when it is dialled from outside the country.
    /// </summary>
    [Theory]
    [InlineData("9845861860")]
    [InlineData("098458 61860")]
    [InlineData("98458-61860")]
    public void ABareIndianNumberIsReadInTheDefaultCountry(string typed)
    {
        Assert.True(SmsPhoneNumber.TryNormalize(typed, "91", out var e164));
        Assert.Equal("+919845861860", e164);
    }

    /// <summary>The country a bare number is read in is a setting: '7372508034' is a US number when the
    /// shop is set to '1', and the same digits are not a valid Indian number.</summary>
    [Fact]
    public void TheDefaultCountryIsWhatABareNumberIsReadIn()
    {
        Assert.True(SmsPhoneNumber.TryNormalize("7372508034", "1", out var american));
        Assert.Equal("+17372508034", american);

        // Ten digits with no country code are a number in whichever country is configured - that is the
        // whole point of the setting. '91' in front of a US number is a well-formed but wrong number, so
        // the check that follows is that the code really was used.
        Assert.True(SmsPhoneNumber.TryNormalize("7372508034", "91", out var indian));
        Assert.Equal("+917372508034", indian);
        Assert.NotEqual(american, indian);
    }

    /// <summary>'00' is how the rest of the world writes '+', and the prefix is dialling, not a number.</summary>
    [Fact]
    public void AnInternationalPrefixIsDropped()
    {
        Assert.True(SmsPhoneNumber.TryNormalize("0091 98458 61860", "91", out var e164));
        Assert.Equal("+919845861860", e164);
    }

    /// <summary>
    /// What is not a number is refused rather than guessed at: letters, an extension, a number with no
    /// country code and too few digits, a number that starts with a zero it cannot keep, and more digits
    /// than E.164 allows. A refusal here is one Twilio is never asked to pay for.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a number")]
    [InlineData("9845861860 ext 12")]
    [InlineData("12345")]
    [InlineData("+0123456789")]
    [InlineData("+1234567890123456")]
    [InlineData("+")]
    [InlineData("()-/ .")]
    public void WhatIsNotANumberIsRefused(string? typed)
    {
        Assert.False(SmsPhoneNumber.TryNormalize(typed, "91", out var e164));
        Assert.Equal(string.Empty, e164);
    }

    /// <summary>An unusable country setting falls back to India rather than reading the number wrongly.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("0091")]
    [InlineData("9191")]
    public void AnUnusableCountryCodeFallsBackToIndia(string? configured)
    {
        Assert.Equal("91", SmsPhoneNumber.CountryCode(configured));
    }

    /// <summary>The country code is read as the digits of what was configured - '91', '+91' and ' 91 ' are
    /// the same country, and it is never kept with its plus (the adapter adds that once).</summary>
    [Theory]
    [InlineData("91", "91")]
    [InlineData("+91", "91")]
    [InlineData(" 1 ", "1")]
    [InlineData("+1", "1")]
    public void TheCountryCodeIsTheDigitsOfWhatWasConfigured(string configured, string expected)
    {
        Assert.Equal(expected, SmsPhoneNumber.CountryCode(configured));
    }
}
