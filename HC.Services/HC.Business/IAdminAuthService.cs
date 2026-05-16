using HC.Business.Dtos;

namespace HC.Business;

public interface IAdminAuthService
{
    Task<AdminLoginResponse> LoginAsync(string loginId, string password);
    AdminUserDto? ValidateToken(string token);
    string GenerateToken(long userId, string loginId, string ipAddress);
    Task<List<AdminMenuDto>> GetMenusByRoleAsync(short roleId);
    Task<List<AdminMenuDto>> GetAllMenusAsync();
}
