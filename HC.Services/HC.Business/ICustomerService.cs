using HC.Business.Dtos;

namespace HC.Business;

public interface ICustomerService
{
    Task<CustomerDto?> GetCustomerAsync(long customerId);
    JwtResponseDto GetCustomerJwt(long customerId, string email, string ipAddress);
    JwtValidationDto ValidateCustomerJwt(string jwt);
    Task<GuestCustomerDto> CreateGuestCustomerAsync();
}
