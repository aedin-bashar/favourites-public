using Microsoft.AspNetCore.Authentication.Cookies;
using Favourites.Api.ExceptionHandling;
using Favourites.Application;
using Favourites.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

const string AngularDevelopmentCorsPolicy = "AngularDevelopment";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ClientAbortActionFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<ClientAbortActionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;
});
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ForgotPassword", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(15);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevelopmentCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseClientAbortExceptionHandling();
app.UseForwardedHeaders();
app.Use((context, next) =>
{
    if (TryGetForwardedPrefix(context.Request, out var pathBase))
    {
        context.Request.PathBase = pathBase;
    }

    return next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseCors(AngularDevelopmentCorsPolicy);
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static bool TryGetForwardedPrefix(HttpRequest request, out PathString pathBase)
{
    pathBase = PathString.Empty;

    if (!request.Headers.TryGetValue("X-Forwarded-Prefix", out var values))
    {
        return false;
    }

    var prefix = values.ToString().Split(',', 2)[0].Trim().TrimEnd('/');

    if (string.IsNullOrWhiteSpace(prefix) ||
        !prefix.StartsWith("/", StringComparison.Ordinal) ||
        prefix.Contains("://", StringComparison.Ordinal) ||
        prefix.Contains('?') ||
        prefix.Contains('#'))
    {
        return false;
    }

    pathBase = new PathString(prefix);
    return true;
}

public partial class Program;
