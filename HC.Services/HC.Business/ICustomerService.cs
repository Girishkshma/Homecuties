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
    Task<ResultDto> ForgotPasswordAsync(string email);
    Task<ResultDto> ResetPasswordAsync(string token, string newPassword);
}
