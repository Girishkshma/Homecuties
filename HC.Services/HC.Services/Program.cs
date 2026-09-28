using HC.Business;
using HC.Business.Security;
using HC.Data;
using HC.Services;
using HC.Services.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configuration sources.
// Secrets must never be committed, so 'appsettings.json' only holds development/test values.
// 'appsettings.Local.json' (git-ignored - see .gitignore) is layered on top of it and is where the
// LIVE Razorpay keys ('rzp_live_...') and the webhook secret belong on the server, so a publish can
// never silently drop the deployment back to test mode. Environment variables
// ('Razorpay__KeyId', 'Razorpay__KeySecret', 'Razorpay__WebhookSecret') and command line arguments
// still win over both files.
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

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

// ---------------------------------------------------------------------------
// Admin authentication and authorization.
//
// Logging in ('POST /api/admin/login') issues a signed JWT that carries the user id, the login id,
// the roles and the login time/expiry. Every other admin endpoint is protected with [Authorize]:
// the token has to be sent on every call in the 'Authorization: Bearer <token>' header, and it is
// validated here (HS256 signature, issuer, audience, expiry) before an action is allowed to run.
//
// Beside being authenticated, an admin needs a role that is mapped to the section being called -
// the mapping lives in 'AdminMenusRoles' (role -> menu, the same data the navigation is built from),
// so the sections each admin can open are administrated in the database.
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the claim names exactly as they were issued ('userId', 'role', ...) instead of
        // rewriting them to the legacy WS-Federation claim type URIs.
        options.MapInboundClaims = false;
        options.SaveToken = true;
        options.TokenValidationParameters = AdminJwtTokenService.CreateValidationParameters(builder.Configuration);
        options.Events = new JwtBearerEvents
        {
            // The signature only proves who signed in; the account itself is checked on every request
            // so deactivating an admin takes effect immediately instead of after the token expires.
            OnTokenValidated = async context =>
            {
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<HomecutiesDbContext>();
                var userId = AdminJwtTokenService.GetUserId(context.Principal);

                if (userId <= 0)
                {
                    context.Fail("The access token does not identify an admin user.");
                    return;
                }

                var isActiveAdmin = await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.UserId == userId && u.IsActive == true);

                if (!isActiveAdmin)
                    context.Fail("The admin account is no longer active.");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Dashboard and navigation: the admin needs access to at least one section of the admin area.
    options.AddPolicy(AdminPolicies.AdminArea, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new AdminMenuAccessRequirement()));

    // One policy per section combination, named after the menu URL(s) it grants.
    foreach (var policyName in AdminPolicies.SectionPolicies)
    {
        var sections = AdminPolicies.SectionsOf(policyName);
        options.AddPolicy(policyName, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new AdminMenuAccessRequirement(sections)));
    }
});

builder.Services.AddScoped<IAuthorizationHandler, AdminMenuAccessHandler>();

var app = builder.Build();

// Log the JWT settings (never the key itself) so a mismatch between the deployed API and the token
// it issues is obvious at a glance.
var jwtIssuer = AdminJwtTokenService.GetIssuer(builder.Configuration);
var jwtAudience = AdminJwtTokenService.GetAudience(builder.Configuration);
var jwtExpiryMinutes = AdminJwtTokenService.GetExpiryMinutes(builder.Configuration);
app.Logger.LogInformation(
    "Admin JWT authentication: issuer '{Issuer}', audience '{Audience}', sessions expire after {ExpiryMinutes} minute(s).",
    jwtIssuer,
    jwtAudience,
    jwtExpiryMinutes);

if (AdminJwtTokenService.IsWeakKey(AdminJwtTokenService.GetSigningKey(builder.Configuration)))
{
    app.Logger.LogWarning(
        "The JWT signing key is shorter than {RecommendedBytes} bytes. Set a long, random 'Jwt:Key' " +
        "(at least 32 characters) in appsettings.Production.json or appsettings.Local.json before deploying.",
        AdminJwtTokenService.RecommendedKeyLengthInBytes);
}

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

// Log whether the payment gateway is configured AND which mode it is in. Test vs live is decided
// entirely by the key handed to Razorpay Checkout: 'rzp_test_...' makes the payment window show the
// "Test Mode" banner and no money moves, 'rzp_live_...' takes real payments. The key id is public
// (it ships to the browser with every checkout); the secret is never logged.
var razorpayKeyId = builder.Configuration["Razorpay:KeyId"] ?? string.Empty;
var razorpaySecretConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Razorpay:KeySecret"]);

if (string.IsNullOrWhiteSpace(razorpayKeyId) || !razorpaySecretConfigured)
{
    app.Logger.LogWarning(
        "Razorpay is NOT configured: set 'Razorpay:KeyId' and 'Razorpay:KeySecret' (or the " +
        "environment variables 'Razorpay__KeyId' / 'Razorpay__KeySecret') - online payments will " +
        "be refused with 'Online payment is not available right now.'");
}
else if (razorpayKeyId.StartsWith("rzp_test_", StringComparison.Ordinal))
{
    app.Logger.LogInformation(
        "Razorpay mode: TEST (key id {KeyId}) - the checkout window shows 'Test Mode' and no real payment is taken.",
        razorpayKeyId);

    if (!app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning(
            "Razorpay TEST keys are configured for the '{Environment}' environment, so customers cannot " +
            "pay. Put the LIVE keys ('rzp_live_...') in 'appsettings.Local.json' or in the environment " +
            "variables 'Razorpay__KeyId' / 'Razorpay__KeySecret'.",
            app.Environment.EnvironmentName);
    }
}
else if (razorpayKeyId.StartsWith("rzp_live_", StringComparison.Ordinal))
{
    app.Logger.LogInformation(
        "Razorpay mode: LIVE (key id {KeyId}) - real payments are being collected.", razorpayKeyId);

    if (app.Environment.IsDevelopment())
    {
        app.Logger.LogWarning(
            "Razorpay LIVE keys are in use on a development machine - every test checkout charges real money.");
    }
}
else
{
    app.Logger.LogInformation("Razorpay key id: {KeyId} (secret configured: true)", razorpayKeyId);
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Razorpay:WebhookSecret"]))
{
    app.Logger.LogWarning(
        "Razorpay webhook secret is not configured ('Razorpay:WebhookSecret') - every delivery to " +
        "'POST /api/Order/Webhook' is rejected, so orders are only confirmed through the checkout " +
        "callback. Add the endpoint in Razorpay Dashboard > Account & Settings > Webhooks and copy " +
        "its secret into 'appsettings.Local.json' or the environment variable 'Razorpay__WebhookSecret'.");
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

// Admin requests carry a signed JWT: authenticate (validate the token) first, then authorize
// (check the roles/sections it grants).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
