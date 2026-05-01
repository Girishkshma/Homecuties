using HC.Business.Dtos;

namespace HC.Business;

public interface IFacebookLoginService
{
    Task<LoginCustomerResponseDto> ValidateTokenAsync(string id, string accessToken);
}
