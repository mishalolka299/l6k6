using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lab7.Tests;

public class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("lab7db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync().AsTask();
    }

    public DbContextOptions<AppDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
