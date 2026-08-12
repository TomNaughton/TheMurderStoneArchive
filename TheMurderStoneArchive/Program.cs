using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.AspNetCore;
using TheMurderStoneArchive.Data;
using TheMurderStoneArchive.Helpers;
using TheMurderStoneArchive.HealthChecks;
using TheMurderStoneArchive.Middleware;
using TheMurderStoneArchive.Models;
using TheMurderStoneArchive.Services;
using TheMurderStoneArchive.Validators;

var builder = WebApplication.CreateBuilder(args);

// Load .env file into environment variables.
// In Development, prefer .env.local but fall back to .env.
var envCandidates = builder.Environment.IsDevelopment()
    ? new[] { ".env.local", ".env" }
    : new[] { ".env" };

var envPath = envCandidates
    .Select(file => Path.Combine(builder.Environment.ContentRootPath, file))
    .FirstOrDefault(File.Exists);

if (!string.IsNullOrWhiteSpace(envPath))
{
    foreach (var line in File.ReadLines(envPath))
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            continue;

        var parts = line.Split('=', 2);
        if (parts.Length == 2)
        {
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
}

// Load secrets from environment variables
// IMPORTANT: In production, set these environment variables:
//   - ConnectionStrings__DefaultConnection
//   - ReCaptcha__SiteKey
//   - ReCaptcha__SecretKey
// Never commit actual secrets to appsettings.Production.json
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    // Trust all proxies coming from the Docker network
    // Since we're inside a docker network and Cloudflare tunnel is our reverse proxy
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Accept forwarded headers from any IP on the docker network
    options.AllowedHosts = new[] { "*" };
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString(AppConstants.ConnectionStringKey)));

// Persist Data Protection keys to the database so they survive container restarts
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    // Keep email confirmation off for now so users can sign in immediately
    options.SignIn.RequireConfirmedAccount = false;

    // Explicit password policy: require a reasonable minimum length and at least
    // one digit for basic strength, while keeping UX simple (no forced casing/symbols).
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(AppConstants.StripeSection));
builder.Services.Configure<DonationOptions>(builder.Configuration.GetSection(AppConstants.DonationSection));

// Register application services
builder.Services.AddScoped<IMurderEventService, MurderEventService>();
builder.Services.AddScoped<IPdfDocumentService, PdfDocumentService>();
builder.Services.AddScoped<IStripePaymentService, StripePaymentService>();
builder.Services.AddScoped<IPatreonWebhookService, PatreonWebhookService>();
builder.Services.AddScoped<IFourthwallWebhookService, FourthwallWebhookService>();
builder.Services.AddScoped<IApiAuthenticationService, ApiAuthenticationService>();
builder.Services.AddScoped<IFourthwallApiSubscriptionService, FourthwallApiSubscriptionService>();

// Register FluentValidation
builder.Services.AddFluentValidationAutoValidation()
    .AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddScoped<IValidator<MurderEvent>, MurderEventValidator>();
builder.Services.AddScoped<IValidator<MurderEventPhoto>, MurderEventPhotoValidator>();
builder.Services.AddScoped<IValidator<MurderEventVideo>, MurderEventVideoValidator>();

// Register Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", 
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "ready" });

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application starting. Environment: {Environment}", app.Environment.EnvironmentName);

app.UseForwardedHeaders();

// Validate required configuration
var dbConnectionString = builder.Configuration.GetConnectionString(AppConstants.ConnectionStringKey);
if (string.IsNullOrEmpty(dbConnectionString))
{
    logger.LogError("Database connection string is missing. Set ConnectionStrings__DefaultConnection environment variable.");
}

var reCaptchaSecret = builder.Configuration[AppConstants.ReCaptchaSecretKeyKey];
if (string.IsNullOrEmpty(reCaptchaSecret))
{
    logger.LogWarning("ReCaptcha secret key is missing. Set ReCaptcha__SecretKey environment variable.");
}

var stripeSecretKey = builder.Configuration[$"{AppConstants.StripeSection}:SecretKey"];
if (string.IsNullOrEmpty(stripeSecretKey))
{
    logger.LogWarning("Stripe secret key is missing. Set Stripe__SecretKey environment variable.");
}

