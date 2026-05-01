using HC.Business.Dtos;

namespace HC.Business;

public interface IGoogleLoginService
{
    Task<LoginCustomerResponseDto> ValidateTokenAsync(string idToken);
}
