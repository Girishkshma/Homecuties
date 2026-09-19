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

// Log the configured Google client id (a public value, it also ships in the browser bundle) so a
// mismatch with the deployed storefront build is obvious at a glance.
var googleClientId = builder.Configuration["Google:ClientId"];
if (string.IsNullOrWhiteSpace(googleClientId))
{
    app.Logger.LogWarning(
        "Google sign-in is DISABLED: 'Google:ClientId' is not configured. Set it to the storefront's " +
        "OAuth client id to enable the 'Continue with Google' button.");
}
else
{
    app.Logger.LogInformation("Google sign-in client id: {ClientId}", googleClientId);
}

// Log whether the payment gateway is configured. The key id is public (it is sent to the browser
// with every checkout); the secret is never logged.
var razorpayKeyId = builder.Configuration["Razorpay:KeyId"];
var razorpaySecretConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Razorpay:KeySecret"]);
if (string.IsNullOrWhiteSpace(razorpayKeyId) || !razorpaySecretConfigured)
{
    app.Logger.LogWarning(
        "Razorpay is NOT configured: set 'Razorpay:KeyId' and 'Razorpay:KeySecret' (or the " +
        "environment variables 'Razorpay__KeyId' / 'Razorpay__KeySecret') - online payments will " +
        "be refused with 'Online payment is not available right now.'");
}
else
{
    app.Logger.LogInformation("Razorpay key id: {KeyId} (secret configured: true)", razorpayKeyId);
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Razorpay:WebhookSecret"]))
{
    app.Logger.LogWarning(
        "Razorpay webhook secret is not configured ('Razorpay:WebhookSecret') - webhook deliveries " +
        "will be rejected, so orders are only confirmed through the checkout callback.");
}
else
{
    app.Logger.LogInformation("Razorpay webhook endpoint: POST /api/Order/Webhook (signature validation on)");
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
