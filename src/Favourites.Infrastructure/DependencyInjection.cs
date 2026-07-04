using Favourites.Application.Abstractions.Email;
using Favourites.Application.Abstractions.Identity;
using Favourites.Application.Abstractions.Persistence;
using Favourites.Application.Auth.ForgotPassword;
using Favourites.Application.Auth.ResetPassword;
using Favourites.Infrastructure.Email;
using Favourites.Infrastructure.Identity;
using Favourites.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Favourites.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddTransient<IEmailSender, QueuedEmailSender>();
        services.AddHostedService<EmailQueueProcessor>();

        services.AddDbContext<FavouritesDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IFavouritesDbContext>(sp => sp.GetRequiredService<FavouritesDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddEntityFrameworkStores<FavouritesDbContext>()
            .AddSignInManager();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IUserAccountDeletionService, UserAccountDeletionService>();

        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();

        return services;
    }
}
