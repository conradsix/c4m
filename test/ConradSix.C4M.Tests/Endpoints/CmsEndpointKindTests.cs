#pragma warning disable SA1402 // The sample endpoint is kept with the tests that use it.
namespace ConradSix.C4M.Tests.Endpoints;

using System.Reflection;

using AwesomeAssertions;

using ConradSix.C4M;
using ConradSix.C4M.Endpoints.Page;

using FastEndpoints;

public sealed class CmsEndpointKindTests
{
    private static IEnumerable<Type> EngineEndpoints => C4MEndpoints.Assembly.GetTypes()
        .Where(type => !type.IsAbstract && typeof(IEndpoint).IsAssignableFrom(type));

    [Fact]
    public void EveryEngineEndpoint_ShouldBeMarkedAsEditorOrDelivery() =>
        EngineEndpoints.Where(type => type.GetCustomAttribute<CmsEndpointAttribute>() is null).Select(type => type.Name)
            .Should().BeEmpty();

    [Fact]
    public void DeliveryEndpoints_ShouldBeThePublishedPageAndMenuReads() =>
        EngineEndpoints.Where(type => type.GetCustomAttribute<CmsEndpointAttribute>()?.Kind == CmsEndpointKind.Delivery)
            .Select(type => type.Name)
            .Should().BeEquivalentTo("GetPageByPath", "GetMenu", "GetDefaultMenu", "GetFooterMenu");

    [Theory]
    [InlineData(typeof(UpdatePage), "editor")]
    [InlineData(typeof(GetPageByPath), "delivery")]
    [InlineData(typeof(HostEndpoint), null)]
    public void ConfigureCmsEndpoint_ShouldApplyTheKindsPolicyOnlyToCmsEndpoints(Type endpointType, string? expected)
    {
        string? applied = null;

        var configured = new EndpointDefinition(endpointType, typeof(EmptyRequest), typeof(object))
            .ConfigureCmsEndpoint(_ => applied = "editor", _ => applied = "delivery");

        configured.Should().Be(expected is not null);
        applied.Should().Be(expected);
    }
}

// An endpoint of the host's own, which the engine leaves alone.
public sealed class HostEndpoint : EndpointWithoutRequest
{
    public override void Configure() => this.Get("/host");
}
