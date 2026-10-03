using HC.Business.Dtos;

namespace HC.Business;

public interface ICustomerService
{
    Task<CustomerDto?> GetCustomerAsync(long customerId);
    JwtResponseDto GetCustomerJwt(long customerId, string email, string ipAddress);
    JwtValidationDto ValidateCustomerJwt(string jwt);
    Task<GuestCustomerDto> CreateGuestCustomerAsync();
    Task<LoginCustomerResponseDto> CreateCustomerAsync(string firstName, string lastName, string email, string password);
    Task<LoginCustomerResponseDto> LoginAsync(string email, string password);
    Task<LoginCustomerResponseDto> SignInExternalAsync(string email, string firstName, string lastName, string? externalPayload);
    Task<ResultDto> SetPasswordAsync(long customerId, string? currentPassword, string newPassword);

    /// <summary>The customer's address book, newest first.</summary>
    Task<List<CustomerAddressDto>> GetAddressesAsync(long customerId);

    /// <summary>Adds or edits one address book entry. The address is always matched against the customer.</summary>
    Task<CustomerAddressResultDto> SaveAddressAsync(long customerId, SaveCustomerAddressRequest request);

    /// <summary>Removes one address book entry - refused while an order still points at it.</summary>
    Task<ResultDto> DeleteAddressAsync(long customerId, long addressId);

    Task<ResultDto> ForgotPasswordAsync(string email);
    Task<ResultDto> ResetPasswordAsync(string token, string newPassword);
}
