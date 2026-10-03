using HC.Business;
using HC.Business.Security;
using HC.Business.Shipping;
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

// PIN code lookup: the address forms send a 6-digit PIN code here and get back the city, the state and
// the areas it covers, so the customer does not have to type them ('Customer/GetPincode'). The base
// address comes from configuration ('Pincode:BaseUrl') so a mirror or a paid provider can take over
// without a rebuild, and the timeout is short because a customer is waiting in front of the field.
var pincodeBaseUrl = builder.Configuration["Pincode:BaseUrl"] ?? "https://api.postalpincode.in/";
builder.Services.AddHttpClient<IPincodeService, PincodeService>(client =>
{
    client.BaseAddress = new Uri(pincodeBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
});

// Shipping: the providers that carry the parcels of an order. One adapter per provider, registered here
// and resolved through IShipmentProviderRegistry so nothing above the adapters names one of them - a
// second provider is one more adapter and one more line here, no screen, no order rule and no database
// column changes. Which provider is used is configuration ('Shipping:DefaultProvider'), and a parcel
// remembers the one it was booked with ('OrderShipments.Provider'), so two providers can be used side by
// side.
//
// The two registered are the two kinds the shop has: Shiprocket, an aggregator whose panel books the
// consignment and hands back the AWB, and Custom - the service the shop arranges itself (a parcel handed
// over in person, or given to a local courier dealt with by phone). The second has no panel, no
// credentials and no tracking behind it, so the reference written on its parcels is minted here and it is
// never asked where a parcel is (see CustomShipmentProvider).
builder.Services.AddHttpClient<IShipmentProvider, ShiprocketShipmentProvider>(client =>
{
    // A customer is watching 'My Orders' while this runs, so a slow courier must not hold the page:
    // the adapter reports the failure and the page carries on (the same rule the PIN code lookup keeps).
    client.Timeout = TimeSpan.FromSeconds(10);
});

// The shop's own service, registered second: it holds no state and talks to nobody, so it is a singleton
// (and always 'configured' - there is nothing to set up). Registered after the aggregator so that the
// 'first provider that is configured' fallback still favours the shop's courier when no default is named
// in configuration, which is what a shop that simply has not set 'Shipping:DefaultProvider' expects.
builder.Services.AddSingleton<IShipmentProvider, CustomShipmentProvider>();
builder.Services.AddScoped<IShipmentProviderRegistry, ShipmentProviderRegistry>();
builder.Services.AddScoped<IShipmentTrackingService, ShipmentTrackingService>();

// The rolling settlement pull: the shop's books are exactly as old as the last time Razorpay was asked what it
// settled to the bank account, so this asks by itself - the same pull the admin area can ask for by hand
// ('Pull now' on the dashboard, POST api/admin/settlements/sync). It re-reads the same few days every pass
// (yesterday and the seven days before it), which is what catches a settlement created late, put on hold or
// corrected after the transaction it covers, and it is idempotent, so re-reading a day costs one request and
// nothing else. When it runs and how often are configuration - see HC.Business/SettlementSyncSchedule, where
// the settings are read, and 'Razorpay:SettlementSync' in appsettings.json, where they are documented; setting
// 'Enabled' to false stops it and leaves every pull to the screen.
builder.Services.AddHostedService<SettlementSyncJob>();

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

// Log which shipping providers the shop's parcels go out with and whether they can actually be used:
// without credentials no screen can track a parcel, and that has to be visible at startup rather than
// discovered by a customer tapping 'Track parcel'. A provider that carries parcels without any tracking
// behind it says so too - it is not misconfigured, it is simply not something to ask about.
using (var shippingScope = app.Services.CreateScope())
{
    var shippingRegistry = shippingScope.ServiceProvider.GetRequiredService<IShipmentProviderRegistry>();

    foreach (var provider in shippingRegistry.Describe())
    {
        // A provider's id is what a parcel is recorded with ('OrderShipments.Provider') and how its
        // adapter is found again when the courier is asked, so one that no longer fits that column
        // would quietly track through the default provider instead - asking the wrong aggregator about
        // the AWB. Worth shouting about at startup rather than at the first customer's question.
        if (provider.Name.Length > ShipmentTrackingService.ProviderMaxLength)
        {
            app.Logger.LogError(
                "Shipping provider '{Provider}' has an id longer than the {MaxLength} characters " +
                "'OrderShipments.Provider' can hold, so parcels booked with it would be tracked through " +
                "the default provider instead. Give the adapter a shorter 'Name' (or widen the column) " +
                "before any parcel is booked with it.",
                provider.Name,
                ShipmentTrackingService.ProviderMaxLength);
        }

        if (provider.Configured && !provider.ReportsTracking)
        {
            app.Logger.LogInformation(
                "Shipping provider '{Provider}' is ready{DefaultMarker}: it carries parcels without a " +
                "courier behind it, so the shop mints each parcel's reference itself and the order is " +
                "moved along by hand.",
                provider.Name,
                provider.IsDefault ? " [default]" : string.Empty);
        }
        else if (provider.Configured)
        {
            app.Logger.LogInformation(
                "Shipping provider '{Provider}' is ready{DefaultMarker}: {ApiBaseUrl} (tracking '{TrackingPath}').",
                provider.Name,
                provider.IsDefault ? " [default]" : string.Empty,
                provider.ApiBaseUrl,
                provider.TrackingPath);
        }
        else
        {
            app.Logger.LogWarning(
                "Shipping provider '{Provider}' is registered but NOT configured, so parcels recorded " +
                "against it cannot be tracked. Set its credentials (e.g. 'Shiprocket:Email' / " +
                "'Shiprocket:Password', or the environment variables 'Shiprocket__Email' / " +
                "'Shiprocket__Password') in the git-ignored 'appsettings.Local.json'.",
                provider.Name);
        }
    }

    // How long a parcel is left alone between two courier lookups: 'My Orders' refreshes the parcels
    // whose last check is older than this, so opening the page is not itself a courier call.
    app.Logger.LogInformation(
        "Shipping pull throttle: {ThrottleMinutes} minute(s) between courier lookups of the same " +
        "parcel (configure '{ThrottleKey}').",
        builder.Configuration[ShipmentTrackingService.SyncThrottleKey] ?? "15",
        ShipmentTrackingService.SyncThrottleKey);
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
