using System;
using System.Collections.Generic;
using HC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HC.Data;

public partial class HomecutiesDbContext : DbContext
{
    public HomecutiesDbContext(DbContextOptions<HomecutiesDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccessTracking> AccessTrackings { get; set; }

    public virtual DbSet<AdminActivitiesRole> AdminActivitiesRoles { get; set; }

    public virtual DbSet<AdminActivity> AdminActivities { get; set; }

    public virtual DbSet<AdminMenu> AdminMenus { get; set; }

    public virtual DbSet<AdminMenusRole> AdminMenusRoles { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CommunicationMode> CommunicationModes { get; set; }

    public virtual DbSet<CommunicationTemplate> CommunicationTemplates { get; set; }

    public virtual DbSet<CommunicationType> CommunicationTypes { get; set; }

    public virtual DbSet<CommunicationTypeDataField> CommunicationTypeDataFields { get; set; }

    public virtual DbSet<CommunicationTypeParameter> CommunicationTypeParameters { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerAddress> CustomerAddresses { get; set; }

    public virtual DbSet<CustomerStatus> CustomerStatuses { get; set; }

    public virtual DbSet<EmailCommunication> EmailCommunications { get; set; }

    public virtual DbSet<EmailCommunicationAttachment> EmailCommunicationAttachments { get; set; }

    public virtual DbSet<EmailCommunicationDetail> EmailCommunicationDetails { get; set; }

    public virtual DbSet<EmailCommunicationStatus> EmailCommunicationStatuses { get; set; }

    public virtual DbSet<EmailValidationTracking> EmailValidationTrackings { get; set; }

    public virtual DbSet<GuestCart> GuestCarts { get; set; }

    public virtual DbSet<GuestCartItem> GuestCartItems { get; set; }

    public virtual DbSet<GuestCustomer> GuestCustomers { get; set; }

    public virtual DbSet<ImageType> ImageTypes { get; set; }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<InventoryStatus> InventoryStatuses { get; set; }

    public virtual DbSet<MobileValidationTracking> MobileValidationTrackings { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderHistory> OrderHistories { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<OrderStatus> OrderStatuses { get; set; }

    public virtual DbSet<Parameter> Parameters { get; set; }

    public virtual DbSet<Partner> Partners { get; set; }

    public virtual DbSet<PartnerStatus> PartnerStatuses { get; set; }

    public virtual DbSet<PartnersUser> PartnersUsers { get; set; }

    public virtual DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductCategory> ProductCategories { get; set; }

    public virtual DbSet<ProductFeature> ProductFeatures { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<ProductStatus> ProductStatuses { get; set; }

    public virtual DbSet<Purchase> Purchases { get; set; }

    public virtual DbSet<PurchaseComment> PurchaseComments { get; set; }

    public virtual DbSet<PurchaseDetail> PurchaseDetails { get; set; }

    public virtual DbSet<PurchaseStatus> PurchaseStatuses { get; set; }

    public virtual DbSet<PurchaseStatusCompatibility> PurchaseStatusCompatibilities { get; set; }

    public virtual DbSet<PurchaseStatusUserRoleCompatibility> PurchaseStatusUserRoleCompatibilities { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Salutation> Salutations { get; set; }

    public virtual DbSet<Scheduler> Schedulers { get; set; }

    public virtual DbSet<SchedulerTransaction> SchedulerTransactions { get; set; }

    public virtual DbSet<Sku> Skus { get; set; }

    public virtual DbSet<Skuhistory> Skuhistories { get; set; }

    public virtual DbSet<Skustatus> Skustatuses { get; set; }

    public virtual DbSet<SmscommuncationStatus> SmscommuncationStatuses { get; set; }

    public virtual DbSet<Smscommunication> Smscommunications { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserCommunication> UserCommunications { get; set; }

    public virtual DbSet<UserCommunicationParameter> UserCommunicationParameters { get; set; }

    public virtual DbSet<UserCommunicationsDatum> UserCommunicationsData { get; set; }

    public virtual DbSet<UserRelation> UserRelations { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<UserRoleStatus> UserRoleStatuses { get; set; }

    public virtual DbSet<UserRolesHistory> UserRolesHistories { get; set; }

    public virtual DbSet<VAccesstracking> VAccesstrackings { get; set; }

    public virtual DbSet<Vendor> Vendors { get; set; }

    public virtual DbSet<VendorsUser> VendorsUsers { get; set; }

    public virtual DbSet<WishList> WishLists { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccessTracking>(entity =>
        {
            entity.ToTable("AccessTracking");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.AccessOn).HasColumnType("datetime");
            entity.Property(e => e.City)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Country)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ip)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("IP");
            entity.Property(e => e.Organization)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Region)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Url)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("URL");
        });

        modelBuilder.Entity<AdminActivitiesRole>(entity =>
        {
            entity.HasKey(e => new { e.ActivityId, e.RoleId });

            entity.Property(e => e.ActivityId).HasColumnName("ActivityID");
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_AdminActivitiesRoles_IsActive");

            entity.HasOne(d => d.Activity).WithMany(p => p.AdminActivitiesRoles)
                .HasForeignKey(d => d.ActivityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdminActivitiesRoles_AdminActivities");

            entity.HasOne(d => d.Role).WithMany(p => p.AdminActivitiesRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdminActivitiesRoles_Roles");
        });

        modelBuilder.Entity<AdminActivity>(entity =>
        {
            entity.HasKey(e => e.ActivityId);

            entity.Property(e => e.ActivityId)
                .ValueGeneratedNever()
                .HasColumnName("ActivityID");
            entity.Property(e => e.ActivityTitle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_AdminActivities_IsActive");
            entity.Property(e => e.MenuId).HasColumnName("MenuID");

            entity.HasOne(d => d.Menu).WithMany(p => p.AdminActivities)
                .HasForeignKey(d => d.MenuId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdminActivities_AdminMenus");
        });

        modelBuilder.Entity<AdminMenu>(entity =>
        {
            entity.HasKey(e => e.MenuId);

            entity.Property(e => e.MenuId)
                .ValueGeneratedNever()
                .HasColumnName("MenuID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_AdminMenus_IsActive");
            entity.Property(e => e.MenuDescription)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.MenuTitle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MenuUrl)
                .HasMaxLength(1000)
                .IsUnicode(false)
                .HasColumnName("MenuURL");
            entity.Property(e => e.ParentMenuId).HasColumnName("ParentMenuID");

            entity.HasOne(d => d.ParentMenu).WithMany(p => p.InverseParentMenu)
                .HasForeignKey(d => d.ParentMenuId)
                .HasConstraintName("FK_AdminMenus_AdminMenus");
        });

        modelBuilder.Entity<AdminMenusRole>(entity =>
        {
            entity.HasKey(e => new { e.MenuId, e.RoleId });

            entity.Property(e => e.MenuId).HasColumnName("MenuID");
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_AdminMenusRoles_IsActive");

            entity.HasOne(d => d.Menu).WithMany(p => p.AdminMenusRoles)
                .HasForeignKey(d => d.MenuId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdminMenusRoles_AdminMenus");

            entity.HasOne(d => d.Role).WithMany(p => p.AdminMenusRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdminMenusRoles_Roles");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.ToTable("Cart");

            entity.Property(e => e.CartId).HasColumnName("CartID");
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

            entity.HasOne(d => d.Customer).WithMany(p => p.Carts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cart_Customers");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => new { e.CartId, e.ProductId });

            entity.Property(e => e.CartId).HasColumnName("CartID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItems_Cart");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItems_Products");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK_ProductCategories");

            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ParentCategoryId).HasColumnName("ParentCategoryID");

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("FK_ProductCategories_ProductCategories");
        });

        modelBuilder.Entity<CommunicationMode>(entity =>
        {
            entity.HasIndex(e => e.CommunicationMode1, "UQ_CommunicationModes").IsUnique();

            entity.Property(e => e.CommunicationModeId)
                .ValueGeneratedNever()
                .HasColumnName("CommunicationModeID");
            entity.Property(e => e.CommunicationMode1)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CommunicationMode");
        });

        modelBuilder.Entity<CommunicationTemplate>(entity =>
        {
            entity.Property(e => e.CommunicationTemplateId)
                .ValueGeneratedNever()
                .HasColumnName("CommunicationTemplateID");
            entity.Property(e => e.Htmlpath)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("HTMLPath");
            entity.Property(e => e.Query)
                .HasMaxLength(4000)
                .IsUnicode(false);
            entity.Property(e => e.TemplateDescription)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.TemplateName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.XslPath)
                .HasMaxLength(150)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CommunicationType>(entity =>
        {
            entity.Property(e => e.CommunicationTypeId)
                .ValueGeneratedNever()
                .HasColumnName("CommunicationTypeID");
            entity.Property(e => e.CommunicationModeId).HasColumnName("CommunicationModeID");
            entity.Property(e => e.CommunicationTemplateId).HasColumnName("CommunicationTemplateID");
            entity.Property(e => e.CommunicationType1)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CommunicationType");

            entity.HasOne(d => d.CommunicationMode).WithMany(p => p.CommunicationTypes)
                .HasForeignKey(d => d.CommunicationModeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CommunicationTypes_CommunicationModes");

            entity.HasOne(d => d.CommunicationTemplate).WithMany(p => p.CommunicationTypes)
                .HasForeignKey(d => d.CommunicationTemplateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CommunicationTypes_CommunicationTemplates");
        });

        modelBuilder.Entity<CommunicationTypeDataField>(entity =>
        {
            entity.HasIndex(e => e.CommunicationTypeId, "IX_CommunicationTypeDataFields");

            entity.Property(e => e.CommunicationTypeDataFieldId)
                .ValueGeneratedNever()
                .HasColumnName("CommunicationTypeDataFieldID");
            entity.Property(e => e.CommunicationTypeId).HasColumnName("CommunicationTypeID");
            entity.Property(e => e.DataFieldDisplayName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DataFieldName)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.CommunicationType).WithMany(p => p.CommunicationTypeDataFields)
                .HasForeignKey(d => d.CommunicationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CommunicationTypeDataFields_CommunicationTypes");
        });

        modelBuilder.Entity<CommunicationTypeParameter>(entity =>
        {
            entity.HasKey(e => e.CommunicationTypeParameterId).HasName("PK__Communic__DA35930D6310AC05");

            entity.Property(e => e.CommunicationTypeParameterId)
                .ValueGeneratedNever()
                .HasColumnName("CommunicationTypeParameterID");
            entity.Property(e => e.CommunicationTypeId).HasColumnName("CommunicationTypeID");
            entity.Property(e => e.CommunicationTypeParameterDescription)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.CommunicationTypeParameterName)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.CommunicationType).WithMany(p => p.CommunicationTypeParameters)
                .HasForeignKey(d => d.CommunicationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Communica__Commu__607251E5");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(e => e.EmailId, "IX_EmailID").IsUnique();

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.CustomerStatusId).HasColumnName("CustomerStatusID");
            entity.Property(e => e.EmailId)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("EmailID");
            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GooglePayLoad)
                .HasMaxLength(2000)
                .IsUnicode(false);
            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MiddleName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MobileIsd)
                .HasMaxLength(3)
                .IsUnicode(false)
                .HasColumnName("MobileISD");
            entity.Property(e => e.MobileNumber)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.MobileVerified).HasDefaultValue(false, "DF_Customers_MobileVerified");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.Password)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.CustomerStatus).WithMany(p => p.Customers)
                .HasForeignKey(d => d.CustomerStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Customers_CustomerStatuses");
        });

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.HasKey(e => e.AddressId);

            entity.Property(e => e.AddressId).HasColumnName("AddressID");
            entity.Property(e => e.AddressLine1)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.AddressLine2)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.AddressTitle)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.City)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ContactName)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Country)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.EmailId)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("EmailID");
            entity.Property(e => e.MobileNumber)
                .HasMaxLength(12)
                .IsUnicode(false);
            entity.Property(e => e.State)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Zipcode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("ZIPCode");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerAddresses)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerAddresses_Customers");
        });

        modelBuilder.Entity<CustomerStatus>(entity =>
        {
            entity.Property(e => e.CustomerStatusId)
                .ValueGeneratedNever()
                .HasColumnName("CustomerStatusID");
            entity.Property(e => e.CustomerStatus1)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("CustomerStatus");
        });

        modelBuilder.Entity<EmailCommunication>(entity =>
        {
            entity.Property(e => e.EmailCommunicationId).HasColumnName("EmailCommunicationID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.Bccaddress)
                .HasMaxLength(1000)
                .IsUnicode(false)
                .HasColumnName("BCCAddress");
            entity.Property(e => e.Body)
                .HasMaxLength(8000)
                .IsUnicode(false);
            entity.Property(e => e.Ccaddress)
                .HasMaxLength(1000)
                .IsUnicode(false)
                .HasColumnName("CCAddress");
            entity.Property(e => e.EmailCommunicationStatusId).HasColumnName("EmailCommunicationStatusID");
            entity.Property(e => e.FromConfigName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IsHtml)
                .HasDefaultValue(true, "DF_EmailCommunications_IsHTML")
                .HasColumnName("IsHTML");
            entity.Property(e => e.SentOn).HasColumnType("datetime");
            entity.Property(e => e.SubjectLine)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.ToAddress)
                .HasMaxLength(1000)
                .IsUnicode(false);

            entity.HasOne(d => d.EmailCommunicationStatus).WithMany(p => p.EmailCommunications)
                .HasForeignKey(d => d.EmailCommunicationStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmailCommunications_EmailCommunicationStatus");
        });

        modelBuilder.Entity<EmailCommunicationAttachment>(entity =>
        {
            entity.Property(e => e.EmailCommunicationAttachmentId).ValueGeneratedNever();
            entity.Property(e => e.Attachment)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.EmailCommunicationId).HasColumnName("EmailCommunicationID");

            entity.HasOne(d => d.EmailCommunication).WithMany(p => p.EmailCommunicationAttachments)
                .HasForeignKey(d => d.EmailCommunicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmailCommunicationAttachments_EmailCommunications");
        });

        modelBuilder.Entity<EmailCommunicationDetail>(entity =>
        {
            entity.HasKey(e => new { e.EmailCommunicationDetailId, e.EmailCommunicationId });

            entity.Property(e => e.EmailCommunicationDetailId)
                .ValueGeneratedOnAdd()
                .HasColumnName("EmailCommunicationDetailID");
            entity.Property(e => e.EmailCommunicationId).HasColumnName("EmailCommunicationID");
            entity.Property(e => e.Body)
                .HasMaxLength(8000)
                .IsUnicode(false);

            entity.HasOne(d => d.EmailCommunication).WithMany(p => p.EmailCommunicationDetails)
                .HasForeignKey(d => d.EmailCommunicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmailCommunicationDetails_EmailCommunications");
        });

        modelBuilder.Entity<EmailCommunicationStatus>(entity =>
        {
            entity.ToTable("EmailCommunicationStatus");

            entity.Property(e => e.EmailCommunicationStatusId)
                .ValueGeneratedNever()
                .HasColumnName("EmailCommunicationStatusID");
            entity.Property(e => e.EmailCommunicationStatus1)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("EmailCommunicationStatus");
        });

        modelBuilder.Entity<EmailValidationTracking>(entity =>
        {
            entity.HasKey(e => e.EmailId);

            entity.ToTable("EmailValidationTracking");

            entity.Property(e => e.EmailId)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("EmailID");
            entity.Property(e => e.Datecreated)
                .HasColumnType("datetime")
                .HasColumnName("DATECREATED");
            entity.Property(e => e.Otp)
                .HasMaxLength(6)
                .IsUnicode(false)
                .HasColumnName("OTP");
        });

        modelBuilder.Entity<GuestCart>(entity =>
        {
            entity.HasKey(e => e.CartId);

            entity.ToTable("GuestCart");

            entity.Property(e => e.CartId).HasColumnName("CartID");
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");

            entity.HasOne(d => d.Customer).WithMany(p => p.GuestCarts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GuestCart_GuestCustomers");
        });

        modelBuilder.Entity<GuestCartItem>(entity =>
        {
            entity.HasKey(e => new { e.CartId, e.ProductId });

            entity.Property(e => e.CartId).HasColumnName("CartID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");

            entity.HasOne(d => d.Cart).WithMany(p => p.GuestCartItems)
                .HasForeignKey(d => d.CartId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GuestCartItems_GuestCart");

            entity.HasOne(d => d.Product).WithMany(p => p.GuestCartItems)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GuestCartItems_Products");
        });

        modelBuilder.Entity<GuestCustomer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
        });

        modelBuilder.Entity<ImageType>(entity =>
        {
            entity.Property(e => e.ImageTypeId)
                .ValueGeneratedNever()
                .HasColumnName("ImageTypeID");
            entity.Property(e => e.ImageTypeName)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ShortCode)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength()
                .HasDefaultValue("S", "DF_ImageTypes_ShortCode");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.Property(e => e.InventoryId).HasColumnName("InventoryID");
            entity.Property(e => e.InventoryName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.InventoryStatusId).HasColumnName("InventoryStatusID");
            entity.Property(e => e.PartnerId).HasColumnName("PartnerID");

            entity.HasOne(d => d.InventoryStatus).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.InventoryStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Inventories_InventoryStatuses");

            entity.HasOne(d => d.Partner).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.PartnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Inventories_Partners");
        });

        modelBuilder.Entity<InventoryStatus>(entity =>
        {
            entity.Property(e => e.InventoryStatusId)
                .ValueGeneratedNever()
                .HasColumnName("InventoryStatusID");
            entity.Property(e => e.InventoryStatus1)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("InventoryStatus");
        });

        modelBuilder.Entity<MobileValidationTracking>(entity =>
        {
            entity.HasKey(e => e.MobileNumber);

            entity.ToTable("MobileValidationTracking");

            entity.Property(e => e.MobileNumber)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.DateCreated).HasColumnType("datetime");
            entity.Property(e => e.Otp)
                .HasMaxLength(6)
                .IsUnicode(false)
                .HasColumnName("OTP");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.BillingAddressId).HasColumnName("BillingAddressID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.OrderDate).HasColumnType("datetime");
            entity.Property(e => e.OrderStatusId).HasColumnName("OrderStatusID");
            entity.Property(e => e.SellerId).HasColumnName("SellerID");
            entity.Property(e => e.ShippingAddressId).HasColumnName("ShippingAddressID");

            entity.HasOne(d => d.BillingAddress).WithMany(p => p.OrderBillingAddresses)
                .HasForeignKey(d => d.BillingAddressId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_CustomerAddresses");

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Customers");

            entity.HasOne(d => d.OrderStatus).WithMany(p => p.Orders)
                .HasForeignKey(d => d.OrderStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_OrderStatuses");

            entity.HasOne(d => d.Seller).WithMany(p => p.Orders)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Partners");

            entity.HasOne(d => d.ShippingAddress).WithMany(p => p.OrderShippingAddresses)
                .HasForeignKey(d => d.ShippingAddressId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_CustomerAddresses1");
        });

        modelBuilder.Entity<OrderHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId);

            entity.ToTable("OrderHistory");

            entity.Property(e => e.HistoryId)
                .ValueGeneratedNever()
                .HasColumnName("HistoryID");
            entity.Property(e => e.Comments)
                .HasMaxLength(2000)
                .IsUnicode(false);
            entity.Property(e => e.HistoryDate).HasColumnType("datetime");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.OrderStatusId).HasColumnName("OrderStatusID");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderHistories)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderHistory_Orders");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => new { e.OrderId, e.Sku });

            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.Sku)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SKU");
            entity.Property(e => e.AdditionalDiscountPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Cgstpercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("CGSTPercent");
            entity.Property(e => e.DeliveryCharge).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.DiscountPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Hsncode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("HSNCode");
            entity.Property(e => e.Igstpercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("IGSTPercent");
            entity.Property(e => e.PackagingCharge).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.ProductCategoryId).HasColumnName("ProductCategoryID");
            entity.Property(e => e.ProductDescription)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.ProductName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProductTitle)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.ProfitMarginPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Sgstpercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("SGSTPercent");
            entity.Property(e => e.StorageCharge).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(9, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItems_Orders");

            entity.HasOne(d => d.ProductCategory).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItems_ProductCategories");

            entity.HasOne(d => d.SkuNavigation).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.Sku)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderItems_SKUs");
        });

        modelBuilder.Entity<OrderStatus>(entity =>
        {
            entity.Property(e => e.OrderStatusId)
                .ValueGeneratedNever()
                .HasColumnName("OrderStatusID");
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Parameter>(entity =>
        {
            entity.Property(e => e.ParameterId)
                .ValueGeneratedNever()
                .HasColumnName("ParameterID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Parameters_IsActive");
            entity.Property(e => e.ParameterName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ParameterValue)
                .HasMaxLength(1000)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Partner>(entity =>
        {
            entity.Property(e => e.PartnerId).HasColumnName("PartnerID");
            entity.Property(e => e.LastModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.PartnerName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PartnerStatusId).HasColumnName("PartnerStatusID");

            entity.HasOne(d => d.PartnerStatus).WithMany(p => p.Partners)
                .HasForeignKey(d => d.PartnerStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Partners_PartnerStatuses");
        });

        modelBuilder.Entity<PartnerStatus>(entity =>
        {
            entity.Property(e => e.PartnerStatusId)
                .ValueGeneratedNever()
                .HasColumnName("PartnerStatusID");
            entity.Property(e => e.PartnerStatus1)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("PartnerStatus");
        });

        modelBuilder.Entity<PartnersUser>(entity =>
        {
            entity.HasKey(e => e.PartnerUserId);

            entity.HasIndex(e => new { e.PartnerId, e.UserId }, "IX_PartnersUsers").IsUnique();

            entity.Property(e => e.PartnerUserId).HasColumnName("PartnerUserID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_PartnersUsers_IsActive");
            entity.Property(e => e.PartnerId).HasColumnName("PartnerID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Partner).WithMany(p => p.PartnersUsers)
                .HasForeignKey(d => d.PartnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnersUsers_Partners");

            entity.HasOne(d => d.User).WithMany(p => p.PartnersUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PartnersUsers_Users");
        });

        modelBuilder.Entity<PasswordResetRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK_PasswordResetRequest");

            entity.Property(e => e.RequestId).HasColumnName("RequestID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.CommunicationSentOn).HasColumnType("datetime");
            entity.Property(e => e.EmailId)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("EmailID");
            entity.Property(e => e.Jwt)
                .HasMaxLength(8000)
                .IsUnicode(false)
                .HasColumnName("JWT");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.AdditionalDiscountPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Cgstpercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("CGSTPercent");
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.DeliveryCharge).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.DiscountPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Hsncode)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("HSNCode");
            entity.Property(e => e.Igstpercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("IGSTPercent");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.PackagingCharge).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.ProductDescription)
                .HasMaxLength(2000)
                .IsUnicode(false);
            entity.Property(e => e.ProductName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProductStatusId).HasColumnName("ProductStatusID");
            entity.Property(e => e.ProductTitle)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.ProfitMarginPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Sgstpercent)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("SGSTPercent");
            entity.Property(e => e.StorageCharge).HasColumnType("decimal(9, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(9, 2)");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProductCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Users");

            entity.HasOne(d => d.ModifiedByNavigation).WithMany(p => p.ProductModifiedByNavigations)
                .HasForeignKey(d => d.ModifiedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Users1");

            entity.HasOne(d => d.ProductStatus).WithMany(p => p.Products)
                .HasForeignKey(d => d.ProductStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_ProductStatuses");
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.CategoryId }).HasName("PK_ProductCategories_1");

            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ProductCategoryRelations_IsActive");

            entity.HasOne(d => d.Category).WithMany(p => p.ProductCategories)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductCategoryRelations_ProductCategories");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductCategories)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductCategoryRelations_Products");
        });

        modelBuilder.Entity<ProductFeature>(entity =>
        {
            entity.Property(e => e.ProductFeatureId).HasColumnName("ProductFeatureID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ProductFeatures_IsActive");
            entity.Property(e => e.ProductFeature1)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("ProductFeature");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductFeatures)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductFeatures_Products");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.Property(e => e.ProductImageId).HasColumnName("ProductImageID");
            entity.Property(e => e.ImageTypeId).HasColumnName("ImageTypeID");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("ImageURL");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ProductImages_IsActive");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");

            entity.HasOne(d => d.ImageType).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ImageTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImages_ImageTypes");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductImages_Products");
        });

        modelBuilder.Entity<ProductStatus>(entity =>
        {
            entity.Property(e => e.ProductStatusId)
                .ValueGeneratedNever()
                .HasColumnName("ProductStatusID");
            entity.Property(e => e.ProductStatusName)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.Property(e => e.PurchaseId).HasColumnName("PurchaseID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.InvoicePath)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.LastModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.PurchaseDate).HasColumnType("datetime");
            entity.Property(e => e.PurchaseStatusId).HasColumnName("PurchaseStatusID");
            entity.Property(e => e.PurchaserId).HasColumnName("PurchaserID");
            entity.Property(e => e.VendorId).HasColumnName("VendorID");

            entity.HasOne(d => d.LastModifiedByNavigation).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.LastModifiedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchases_Users");

            entity.HasOne(d => d.PurchaseStatus).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.PurchaseStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseStatuses_Purchases");

            entity.HasOne(d => d.Purchaser).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.PurchaserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchases_PartnersUsers");

            entity.HasOne(d => d.Vendor).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Purchases_Vendors");
        });

        modelBuilder.Entity<PurchaseComment>(entity =>
        {
            entity.Property(e => e.PurchaseCommentId).HasColumnName("PurchaseCommentID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.Comments)
                .HasMaxLength(2000)
                .IsUnicode(false);
            entity.Property(e => e.PurchaseId).HasColumnName("PurchaseID");

            entity.HasOne(d => d.AddedByNavigation).WithMany(p => p.PurchaseComments)
                .HasForeignKey(d => d.AddedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseComments_Users");

            entity.HasOne(d => d.Purchase).WithMany(p => p.PurchaseComments)
                .HasForeignKey(d => d.PurchaseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseComments_Purchases");
        });

        modelBuilder.Entity<PurchaseDetail>(entity =>
        {
            entity.Property(e => e.PurchaseDetailId).HasColumnName("PurchaseDetailID");
            entity.Property(e => e.Gst)
                .HasColumnType("decimal(6, 2)")
                .HasColumnName("GST");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.PurchaseId).HasColumnName("PurchaseID");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(9, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseDetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseDetails_Products");

            entity.HasOne(d => d.Purchase).WithMany(p => p.PurchaseDetails)
                .HasForeignKey(d => d.PurchaseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseDetails_Purchases");
        });

        modelBuilder.Entity<PurchaseStatus>(entity =>
        {
            entity.HasKey(e => e.PurchaseStatusId).HasName("PK__Purchase__646AA0032469D310");

            entity.Property(e => e.PurchaseStatusId)
                .ValueGeneratedNever()
                .HasColumnName("PurchaseStatusID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PurchaseStatusName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<PurchaseStatusCompatibility>(entity =>
        {
            entity.HasKey(e => e.RelationId);

            entity.HasIndex(e => new { e.PurchaseStatusId, e.PurchaseNewStatusId }, "IX_PurchaseStatusCompatibilities").IsUnique();

            entity.Property(e => e.RelationId).HasColumnName("RelationID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_PurchaseStatusCompatibilities_IsActive");
            entity.Property(e => e.PurchaseNewStatusId).HasColumnName("PurchaseNewStatusID");
            entity.Property(e => e.PurchaseStatusId).HasColumnName("PurchaseStatusID");

            entity.HasOne(d => d.PurchaseNewStatus).WithMany(p => p.PurchaseStatusCompatibilityPurchaseNewStatuses)
                .HasForeignKey(d => d.PurchaseNewStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseStatusCompatibilities_PurchaseStatuses");

            entity.HasOne(d => d.PurchaseStatus).WithMany(p => p.PurchaseStatusCompatibilityPurchaseStatuses)
                .HasForeignKey(d => d.PurchaseStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseStatusCompatibilities_PurchaseStatusCompatibilities");
        });

        modelBuilder.Entity<PurchaseStatusUserRoleCompatibility>(entity =>
        {
            entity.HasKey(e => e.RelationId).HasName("PK__Purchase__E2DA1695ABEB99AA");

            entity.Property(e => e.RelationId).HasColumnName("RelationID");
            entity.Property(e => e.PurchaseStatusId).HasColumnName("PurchaseStatusID");
            entity.Property(e => e.UserRoleId).HasColumnName("UserRoleID");

            entity.HasOne(d => d.PurchaseStatus).WithMany(p => p.PurchaseStatusUserRoleCompatibilities)
                .HasForeignKey(d => d.PurchaseStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseStatusUserRoleCompatibilities_PurchaseStatuses");

            entity.HasOne(d => d.UserRole).WithMany(p => p.PurchaseStatusUserRoleCompatibilities)
                .HasForeignKey(d => d.UserRoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseStatusUserRoleCompatibilities_Roles");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.RoleId)
                .ValueGeneratedNever()
                .HasColumnName("RoleID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF__Roles__IsActive__31EC6D26");
            entity.Property(e => e.RoleDescription)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Salutation>(entity =>
        {
            entity.Property(e => e.SalutationId)
                .ValueGeneratedNever()
                .HasColumnName("SalutationID");
            entity.Property(e => e.Salutation1)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("Salutation");
        });

        modelBuilder.Entity<Scheduler>(entity =>
        {
            entity.HasKey(e => e.ScheduleId);

            entity.Property(e => e.ScheduleId).ValueGeneratedNever();
            entity.Property(e => e.AssemblyToLoad)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasComment("valid name of the assembly to load (with physical path if necessory)");
            entity.Property(e => e.ClassToLoad)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasComment("Fully Qualified Name of the Class to Load with in Assembly to Load");
            entity.Property(e => e.EffectiveFrom).HasColumnType("datetime");
            entity.Property(e => e.EffectiveTill).HasColumnType("datetime");
            entity.Property(e => e.NextExecutionOn).HasColumnType("datetime");
            entity.Property(e => e.ScheduleDescription)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.ScheduleExecutionUnit)
                .HasMaxLength(1)
                .IsUnicode(false)
                .HasComment("'Y' - Year, 'Q' - Quarter Year, 'M' - Month, 'W' - Week, 'D' - Day, 'H' - Hour, 'I' - minute, 'S' - Second");
            entity.Property(e => e.ScheduleName)
                .HasMaxLength(150)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SchedulerTransaction>(entity =>
        {
            entity.HasKey(e => e.ScheduleTransactionId);

            entity.Property(e => e.ScheduleCompletedOn).HasColumnType("datetime");
            entity.Property(e => e.ScheduleStartedOn).HasColumnType("datetime");
            entity.Property(e => e.ScheduleTransactionStatus)
                .HasMaxLength(1)
                .IsUnicode(false)
                .HasComment("Statuses : 'S' : Started, 'E' : Executing, 'F' : Failed, 'C' : Completed")
                .HasDefaultValue("S", "DF_SchedulerTransactions_ScheduleTransactionStatus");

            entity.HasOne(d => d.Schedule).WithMany(p => p.SchedulerTransactions)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SchedulerTransactions_Schedulers");
        });

        modelBuilder.Entity<Sku>(entity =>
        {
            entity.HasKey(e => e.Sku1);

            entity.ToTable("SKUs");

            entity.Property(e => e.Sku1)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SKU");
            entity.Property(e => e.InventoryId).HasColumnName("InventoryID");
            entity.Property(e => e.PurchaseDetailId).HasColumnName("PurchaseDetailID");
            entity.Property(e => e.SkustatusId).HasColumnName("SKUStatusID");

            entity.HasOne(d => d.Inventory).WithMany(p => p.Skus)
                .HasForeignKey(d => d.InventoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SKUs_Inventories");

            entity.HasOne(d => d.PurchaseDetail).WithMany(p => p.Skus)
                .HasForeignKey(d => d.PurchaseDetailId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SKUs_PurchaseDetails");

            entity.HasOne(d => d.Skustatus).WithMany(p => p.Skus)
                .HasForeignKey(d => d.SkustatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SKUs_SKUStatuses");
        });

        modelBuilder.Entity<Skuhistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId);

            entity.ToTable("SKUHistory");

            entity.Property(e => e.HistoryId).HasColumnName("HistoryID");
            entity.Property(e => e.HistoryDate).HasColumnType("datetime");
            entity.Property(e => e.InventoryId).HasColumnName("InventoryID");
            entity.Property(e => e.Sku)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SKU");
            entity.Property(e => e.SkustatusId).HasColumnName("SKUStatusID");

            entity.HasOne(d => d.SkuNavigation).WithMany(p => p.Skuhistories)
                .HasForeignKey(d => d.Sku)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SKUHistory_SKUs");
        });

        modelBuilder.Entity<Skustatus>(entity =>
        {
            entity.ToTable("SKUStatuses");

            entity.Property(e => e.SkustatusId)
                .ValueGeneratedNever()
                .HasColumnName("SKUStatusID");
            entity.Property(e => e.Skustatus1)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("SKUStatus");
        });

        modelBuilder.Entity<SmscommuncationStatus>(entity =>
        {
            entity.HasKey(e => e.SmscommunicationStatusId);

            entity.ToTable("SMSCommuncationStatuses");

            entity.Property(e => e.SmscommunicationStatusId)
                .ValueGeneratedNever()
                .HasColumnName("SMSCommunicationStatusID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_SMSCommuncationStatuses_IsActive");
            entity.Property(e => e.SmscommunicationStatus)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("SMSCommunicationStatus");
        });

        modelBuilder.Entity<Smscommunication>(entity =>
        {
            entity.ToTable("SMSCommunications");

            entity.Property(e => e.SmscommunicationId).HasColumnName("SMSCommunicationID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.FromConfigName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Message)
                .HasMaxLength(8000)
                .IsUnicode(false);
            entity.Property(e => e.MobileNumber)
                .HasMaxLength(1000)
                .IsUnicode(false);
            entity.Property(e => e.SentOn).HasColumnType("datetime");
            entity.Property(e => e.SmscommunicationStatusId).HasColumnName("SMSCommunicationStatusID");

            entity.HasOne(d => d.SmscommunicationStatus).WithMany(p => p.Smscommunications)
                .HasForeignKey(d => d.SmscommunicationStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SMSCommunications_SMSCommuncationStatuses");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.LoginId, "IX_Users_Login").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.AddedBy).HasDefaultValueSql("(NULL)");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.AddressLandMark)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.AddressLine1)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.AddressLine2)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.City)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.CorpRegNo)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CorporateName)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Country)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Dob)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("DOB");
            entity.Property(e => e.EmailId)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("EmailID");
            entity.Property(e => e.FirstName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Gender)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.LoginId)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("LoginID");
            entity.Property(e => e.MiddleName)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MobileNumber)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.MustChangePassword).HasDefaultValue(false);
            entity.Property(e => e.PassportNo)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasDefaultValueSql("(NULL)");
            entity.Property(e => e.Password)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PasswordLastChangedOn).HasColumnType("datetime");
            entity.Property(e => e.State)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.Zipcode)
                .HasMaxLength(6)
                .IsUnicode(false)
                .HasDefaultValueSql("(NULL)")
                .HasColumnName("ZIPCode");
        });

        modelBuilder.Entity<UserCommunication>(entity =>
        {
            entity.Property(e => e.UserCommunicationId).HasColumnName("UserCommunicationID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.CommunicationCreatedOn).HasColumnType("datetime");
            entity.Property(e => e.CommunicationTypeId).HasColumnName("CommunicationTypeID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.CommunicationType).WithMany(p => p.UserCommunications)
                .HasForeignKey(d => d.CommunicationTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserCommunications_CommunicationTypes");

            entity.HasOne(d => d.User).WithMany(p => p.UserCommunications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserCommunications_Users");
        });

        modelBuilder.Entity<UserCommunicationParameter>(entity =>
        {
            entity.HasKey(e => new { e.UserCommunicationId, e.CommunicationTypeParameterId });

            entity.Property(e => e.UserCommunicationId).HasColumnName("UserCommunicationID");
            entity.Property(e => e.CommunicationTypeParameterId).HasColumnName("CommunicationTypeParameterID");
            entity.Property(e => e.UserCommunicationParameterValue)
                .HasMaxLength(8000)
                .IsUnicode(false);

            entity.HasOne(d => d.CommunicationTypeParameter).WithMany(p => p.UserCommunicationParameters)
                .HasForeignKey(d => d.CommunicationTypeParameterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserCommu__Commu__4A4E069C");

            entity.HasOne(d => d.UserCommunication).WithMany(p => p.UserCommunicationParameters)
                .HasForeignKey(d => d.UserCommunicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserCommunicationParameters_UserCommunications");
        });

        modelBuilder.Entity<UserCommunicationsDatum>(entity =>
        {
            entity.HasKey(e => new { e.UserCommunicationId, e.CommunicationTypeDataFieldId }).HasName("PK_UserCommunicationsData_1");

            entity.HasIndex(e => new { e.UserCommunicationId, e.CommunicationTypeDataFieldId }, "IX_UserCommunicationsData").IsUnique();

            entity.Property(e => e.UserCommunicationId).HasColumnName("UserCommunicationID");
            entity.Property(e => e.CommunicationTypeDataFieldId).HasColumnName("CommunicationTypeDataFieldID");
            entity.Property(e => e.UserCommunicationData)
                .HasMaxLength(8000)
                .IsUnicode(false);

            entity.HasOne(d => d.CommunicationTypeDataField).WithMany(p => p.UserCommunicationsData)
                .HasForeignKey(d => d.CommunicationTypeDataFieldId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserCommunicationsData_CommunicationTypeDataFields");

            entity.HasOne(d => d.UserCommunication).WithMany(p => p.UserCommunicationsData)
                .HasForeignKey(d => d.UserCommunicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserCommunicationsData_UserCommunications");
        });

        modelBuilder.Entity<UserRelation>(entity =>
        {
            entity.Property(e => e.UserRelationId)
                .ValueGeneratedNever()
                .HasColumnName("UserRelationID");
            entity.Property(e => e.UserRelationDescription)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.UserRelationName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_UserRoles_IsActive");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Roles");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Users");
        });

        modelBuilder.Entity<UserRoleStatus>(entity =>
        {
            entity.HasKey(e => e.UserRoleStatusId).HasName("PK_UserRoleStatus");

            entity.Property(e => e.UserRoleStatusId)
                .ValueGeneratedNever()
                .HasColumnName("UserRoleStatusID");
            entity.Property(e => e.UserRoleStatus1)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("UserRoleStatus");
        });

        modelBuilder.Entity<UserRolesHistory>(entity =>
        {
            entity.HasKey(e => e.UserRoleHistoryId);

            entity.ToTable("UserRolesHistory");

            entity.Property(e => e.UserRoleHistoryId).HasColumnName("UserRoleHistoryID");
            entity.Property(e => e.ModifiedOn).HasColumnType("datetime");
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.UserRoleStatusId).HasColumnName("UserRoleStatusID");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRolesHistories)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRolesHistory_Roles");

            entity.HasOne(d => d.User).WithMany(p => p.UserRolesHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRolesHistory_Users");

            entity.HasOne(d => d.UserRoleStatus).WithMany(p => p.UserRolesHistories)
                .HasForeignKey(d => d.UserRoleStatusId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRolesHistory_UserRoleStatuses");
        });

        modelBuilder.Entity<VAccesstracking>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("V_ACCESSTRACKING");

            entity.Property(e => e.AccessOn).HasColumnType("datetime");
            entity.Property(e => e.City)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Country)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Ip)
                .HasMaxLength(15)
                .IsUnicode(false)
                .HasColumnName("IP");
            entity.Property(e => e.Organization)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Region)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Url)
                .HasMaxLength(150)
                .IsUnicode(false)
                .HasColumnName("URL");
            entity.Property(e => e.YearMonth)
                .HasMaxLength(33)
                .IsUnicode(false)
                .HasColumnName("Year-Month");
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.Property(e => e.VendorId).HasColumnName("VendorID");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Vendors_IsActive");
            entity.Property(e => e.Mobile)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Remarks)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.VendorAddress)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.VendorName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VendorsUser>(entity =>
        {
            entity.HasKey(e => e.VendorUserId);

            entity.Property(e => e.VendorUserId).HasColumnName("VendorUserID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_VendorsUsers_IsActive");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.VendorId).HasColumnName("VendorID");

            entity.HasOne(d => d.User).WithMany(p => p.VendorsUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VendorsUsers_Users");

            entity.HasOne(d => d.Vendor).WithMany(p => p.VendorsUsers)
                .HasForeignKey(d => d.VendorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VendorsUsers_Vendors");
        });

        modelBuilder.Entity<WishList>(entity =>
        {
            entity.HasKey(e => new { e.CustomerId, e.ProductId });

            entity.ToTable("WishList");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.AddedOn).HasColumnType("datetime");

            entity.HasOne(d => d.Customer).WithMany(p => p.WishLists)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WishList_Customers");

            entity.HasOne(d => d.Product).WithMany(p => p.WishLists)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WishList_Products");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
