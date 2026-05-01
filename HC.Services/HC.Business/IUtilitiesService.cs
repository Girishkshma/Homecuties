using HC.Business.Dtos;

namespace HC.Business;

public interface IUtilitiesService
{
    Task<ResultDto> AddAccessTrackingAsync(string ip, string url);
}
