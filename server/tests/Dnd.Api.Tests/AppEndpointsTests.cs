using System.Net;
using System.Net.Http.Json;
using Dnd.Api.Endpoints;

namespace Dnd.Api.Tests;

public class AppEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public AppEndpointsTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Liveness_endpoint_returns_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task App_info_returns_name_and_semantic_version()
    {
        var info = await _client.GetFromJsonAsync<AppInfoResponse>("/api/v1/app/info");

        Assert.NotNull(info);
        Assert.Equal("dnd-companion-api", info.Name);
        Assert.Matches(@"^\d+\.\d+\.\d+$", info.Version);
    }

    [Fact]
    public async Task Swagger_document_is_served()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
