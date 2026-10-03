using HC.Business.Dtos;

namespace HC.Business;

/// <summary>
/// Turns a PIN code into the areas it covers with their city and state, so the address forms in
/// checkout and in 'My Profile' can fill those in instead of asking the customer to type them.
/// </summary>
public interface IPincodeService
{
    /// <summary>
    /// Looks <paramref name="pincode"/> up in the public India Post directory.
    ///
    /// Never throws: an unknown PIN and an unreachable directory both come back as a failed
    /// <see cref="PincodeLookupResultDto"/> carrying the reason, so the caller can carry on and let the
    /// customer type the address.
    /// </summary>
    Task<PincodeLookupResultDto> LookupAsync(string? pincode, CancellationToken cancellationToken = default);
}
