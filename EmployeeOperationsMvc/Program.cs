using EmployeeOperationsMvc.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

 
// Keep existing builder creation and service registrations.
builder.Services.AddControllersWithViews();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-EmployeeOperations.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Home/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApprovedFrontendPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5500")
            .WithMethods("GET")
            .WithHeaders("Content-Type", "X-API-Key");
    });
});

builder.Services.AddAuthorization();
builder.Services.AddAuthorization();


//// Register MVC services.
builder.Services.AddMvc();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                error = "rate_limit_exceeded",
                message = "Too many requests. Wait before trying again."
            },
            cancellationToken);
    };

    options.AddFixedWindowLimiter("IntegrationPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
        limiterOptions.AutoReplenishment = true;
    });
});


var app = builder.Build();
//// Register early so headers can cover downstream responses.
//app.UseMiddleware<SecurityHeadersMiddleware>();

//// 1. Custom request logging.
//app.UseMiddleware<RequestLoggingMiddleware>();

//// 2. Custom execution-time measurement.
//app.UseMiddleware<RequestExecutionTimeMiddleware>();

//// 3. Built-in middleware: redirect HTTP requests to HTTPS.
//app.UseHttpsRedirection();

//// 4. Built-in middleware: serve files from wwwroot.
//app.UseStaticFiles();

//// 5. Built-in middleware: match requests to endpoints.
//app.UseRouting();

//// 6. Custom middleware: block dynamic requests during maintenance.
//app.UseMiddleware<MaintenanceModeMiddleware>();

//// 7. Custom middleware: intercept selected requests.
//app.UseMiddleware<ConditionalRequestMiddleware>();

//// 8. Built-in middleware: apply authorization policies.
//app.UseAuthorization();

//// 9. Map requests to MVC controllers and actions.
//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

//app.Run();
//using EmployeeOperationsMvc.Middleware;

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RequestExecutionTimeMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
// Endpoint metadata is available after UseRouting.
app.UseMiddleware<ApiKeyValidationMiddleware>();
app.UseMiddleware<IpRestrictionMiddleware>();
// Existing middleware from earlier labs.
app.UseMiddleware<MaintenanceModeMiddleware>();
app.UseMiddleware<ConditionalRequestMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");


app.Run();







