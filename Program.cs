using apiship.Data;
using apiship.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    // Everything under /Admin requires an authenticated admin, except the login page.
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AllowAnonymousToPage("/Admin/Login");

    // The customer account area requires a signed-in customer, except sign-up / sign-in.
    options.Conventions.AuthorizeFolder("/Account", "CustomerOnly");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Register");
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=apiship.db"));
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddSingleton<IApiKeyGenerator, ApiKeyGenerator>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<IAppSettingsService, AppSettingsService>();
builder.Services.AddHttpClient<IPaystackService, PaystackService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        // Send admins to the admin login and customers to the account login.
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/Admin"))
            {
                ctx.RedirectUri = ctx.RedirectUri.Replace("/Account/Login", "/Admin/Login");
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/Admin"))
            {
                ctx.RedirectUri = ctx.RedirectUri.Replace("/Account/Login", "/Admin/Login");
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
});

var app = builder.Build();

// Apply any pending migrations, creating the database if needed. Using migrations
// (instead of EnsureCreated) means future schema changes preserve existing data.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// ---------------------------------------------------------------------------
// Public API service (authenticated by the X-Api-Key header).
// ---------------------------------------------------------------------------
var api = app.MapGroup("/api/v1");

// Unauthenticated health probe.
api.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// Returns the calling project's deployment info; validates the API key.
api.MapGet("/status", async (HttpContext ctx, AppDbContext db, IApiKeyGenerator keys) =>
{
    var presented = ctx.Request.Headers["X-Api-Key"].ToString();
    if (string.IsNullOrWhiteSpace(presented))
    {
        return Results.Json(new { error = "Missing X-Api-Key header." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var hash = keys.Hash(presented);
    var key = await db.ApiKeys
        .Include(k => k.Project!).ThenInclude(p => p.Customer)
        .FirstOrDefaultAsync(k => k.SecretHash == hash && !k.Revoked);

    if (key?.Project is null)
    {
        return Results.Json(new { error = "Invalid or revoked API key." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    key.LastUsedUtc = DateTime.UtcNow;
    key.CallCount++;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        project = key.Project.Name,
        status = key.Project.Status.ToString(),
        plan = key.Project.Plan,
        siteUrl = key.Project.SiteUrl,
        customer = key.Project.Customer?.FullName
    });
});

// Paystack webhook: activates a customer's subscription on charge.success.
app.MapPost("/api/paystack/webhook", async (HttpContext ctx, AppDbContext db,
    IAppSettingsService settingsService, IPaystackService paystack) =>
{
    var settings = await settingsService.GetAsync();
    if (!settings.HasPaystackKey)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    ctx.Request.EnableBuffering();
    using var reader = new StreamReader(ctx.Request.Body, leaveOpen: true);
    var body = await reader.ReadToEndAsync();
    ctx.Request.Body.Position = 0;

    var signature = ctx.Request.Headers["x-paystack-signature"].ToString();
    if (!paystack.VerifySignature(settings.PaystackSecretKey!, body, signature))
    {
        return Results.Unauthorized();
    }

    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        var evt = root.GetProperty("event").GetString();
        if (evt == "charge.success")
        {
            var reference = root.GetProperty("data").GetProperty("reference").GetString();
            if (!string.IsNullOrEmpty(reference))
            {
                await SubscriptionActivator.ActivateAsync(db, reference);
            }
        }
    }
    catch
    {
        // Malformed payload — acknowledge so Paystack does not retry indefinitely.
    }

    return Results.Ok();
});

app.Run();
