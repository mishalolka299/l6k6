using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Lab7.Tests;

[Collection("Database")]
public class SeedDataTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private AppDbContext? _db;

    public SeedDataTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var options = _fixture.CreateDbContextOptions();
        _db = new AppDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_db != null)
            await _db.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task Seed_10000_Products_ToDatabase()
    {
        // Arrange
        var existingCount = await _db!.Products.CountAsync();
        if (existingCount >= 10000) return;

        // Act
        var products = Enumerable.Range(1, 10000)
            .Select(i => new ProductEntity
            {
                Name = $"Product-{i:00000}-{Guid.NewGuid().ToString()[..8]}",
                Price = Random.Shared.Next(10, 10000) / 100m
            })
            .ToList();

        _db.Products.AddRange(products);
        await _db.SaveChangesAsync();

        // Assert
        var count = await _db.Products.CountAsync();
        count.ShouldBeGreaterThanOrEqualTo(10000);
    }

    [Fact]
    public async Task Products_AreAccessible_AfterSeed()
    {
        // Act
        var count = await _db!.Products.CountAsync();
        var sample = await _db.Products.Take(5).ToListAsync();

        // Assert
        count.ShouldBeGreaterThanOrEqualTo(10000);
        sample.Count.ShouldBe(5);
        sample.All(p => !string.IsNullOrEmpty(p.Name)).ShouldBeTrue();
    }

    [Fact]
    public async Task Search_Products_ByName_PartialMatch()
    {
        // Act
        var results = await _db!.Products
            .Where(p => p.Name.Contains("Product-001"))
            .Take(10)
            .ToListAsync();

        // Assert
        results.Count.ShouldBeGreaterThan(0);
        results.All(p => p.Name.Contains("Product-001")).ShouldBeTrue();
    }
}
