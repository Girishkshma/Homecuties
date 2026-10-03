// ============================================================
// CustomerService.Addresses.cs
// Partial class: CustomerService - address book operations
// ============================================================

using HC.Business.Dtos;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Business;

public partial class CustomerService : ICustomerService
{
    /// <summary>Longest values the CustomerAddresses columns accept (see 'HC.Data/HomecutiesDbContext.cs').</summary>
    private const int AddressLineMaxLength = 150;
    private const int AddressCityStateMaxLength = 50;
    private const int AddressZipcodeMaxLength = 10;
    private const int AddressMobileMaxLength = 12;
    private const int AddressEmailMaxLength = 150;

    /// <summary>Shown when the customer leaves the label empty - the column is NOT NULL.</summary>
    private const string DefaultAddressTitle = "Home";

    /// <summary>What an address always gets when the customer does not name a country.</summary>
    private const string DefaultAddressCountry = "India";

    public async Task<List<CustomerAddressDto>> GetAddressesAsync(long customerId)
    {
        return await _context.CustomerAddresses
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.AddressId)
            .Select(a => new CustomerAddressDto
            {
                AddressId = a.AddressId,
                AddressTitle = a.AddressTitle,
                ContactName = a.ContactName,
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2 ?? "",
                City = a.City,
                State = a.State,
                Country = a.Country,
                Zipcode = a.Zipcode,
                MobileNumber = a.MobileNumber,
                EmailId = a.EmailId
            })
            .ToListAsync();
    }

    public async Task<CustomerAddressResultDto> SaveAddressAsync(long customerId, SaveCustomerAddressRequest request)
    {
        var title = (request.AddressTitle ?? "").Trim();
        var contactName = (request.ContactName ?? "").Trim();
        var line1 = (request.AddressLine1 ?? "").Trim();
        var line2 = (request.AddressLine2 ?? "").Trim();
        var city = (request.City ?? "").Trim();
        var state = (request.State ?? "").Trim();
        var country = (request.Country ?? "").Trim();
        var zipcode = (request.Zipcode ?? "").Trim();
        var mobile = (request.MobileNumber ?? "").Trim();
        var email = (request.EmailId ?? "").Trim();

        if (string.IsNullOrEmpty(line1) || string.IsNullOrEmpty(city) ||
            string.IsNullOrEmpty(state) || string.IsNullOrEmpty(zipcode))
        {
            return AddressError("Please fill in the address, city, state and PIN code.");
        }

        // The columns are fixed width, so an over-long value would only fail at the INSERT and the
        // customer would be left with an unexplained error. Say what is wrong instead.
        if (title.Length > CustomerAddressDto.AddressTitleMaxLength)
            return AddressError($"Please keep the label under {CustomerAddressDto.AddressTitleMaxLength} characters.");

        if (line1.Length > AddressLineMaxLength || line2.Length > AddressLineMaxLength)
            return AddressError($"Please keep each address line under {AddressLineMaxLength} characters.");

        if (city.Length > AddressCityStateMaxLength || state.Length > AddressCityStateMaxLength)
            return AddressError($"Please keep the city and state under {AddressCityStateMaxLength} characters.");

        if (zipcode.Length > AddressZipcodeMaxLength)
            return AddressError("Please check the PIN code - it looks too long.");

        if (mobile.Length > AddressMobileMaxLength)
            return AddressError("Please check the phone number - it looks too long.");

        if (email.Length > AddressEmailMaxLength)
            return AddressError("Please check the email address - it looks too long.");

        CustomerAddress address;

        if (request.AddressId > 0)
        {
            // An id from the browser is never trusted on its own: the address has to belong to the
            // caller, so guessing an id can only ever edit your own address book.
            var existing = await _context.CustomerAddresses
                .FirstOrDefaultAsync(a => a.AddressId == request.AddressId && a.CustomerId == customerId);

            if (existing == null)
                return AddressError("We could not find that address in your address book.");

            address = existing;
        }
        else
        {
            address = new CustomerAddress { CustomerId = customerId };
            _context.CustomerAddresses.Add(address);
        }

        address.AddressTitle = string.IsNullOrEmpty(title) ? DefaultAddressTitle : title;
        address.ContactName = contactName;
        address.AddressLine1 = line1;
        address.AddressLine2 = string.IsNullOrEmpty(line2) ? null : line2;
        address.City = city;
        address.State = state;
        address.Country = string.IsNullOrEmpty(country) ? DefaultAddressCountry : country;
        address.Zipcode = zipcode;
        address.MobileNumber = mobile;
        address.EmailId = string.IsNullOrEmpty(email) ? null : email;

        await _context.SaveChangesAsync();

        return new CustomerAddressResultDto
        {
            Result = 1,
            Messages = new[] { request.AddressId > 0 ? "Address updated." : "Address saved." },
            Address = new CustomerAddressDto
            {
                AddressId = address.AddressId,
                AddressTitle = address.AddressTitle,
                ContactName = address.ContactName,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2 ?? "",
                City = address.City,
                State = address.State,
                Country = address.Country,
                Zipcode = address.Zipcode,
                MobileNumber = address.MobileNumber,
                EmailId = address.EmailId
            }
        };
    }

    public async Task<ResultDto> DeleteAddressAsync(long customerId, long addressId)
    {
        var address = await _context.CustomerAddresses
            .FirstOrDefaultAsync(a => a.AddressId == addressId && a.CustomerId == customerId);

        if (address == null)
            return AddressError("We could not find that address in your address book.");

        // Orders point at the address row (Orders.BillingAddressID / ShippingAddressID), so removing
        // one an order uses would break that order - and the foreign key would refuse the DELETE
        // anyway. Editing it stays possible, which is what a customer who moved actually wants.
        var usedByAnOrder = await _context.Orders
            .AnyAsync(o => o.BillingAddressId == addressId || o.ShippingAddressId == addressId);

        if (usedByAnOrder)
        {
            return AddressError("This address is used by one of your orders, so it cannot be removed. You can edit it instead.");
        }

        _context.CustomerAddresses.Remove(address);
        await _context.SaveChangesAsync();

        return new ResultDto { Result = 1, Messages = new[] { "Address removed." } };
    }

    private static CustomerAddressResultDto AddressError(string message)
    {
        return new CustomerAddressResultDto
        {
            Result = 0,
            Messages = new[] { message },
            Address = null
        };
    }
}
