namespace HC.Business.Dtos;

/// <summary>
/// One entry in a signed-in customer's address book (Customers -> CustomerAddresses).
///
/// An address book entry is what the customer can pick in checkout as the address the order is
/// shipped to, and - separately - as the address the order is billed to. The same row can be used
/// by any number of orders, which is why editing one also updates what those orders show: the shop
/// ships to the address as it stands in the book.
/// </summary>
public class CustomerAddressDto
{
    public long AddressId { get; set; }

    /// <summary>The customer's own label ("Home", "Office", ...) - see <see cref="AddressTitleMaxLength"/>.</summary>
    public string AddressTitle { get; set; } = "";

    public string ContactName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "India";
    public string Zipcode { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string? EmailId { get; set; }

    /// <summary>Longest value CustomerAddresses.AddressTitle accepts (the column is nvarchar(20)).</summary>
    public const int AddressTitleMaxLength = 20;
}

/// <summary>
/// An address book entry as it comes from the storefront. <see cref="AddressId"/> is 0 for a new
/// address; anything else edits that address - but only while it belongs to the caller.
/// </summary>
public class SaveCustomerAddressRequest
{
    public long AddressId { get; set; }
    public string AddressTitle { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "";
    public string Zipcode { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string EmailId { get; set; } = "";
}

/// <summary>Answer to 'Customer/SaveAddress' - carries the saved row so the storefront can select it.</summary>
public class CustomerAddressResultDto : ResultDto
{
    public CustomerAddressDto? Address { get; set; }
}

/// <summary>Body of 'Customer/DeleteAddress'.</summary>
public class DeleteCustomerAddressRequest
{
    public long AddressId { get; set; }
}
