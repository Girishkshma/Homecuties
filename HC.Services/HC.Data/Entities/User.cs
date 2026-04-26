using System;
using System.Collections.Generic;

namespace HC.Data.Entities;

public partial class User
{
    public long UserId { get; set; }

    public string LoginId { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string? CorporateName { get; set; }

    public string FirstName { get; set; } = null!;

    public string? MiddleName { get; set; }

    public string? LastName { get; set; }

    public string? EmailId { get; set; }

    public string? MobileNumber { get; set; }

    public string? Dob { get; set; }

    public string? Gender { get; set; }

    public string AddressLine1 { get; set; } = null!;

    public string? AddressLine2 { get; set; }

    public string? AddressLandMark { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Country { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? PasswordLastChangedOn { get; set; }

    public bool? MustChangePassword { get; set; }

    public long? AddedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public long? ModifiedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? PassportNo { get; set; }

    public string? Zipcode { get; set; }

    public string? CorpRegNo { get; set; }

    public virtual ICollection<PartnersUser> PartnersUsers { get; set; } = new List<PartnersUser>();

    public virtual ICollection<Product> ProductCreatedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<Product> ProductModifiedByNavigations { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseComment> PurchaseComments { get; set; } = new List<PurchaseComment>();

    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public virtual ICollection<UserCommunication> UserCommunications { get; set; } = new List<UserCommunication>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    public virtual ICollection<UserRolesHistory> UserRolesHistories { get; set; } = new List<UserRolesHistory>();

    public virtual ICollection<VendorsUser> VendorsUsers { get; set; } = new List<VendorsUser>();
}
