// ============================================================
// AdminDashboardService.Customers.cs
// Partial class: AdminDashboardService - Customers operations
// ============================================================

using System.Security.Cryptography;
using System.Text;
using HC.Business.Dtos;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HC.Business;

public partial class AdminDashboardService : IAdminDashboardService
{
    public async Task<List<AdminCustomerListDto>> GetCustomersAsync()
    {
        return await _context.Customers
            .Include(c => c.CustomerStatus)
            .OrderByDescending(c => c.CreatedOn)
            .Select(c => new AdminCustomerListDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                EmailId = c.EmailId,
                MobileNumber = c.MobileNumber,
                MobileVerified = c.MobileVerified,
                EmailVerified = c.EmailVerfied,
                CreatedOn = c.CreatedOn,
                ModifiedOn = c.ModifiedOn,
                CustomerStatusId = c.CustomerStatusId,
                Status = c.CustomerStatus.CustomerStatus1
            })
            .ToListAsync();
    }

    public async Task<AdminCustomerDetailDto?> GetCustomerDetailAsync(long customerId)
    {
        return await _context.Customers
            .Include(c => c.CustomerStatus)
            .Include(c => c.CustomerAddresses)
            .Include(c => c.Orders)
            .Where(c => c.CustomerId == customerId)
            .Select(c => new AdminCustomerDetailDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                EmailId = c.EmailId,
                MobileNumber = c.MobileNumber,
                MobileVerified = c.MobileVerified,
                EmailVerified = c.EmailVerfied,
                CreatedOn = c.CreatedOn,
                ModifiedOn = c.ModifiedOn,
                CustomerStatusId = c.CustomerStatusId,
                Status = c.CustomerStatus.CustomerStatus1,
                Addresses = c.CustomerAddresses.Select(a => new AdminCustomerAddressDto
                {
                    AddressId = a.AddressId,
                    AddressTitle = a.AddressTitle,
                    ContactName = a.ContactName,
                    AddressLine1 = a.AddressLine1,
                    AddressLine2 = a.AddressLine2,
                    City = a.City,
                    State = a.State,
                    Country = a.Country,
                    Zipcode = a.Zipcode,
                    MobileNumber = a.MobileNumber
                }).ToList(),
                OrderCount = c.Orders.Count,
                TotalSpent = c.Orders.SelectMany(o => o.OrderItems).Sum(oi => (decimal?)oi.UnitPrice) ?? 0
            })
            .FirstOrDefaultAsync();
    }

    public async Task<AdminResultDto> UpdateCustomerStatusAsync(long customerId, short customerStatusId)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            return new AdminResultDto
            {
                Result = 0,
                Messages = new[] { "Customer not found." }
            };
        }

        var statusExists = await _context.CustomerStatuses.AnyAsync(cs => cs.CustomerStatusId == customerStatusId);
        if (!statusExists)
        {
            return new AdminResultDto
            {
                Result = 0,
                Messages = new[] { "Invalid customer status." }
            };
        }

        customer.CustomerStatusId = customerStatusId;
        customer.ModifiedOn = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new AdminResultDto
        {
            Result = 1,
            Messages = new[] { "Customer status updated successfully." }
        };
    }

    public async Task<List<AdminCustomerListDto>> SearchCustomersAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetCustomersAsync();

        var term = searchTerm.Trim().ToLower();
        return await _context.Customers
            .Include(c => c.CustomerStatus)
            .Where(c => c.FirstName.ToLower().Contains(term) ||
                        (c.LastName != null && c.LastName.ToLower().Contains(term)) ||
                        c.EmailId.ToLower().Contains(term) ||
                        (c.MobileNumber != null && c.MobileNumber.Contains(term)))
            .OrderByDescending(c => c.CreatedOn)
            .Select(c => new AdminCustomerListDto
            {
                CustomerId = c.CustomerId,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                LastName = c.LastName,
                EmailId = c.EmailId,
                MobileNumber = c.MobileNumber,
                MobileVerified = c.MobileVerified,
                EmailVerified = c.EmailVerfied,
                CreatedOn = c.CreatedOn,
                ModifiedOn = c.ModifiedOn,
                CustomerStatusId = c.CustomerStatusId,
                Status = c.CustomerStatus.CustomerStatus1
            })
            .ToListAsync();
    }


}
