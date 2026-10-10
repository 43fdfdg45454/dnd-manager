using OpenTrpg.Core.Api.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog.Events;

namespace OpenTrpg.Core.Api.Tests.Releases;

public class LoggingSetupTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Default_level_is_Information_without_configuration()
    {
        Assert.Equal(LogEventLevel.Information, LoggingSetup.ResolveDefaultLevel(Config()));
    }

    [Theory]
    [InlineData("Trace", LogEventLevel.Verbose)]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("information", LogEventLevel.Information)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("Critical", LogEventLevel.Fatal)]
    [InlineData("None", LogEventLevel.Fatal)]
    public void The_standard_logging_section_sets_the_default_level(string name, LogEventLevel expected)
    {
        // This is what docker compose passes as Logging__LogLevel__Default.
        Assert.Equal(expected, LoggingSetup.ResolveDefaultLevel(Config(("Logging:LogLevel:Default", name))));
    }

    [Fact]
    public void The_serilog_section_wins_over_the_logging_section()
    {
        var configuration = Config(("Logging:LogLevel:Default", "Debug"), ("Serilog:MinimumLevel:Default", "Error"));

        Assert.Equal(LogEventLevel.Error, LoggingSetup.ResolveDefaultLevel(configuration));
    }

    [Fact]
    public void An_unknown_level_falls_back_to_Information()
    {
        Assert.Equal(LogEventLevel.Information, LoggingSetup.ResolveDefaultLevel(Config(("Logging:LogLevel:Default", "loud"))));
    }

    [Fact]
    public void Category_levels_come_from_the_logging_section_without_the_default()
    {
        var levels = LoggingSetup.ResolveCategoryLevels(Config(
            ("Logging:LogLevel:Default", "Debug"),
            ("Logging:LogLevel:Microsoft.AspNetCore", "Warning"),
            ("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Error")));

        Assert.Equal(2, levels.Count);
        Assert.Equal(LogEventLevel.Warning, levels["Microsoft.AspNetCore"]);
        Assert.Equal(LogEventLevel.Error, levels["Microsoft.EntityFrameworkCore"]);
    }
}