var stripePublishableKey = builder.Configuration[$"{AppConstants.StripeSection}:PublishableKey"];
if (string.IsNullOrEmpty(stripePublishableKey))
{
    logger.LogWarning("Stripe publishable key is missing. Set Stripe__PublishableKey environment variable.");
}

var stripeWebhookSecret = builder.Configuration[$"{AppConstants.StripeSection}:WebhookSecret"];
if (string.IsNullOrEmpty(stripeWebhookSecret))
{
    logger.LogWarning("Stripe webhook secret is missing. Set Stripe__WebhookSecret environment variable.");
}

var stripeProductTaxCode = builder.Configuration[$"{AppConstants.StripeSection}:ProductTaxCode"];
if (string.IsNullOrEmpty(stripeProductTaxCode))
{
    logger.LogWarning("Stripe product tax code is missing. Set Stripe__ProductTaxCode environment variable.");
}

var donationProvider = builder.Configuration[$"{AppConstants.DonationSection}:Provider"];
var patreonWebhookSecret = builder.Configuration[$"{AppConstants.DonationSection}:PatreonWebhookSecret"];
var patreonCampaignUrl = builder.Configuration[$"{AppConstants.DonationSection}:PatreonCampaignUrl"];
var patreonOneTimePaymentUrl = builder.Configuration[$"{AppConstants.DonationSection}:PatreonOneTimePaymentUrl"];
var fourthwallOneTimePaymentUrl = builder.Configuration[$"{AppConstants.DonationSection}:FourthwallOneTimePaymentUrl"];
var fourthwallSubscriptionUrl = builder.Configuration[$"{AppConstants.DonationSection}:FourthwallSubscriptionUrl"];
var fourthwallWebhookSecret = builder.Configuration[$"{AppConstants.DonationSection}:FourthwallWebhookSecret"];

if (string.Equals(donationProvider, "Patreon", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(patreonOneTimePaymentUrl) && string.IsNullOrWhiteSpace(patreonCampaignUrl))
    {
        logger.LogWarning("Patreon donation provider is enabled but no Patreon donation URL is configured. Set Donation__PatreonOneTimePaymentUrl (preferred) or Donation__PatreonCampaignUrl.");
    }

    if (string.IsNullOrWhiteSpace(patreonWebhookSecret))
    {
        logger.LogWarning("Patreon donation provider is enabled but webhook secret is missing. Set Donation__PatreonWebhookSecret environment variable.");
    }
}

if (string.Equals(donationProvider, "Fourthwall", StringComparison.OrdinalIgnoreCase))
{
    if (string.IsNullOrWhiteSpace(fourthwallOneTimePaymentUrl))
    {
        logger.LogWarning("Fourthwall donation provider is enabled but one-time payment URL is missing. Set Donation__FourthwallOneTimePaymentUrl environment variable.");
    }

    if (string.IsNullOrWhiteSpace(fourthwallWebhookSecret))
    {
        logger.LogWarning("Fourthwall donation provider is enabled but webhook secret is missing. Set Donation__FourthwallWebhookSecret environment variable.");
    }
}

if (string.IsNullOrWhiteSpace(fourthwallSubscriptionUrl))
{
    logger.LogWarning("Fourthwall donation provider is enabled but one-time payment URL is missing. Set Donation__fourthwallSubscriptionUrl environment variable.");
}

var fourthwallApiSubSecret = builder.Configuration[$"{AppConstants.DonationSection}:FourthwallApiSubscriptionWebhookSecret"];
if (string.IsNullOrWhiteSpace(fourthwallApiSubSecret))
{
    logger.LogWarning("Fourthwall API subscription webhook secret is not configured. Set Donation__FourthwallApiSubscriptionWebhookSecret to secure the subscription webhook endpoints.");
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
    logger.LogInformation("Database migration completed");

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync(AppConstants.AdminRole))
    {
        await roleManager.CreateAsync(new IdentityRole(AppConstants.AdminRole));
        logger.LogInformation("Admin role created");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Add security headers middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Map health check endpoint
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    status = kvp.Value.Status.ToString(),
                    description = kvp.Value.Description
                }
            ),
            timestamp = DateTime.UtcNow
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

// Liveness probe endpoint (checks if the app is running)
app.MapHealthChecks("/health/live");

// Readiness probe endpoint (checks if the app is ready to handle requests)
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = (check) => check.Tags.Contains("ready")
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();
app.Run();
