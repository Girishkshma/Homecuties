// ============================================================
// CustomerService.Auth.cs
// Partial class: CustomerService - Auth operations
// ============================================================

using HC.Business.Dtos;
using HC.Business.Security;
using HC.Data;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace HC.Business;

public partial class CustomerService : ICustomerService
{
    public JwtResponseDto GetCustomerJwt(long customerId, string email, string ipAddress)
    {
        var payload = JsonSerializer.Serialize(new
        {
            CustomerID = customerId,
            Email = email,
            IP = ipAddress
        });

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        return new JwtResponseDto { Token = token, ExpiresOn = DateTime.UtcNow.AddDays(7) };
    }

    public JwtValidationDto ValidateCustomerJwt(string jwt)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(jwt));
            return new JwtValidationDto { Valid = true, Data = json };
        }
        catch
        {
            return new JwtValidationDto { Valid = false };
        }
    }

    public async Task<LoginCustomerResponseDto> CreateCustomerAsync(string firstName, string lastName, string email, string password)
    {
        firstName = (firstName ?? "").Trim();
        lastName = (lastName ?? "").Trim();
        email = (email ?? "").Trim();
        password ??= "";

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(email))
            return Error("First name and email are required.");

        if (password.Length < 6)
            return Error("Password must be at least 6 characters long.");

        var existingCustomer = await _context.Customers
            .FirstOrDefaultAsync(c => c.EmailId == email);

        if (existingCustomer != null)
        {
            // A row without a password is a placeholder created by the guest checkout or social
            // login flows. Upgrade it into a real account (same CustomerID, so any existing
            // orders/addresses stay linked) instead of refusing the registration.
            if (!string.IsNullOrEmpty(existingCustomer.Password))
                return Error("An account with this email already exists.");

            existingCustomer.FirstName = firstName;
            existingCustomer.LastName = lastName;
            existingCustomer.Password = CustomerPasswordHasher.Hash(password);
            existingCustomer.CustomerStatusId = CustomerStatusActive;
            existingCustomer.EmailVerfied = false;
            existingCustomer.ModifiedOn = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Success(existingCustomer, "Account created successfully.");
        }

        var customer = new Customer
        {
            FirstName = firstName,
            LastName = lastName,
            EmailId = email,
            Password = CustomerPasswordHasher.Hash(password),
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = DateTime.UtcNow,
            CustomerStatusId = CustomerStatusActive,
            EmailVerfied = false
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return Success(customer, "Account created successfully.");
    }

    public async Task<LoginCustomerResponseDto> LoginAsync(string email, string password)
    {
        email = (email ?? "").Trim();
        password ??= "";

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.EmailId == email);

        if (customer == null)
            return Error("Invalid email or password.");

        if (!CustomerPasswordHasher.Verify(customer.Password, password, out var needsUpgrade))
            return Error("Invalid email or password.");

        // Status is checked only after the password matches, so we never leak account state.
        if (customer.CustomerStatusId != CustomerStatusActive)
        {
            return customer.CustomerStatusId switch
            {
                2 => Error("Your account has been deactivated. Please contact support."),
                3 => Error("Your account has been suspended. Please contact support."),
                _ => Error("Your account is not active. Please contact support.")
            };
        }

        // Legacy plain-text password matched - store a real hash from now on.
        if (needsUpgrade)
        {
            customer.Password = CustomerPasswordHasher.Hash(password);
            customer.ModifiedOn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return Success(customer, "Login successful.");
    }

    /// <summary>
    /// Signs a customer in through an external identity provider (e.g. Google). The provider has
    /// already proven the e-mail address, so no password is checked: an existing account is reused
    /// and a placeholder row (created by guest checkout / a social login) is filled in. New
    /// customers are created active, and the same signed token as a password login is issued.
    /// </summary>
    public async Task<LoginCustomerResponseDto> SignInExternalAsync(string email, string firstName, string lastName, string? externalPayload)
    {
        email = (email ?? "").Trim();
        firstName = (firstName ?? "").Trim();
        lastName = (lastName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email))
            return Error("The external account did not provide an email address.");

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.EmailId == email);

        if (customer == null)
        {
            customer = new Customer
            {
                EmailId = email,
                FirstName = string.IsNullOrEmpty(firstName) ? email.Split('@')[0] : firstName,
                LastName = lastName,
                EmailVerfied = true,
                GooglePayLoad = externalPayload,
                CreatedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow,
                CustomerStatusId = CustomerStatusActive
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            // Brand new social account: no internal password yet, so the storefront asks for one.
            return Success(customer, "Login successful.", requiresPasswordSetup: true);
        }

        // Same status rules as a password login, so a blocked account cannot bypass them.
        if (customer.CustomerStatusId != CustomerStatusActive)
        {
            return customer.CustomerStatusId switch
            {
                2 => Error("Your account has been deactivated. Please contact support."),
                3 => Error("Your account has been suspended. Please contact support."),
                _ => Error("Your account is not active. Please contact support.")
            };
        }

        var changed = false;

        if (string.IsNullOrWhiteSpace(customer.FirstName) && !string.IsNullOrEmpty(firstName))
        {
            customer.FirstName = firstName;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(customer.LastName) && !string.IsNullOrEmpty(lastName))
        {
            customer.LastName = lastName;
            changed = true;
        }

        if (!string.IsNullOrEmpty(externalPayload) && customer.GooglePayLoad != externalPayload)
        {
            customer.GooglePayLoad = externalPayload;
            changed = true;
        }

        if (!customer.EmailVerfied)
        {
            customer.EmailVerfied = true;
            changed = true;
        }

        if (changed)
        {
            customer.ModifiedOn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        // Accounts that never got an internal password (e.g. created by an earlier social
        // sign-in or by guest checkout) are asked to set one as well.
        return Success(customer, "Login successful.", requiresPasswordSetup: string.IsNullOrEmpty(customer.Password));
    }

    private LoginCustomerResponseDto Success(Customer customer, string message, bool requiresPasswordSetup = false)
    {
        // Every successful login/registration issues a signed token that the customer APIs require.
        var (token, expiresOn) = CustomerTokenService.Create(customer.CustomerId, customer.EmailId, _jwtSecret);

        return new LoginCustomerResponseDto
        {
            Result = 1,
            Messages = new[] { message },
            Token = token,
            ExpiresOn = expiresOn,
            RequiresPasswordSetup = requiresPasswordSetup,
            Customer = new CustomerDto
            {
                CustomerID = customer.CustomerId,
                FirstName = customer.FirstName,
                MiddleName = customer.MiddleName ?? "",
                LastName = customer.LastName ?? "",
                EmailId = customer.EmailId,
                MobileNumber = customer.MobileNumber ?? "",
                MobileIsd = customer.MobileIsd ?? "",
                IsGuest = false
            }
        };
    }

    private static LoginCustomerResponseDto Error(string message)
    {
        return new LoginCustomerResponseDto
        {
            Result = 0,
            Messages = new[] { message },
            Customer = null
        };
    }

}
