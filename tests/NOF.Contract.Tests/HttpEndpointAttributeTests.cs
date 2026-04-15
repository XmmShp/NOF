using NOF.Annotation;
using Xunit;

namespace NOF.Contract.Tests;

public class HttpEndpointAttributeTests
{
    [Fact]
    public void Constructor_StoresMethodInMetadataKey_AndRouteInMetadataValue()
    {
        var attribute = new HttpEndpointAttribute(HttpVerb.Get, "/api/items/{id:int}");
        MetadataAttribute metadata = attribute;

        Assert.Equal("api.http.get.endpoint", metadata.Key);
        Assert.Equal("/api/items/{id:int}", metadata.Value);
        Assert.Equal(HttpVerb.Get, attribute.Method);
        Assert.Equal("/api/items/{id:int}", attribute.Route);
    }

    [Fact]
    public void TryParseMetadataKey_RoundTripsMethod()
    {
        var metadataKey = HttpEndpointAttribute.CreateMetadataKey(HttpVerb.Post);

        var parsed = HttpEndpointAttribute.TryParseMetadataKey(metadataKey, out var method);

        Assert.True(parsed);
        Assert.Equal(HttpVerb.Post, method);
    }
}
