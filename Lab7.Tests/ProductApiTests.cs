using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Lab7.Tests;
//for commit on feature
//for commit on main
public class ProductApiTests : IAsyncLifetime
{
    private INetwork _network = null!;
    private PostgreSqlContainer _postgres = null!;
    private IContainer _apiContainer = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _network = new NetworkBuilder()
            .WithName("lab7-network-" + Guid.NewGuid().ToString("D"))
            .Build();

        await _network.CreateAsync();

        _postgres = new PostgreSqlBuilder()
            .WithNetwork(_network)
            .WithNetworkAliases("db")
            .WithDatabase("lab7db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgres.StartAsync();

        var connectionString = "Host=db;Port=5432;Database=lab7db;Username=postgres;Password=postgres";
        var dockerfileDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Lab7.Api"));

        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(dockerfileDirectory)
            .WithDockerfile("Dockerfile")
            .Build();

        await image.CreateAsync();

        _apiContainer = new ContainerBuilder()
            .WithImage(image)
            .WithNetwork(_network)
            .WithPortBinding(8080, true)
            .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:8080")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("UseInMemoryDatabase", "false")
            .WithEnvironment("ConnectionStrings__Default", connectionString)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(8080)
                    .ForPath("/api/products")))
            .Build();

        await _apiContainer.StartAsync();

        var apiPort = _apiContainer.GetMappedPublicPort(8080);
        _client = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{apiPort}")
        };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        if (_apiContainer != null)
            await _apiContainer.DisposeAsync().AsTask();
        if (_postgres != null)
            await _postgres.DisposeAsync().AsTask();
        if (_network != null)
            await _network.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task GetAll_ReturnsOk_AndProducts()
    {
        // Act
        var response = await _client.GetAsync("/api/products");
        var products = await response.Content.ReadFromJsonAsync<List<Product>>();

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        products.ShouldNotBeNull();
        products.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedProduct()
    {
        // Arrange
        var request = new CreateProductRequest($"API-Test-{Guid.NewGuid():N}"[..18], 123.45m);

        // Act
        var createResponse = await _client.PostAsJsonAsync("/api/products", request);
        var created = await createResponse.Content.ReadFromJsonAsync<Product>();
        var getResponse = await _client.GetAsync($"/api/products/{created!.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<Product>();

        // Assert
        createResponse.IsSuccessStatusCode.ShouldBeTrue();
        getResponse.IsSuccessStatusCode.ShouldBeTrue();
        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(created.Id);
        fetched.Name.ShouldBe(request.Name);
        fetched.Price.ShouldBe(request.Price);
    }

    [Fact]
    public async Task Search_ReturnsOk_AndMatchingProducts()
    {
        // Arrange
        var seed = await _client.GetFromJsonAsync<List<Product>>("/api/products");
        seed.ShouldNotBeNull();
        var term = seed[0].Name.Split('-')[0];

        // Act
        var response = await _client.GetAsync($"/api/products/search?q={term}");
        var results = await response.Content.ReadFromJsonAsync<List<Product>>();

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        results.ShouldNotBeNull();
        results.Count.ShouldBeGreaterThan(0);
        results.All(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }
}
