using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;

namespace HC.Business;

public class UtilitiesService : IUtilitiesService
{
    private readonly HomecutiesDbContext _context;

    public UtilitiesService(HomecutiesDbContext context)
    {
        _context = context;
    }

    public async Task<ResultDto> AddAccessTrackingAsync(string ip, string url)
    {
        var tracking = new AccessTracking
        {
            Ip = ip,
            Url = url,
            AccessOn = DateTime.UtcNow
        };

        _context.AccessTrackings.Add(tracking);
        await _context.SaveChangesAsync();

        return new ResultDto { Result = 1, Messages = new[] { "Access tracked" } };
    }
}
