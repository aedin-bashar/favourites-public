using Favourites.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Favourites.IntegrationTests;

public sealed class FavouritesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"favourites-tests-{Guid.NewGuid()}";
    private readonly List<string> _logs = new();

    public IReadOnlyList<string> Logs => _logs;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(new CapturingLoggerProvider(_logs));
        });

        builder.ConfigureTestServices(services =>
        {
            var dbContextOptions = services.SingleOrDefault(
                descriptor => descriptor.ServiceType == typeof(DbContextOptions<FavouritesDbContext>));

            if (dbContextOptions is not null)
            {
                services.Remove(dbContextOptions);
            }

            var inMemoryEfServices = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<FavouritesDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .UseInternalServiceProvider(inMemoryEfServices));
        });
    }
}

internal sealed class CapturingLoggerProvider(List<string> sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, sink);
    public void Dispose() { }
}

internal sealed class CapturingLogger(string category, List<string> sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var line = $"[{logLevel}] {category}: {formatter(state, exception)}";
        if (exception is not null) line += "\n" + exception;
        lock (sink) sink.Add(line);
    }
}
