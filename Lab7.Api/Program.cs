using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoFixture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useInMemoryDatabase)
    {
        options.UseInMemoryDatabase("lab7db");
        return;
    }

    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();

public record Product(int Id, string Name, decimal Price);

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
            entity.HasIndex(p => p.Name);
        });
    }
}

public class ProductEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, int targetCount = 10000, CancellationToken cancellationToken = default)
    {
        var existingCount = await db.Products.CountAsync(cancellationToken);
        if (existingCount >= targetCount)
            return;

        var fixture = new Fixture();
        var itemsToAdd = targetCount - existingCount;

        var products = Enumerable.Range(existingCount + 1, itemsToAdd)
            .Select(i =>
            {
                var suffix = fixture.Create<Guid>().ToString("N")[..8];
                var rawPrice = Math.Abs(fixture.Create<decimal>());
                var price = decimal.Round((rawPrice % 1000m) + 1m, 2);

                return fixture.Build<ProductEntity>()
                    .Without(p => p.Id)
                    .With(p => p.Name, $"Product-{i:00000}-{suffix}")
                    .With(p => p.Price, price)
                    .Create();
            })
            .ToList();

        db.Products.AddRange(products);
        await db.SaveChangesAsync(cancellationToken);
    }
}

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await Task.Delay(50);
        var products = await _db.Products.Take(100).ToListAsync();
        return Ok(products.Select(p => new Product(p.Id, p.Name, p.Price)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        await Task.Delay(20);
        var product = await _db.Products.FindAsync(id);
        return product is null ? NotFound() : Ok(new Product(product.Id, product.Name, product.Price));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        await Task.Delay(30);
        var product = new ProductEntity { Name = request.Name, Price = request.Price };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, new Product(product.Id, product.Name, product.Price));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        await Task.Delay(200);
        var results = await _db.Products.Where(p => p.Name.Contains(q)).Take(50).ToListAsync();
        return Ok(results.Select(p => new Product(p.Id, p.Name, p.Price)));
    }
}

public record CreateProductRequest(string Name, decimal Price);

public partial class Program;
