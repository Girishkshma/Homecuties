using HC.Business;
using HC.Data;
using HC.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure DbContext.
// The active database is selected by "Database:Provider" and can be switched without a rebuild
// from any configuration source, e.g.:
//   appsettings.json                 -> "Database": { "Provider": "Remote" }
//   launch profile (F5 / dotnet run)  -> dotnet run --launch-profile Local
//   environment variable              -> $env:Database__Provider = 'Local'
//   command line argument             -> dotnet run --Database:Provider=Local
var connectionName = DatabaseConnectionSelector.ResolveConnectionName(builder.Configuration);
var usingLegacyConnectionName = connectionName == DatabaseConnectionSelector.LegacyConnectionName;
var connectionString = builder.Configuration.GetConnectionString(connectionName)
    ?? throw new InvalidOperationException(
        $"Connection string '{connectionName}' is not configured. Add it under 'ConnectionStrings' " +
        $"in appsettings.json or point '{DatabaseConnectionSelector.ProviderConfigurationKey}' at one of the " +
        $"existing entries ({string.Join(", ", builder.Configuration.GetSection("ConnectionStrings").GetChildren().Select(section => section.Key))}).");

builder.Services.AddDbContext<HomecutiesDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register Business Services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IGoogleLoginService, GoogleLoginService>();
builder.Services.AddScoped<IFacebookLoginService, FacebookLoginService>();
builder.Services.AddScoped<IUtilitiesService, UtilitiesService>();
builder.Services.AddScoped<IWishListService, WishListService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Log which connection string is active (credentials are never logged).
var connectionInfo = new SqlConnectionStringBuilder(connectionString);
app.Logger.LogInformation(
    "Active database connection: '{ConnectionName}' ({DataSource} / {InitialCatalog}).",
    connectionName,
    connectionInfo.DataSource,
    connectionInfo.InitialCatalog);

if (usingLegacyConnectionName)
{
    app.Logger.LogWarning(
        "appsettings.json only defines the legacy '{LegacyConnection}'. Define '{LocalConnection}' and " +
        "'{RemoteConnection}' and set '{ProviderKey}' to 'Local' or 'Remote' to switch between them.",
        DatabaseConnectionSelector.LegacyConnectionName,
        DatabaseConnectionSelector.LocalConnectionName,
        DatabaseConnectionSelector.RemoteConnectionName,
        DatabaseConnectionSelector.ProviderConfigurationKey);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");

// Serve uploaded images (e.g., /images/products/...) from wwwroot,
// creating the upload folder if it does not exist yet.
var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var productImagesPath = Path.Combine(wwwrootPath, "images", "products");
Directory.CreateDirectory(productImagesPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wwwrootPath),
    RequestPath = ""
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
