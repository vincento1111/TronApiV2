using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TronApi.Data;
using TronApi.Endpoints;
using TronApi.Hubs;
using TronApi.Infrastructure;
using TronApi.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16 * 1024);
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

var configuredPath = builder.Configuration["Tron:DatabasePath"] ?? "App_Data/tron-city.db";
var databasePath = Path.GetFullPath(configuredPath, builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = databasePath, ForeignKeys = true, DefaultTimeout = 30
}.ToString();
builder.Services.AddDbContext<GameDb>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WriteCoordinator>();
builder.Services.AddSingleton<ConnectionRegistry>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<SessionService>();
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
builder.Services.AddScoped<IPasswordHasher<Player>, PasswordHasher<Player>>();
var keyDirectory = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(databasePath)!, "keys"));
builder.Services.AddDataProtection().PersistKeysToFileSystem(keyDirectory).SetApplicationName("TronCity");
builder.Services.AddAuthentication(SessionService.Scheme).AddCookie(SessionService.Scheme, options =>
{
    options.Cookie.Name = "TronCity.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = SessionService.Lifetime;
    options.SlidingExpiration = false;
    options.Events.OnValidatePrincipal = SessionService.Validate;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return context.Response.WriteAsJsonAsync(new { message = "Please sign in." });
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return context.Response.WriteAsJsonAsync(new { message = "You cannot access this resource." });
    };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "TronCity.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddSignalR(options => { options.MaximumReceiveMessageSize = 4096; options.EnableDetailedErrors = false; });
builder.Services.AddRateLimiter(options =>
{
    var authPermitLimit = builder.Environment.IsDevelopment()
        ? Math.Clamp(builder.Configuration.GetValue<int?>("Tron:AuthPermitLimit") ?? 30, 1, 1000) : 30;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("chat", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many requests. Please wait a minute." }, ct);
    };
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    try { await next(context); }
    catch (ApiException ex) { context.Response.StatusCode = ex.Status; await context.Response.WriteAsJsonAsync(new { message = ex.Message }); }
    catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Your security token expired. Refresh and try again." }); }
    catch (BadHttpRequestException ex) { context.Response.StatusCode = ex.StatusCode; await context.Response.WriteAsJsonAsync(new { message = "The request could not be read. Check the submitted fields." }); }
    catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { message = "This change conflicts with existing data. Refresh and try again." });
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Request failed");
        if (context.Response.HasStarted) throw;
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { message = "Something went wrong. Please try again." });
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);

    if (context.Request.Path.StartsWithSegments("/hubs"))
    {
        var origin = context.Request.Headers.Origin.ToString();
        var sameOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
        var allowed = app.Configuration.GetSection("Tron:AllowedOrigins").Get<string[]>() ?? [];
        // Browser WebSockets always send Origin. Requiring it closes cross-site socket hijacking.
        if (string.IsNullOrEmpty(origin) || !(string.Equals(origin, sameOrigin, StringComparison.OrdinalIgnoreCase) ||
            allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)))
            throw new ApiException(403, "This chat origin is not allowed.");
    }
    await next(context);
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", application = "TronCity" })).AllowAnonymous();
app.MapAuth();
app.MapGame();
app.MapChat();
app.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsJsonAsync(new { message = "Endpoint not found." });
        return;
    }
    var index = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
    if (File.Exists(index))
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(index);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Tron City API is ready. Build the frontend or open the development frontend.");
    }
});

if (app.Configuration.GetValue<bool>("Tron:SeedDemo") && !app.Environment.IsDevelopment())
    throw new InvalidOperationException("Demo accounts may only be seeded in Development.");
await DatabaseInitializer.Initialize(app.Services, app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Tron:SeedDemo"));
app.Run();

public partial class Program;
