using System.Net;
using System.Net.Http.Json;
using Dnd.Api.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dnd.Api.Tests.Realtime;

/// <summary>A failing realtime transport never breaks the request that triggered the event.</summary>
public sealed class NotifierFailureTests(BrokenHubApiFactory factory) : IClassFixture<BrokenHubApiFactory>
{
    [Fact]
    public async Task A_failed_notification_is_logged_and_the_change_is_kept()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var response = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/stash/gold", new { deltaCp = 25 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(factory.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("party.stash.updated", StringComparison.Ordinal));
    }
}

/// <summary>API host whose SignalR hub context throws on every send.</summary>
public sealed class BrokenHubApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHubContext<CampaignHub>>();
            services.AddSingleton<IHubContext<CampaignHub>, BrokenHubContext>();
        });
    }

    private sealed class BrokenHubContext : IHubContext<CampaignHub>
    {
        public IHubClients Clients { get; } = new BrokenClients();

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class BrokenClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new BrokenProxy();

        public IClientProxy All => Proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Client(string connectionId) => Proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;

        public IClientProxy Group(string groupName) => Proxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;

        public IClientProxy User(string userId) => Proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class BrokenProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Transport down.");
    }
}
