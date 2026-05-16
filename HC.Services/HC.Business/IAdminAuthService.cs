using HC.Business.Dtos;

namespace HC.Business;

public interface IAdminAuthService
{
    Task<AdminLoginResponse> LoginAsync(string loginId, string password);
    AdminUserDto? ValidateToken(string token);
    AdminUserDto? ValidateToken(string token, string ipAddress);
    string GenerateToken(long userId, string loginId, string ipAddress);
    Task<List<AdminMenuDto>> GetMenusByRoleAsync(short roleId);
    Task<List<AdminMenuDto>> GetAllMenusAsync();
    Task<AdminResultDto> ForgotPasswordAsync(string loginId);
    Task<AdminResultDto> ResetPasswordAsync(string token, string newPassword);
}
