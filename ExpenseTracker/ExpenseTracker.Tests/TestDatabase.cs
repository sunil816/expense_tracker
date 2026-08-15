using System;
using System.Threading.Tasks;
using ExpenseTracker.Data;
using ExpenseTracker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Tests;

internal sealed class TestDatabase(SqliteConnection connection, ServiceProvider services) : IAsyncDisposable
{
    public ServiceProvider Services { get; } = services;

    public static Task<TestDatabase> CreateAsync() => CreateAsync(TimeProvider.System);

    public static async Task<TestDatabase> CreateAsync(TimeProvider timeProvider)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = ON";
            await command.ExecuteNonQueryAsync();
        }

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContextFactory<ExpenseTrackerDbContext>(options => options.UseSqlite(connection));
        serviceCollection.AddScoped<DocumentImportService>();
        serviceCollection.AddScoped<ITransactionDuplicateService, TransactionDuplicateService>();
        serviceCollection.AddScoped<TransactionService>();
        serviceCollection.AddScoped<TagService>();
        serviceCollection.AddScoped<CategoryService>();
        serviceCollection.AddScoped<ReportService>();
        serviceCollection.AddSingleton(timeProvider);
        var services = serviceCollection.BuildServiceProvider();
        var database = new TestDatabase(connection, services);
        await using var context = await database.CreateContextAsync();
        await context.Database.EnsureCreatedAsync();
        return database;
    }

    public Task<ExpenseTrackerDbContext> CreateContextAsync() =>
        Services.GetRequiredService<IDbContextFactory<ExpenseTrackerDbContext>>().CreateDbContextAsync();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await connection.DisposeAsync();
    }
}
