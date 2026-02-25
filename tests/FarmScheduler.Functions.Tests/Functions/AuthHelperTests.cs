using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FarmScheduler.Functions.Tests.Functions;

public class AuthHelperTests
{
    [Fact]
    public void ParseClientPrincipal_ValidHeader_ReturnsUserIdAndDetails()
    {
        var json = """{"userId":"user-123","userDetails":"John Doe"}""";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var (userId, userDetails) = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        userId.Should().Be("user-123");
        userDetails.Should().Be("John Doe");
    }

    [Fact]
    public void ParseClientPrincipal_MissingHeader_ReturnsNull()
    {
        var context = new DefaultHttpContext();

        var (userId, userDetails) = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        userId.Should().BeNull();
        userDetails.Should().BeNull();
    }

    [Fact]
    public void ParseClientPrincipal_InvalidBase64_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = "not-valid-base64!!!";

        var (userId, userDetails) = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        userId.Should().BeNull();
        userDetails.Should().BeNull();
    }

    [Fact]
    public void ParseClientPrincipal_EmptyHeader_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = "";

        var (userId, userDetails) = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        userId.Should().BeNull();
        userDetails.Should().BeNull();
    }

    [Fact]
    public void ParseClientPrincipal_InvalidJson_ReturnsNull()
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("not json"));

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var (userId, userDetails) = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        userId.Should().BeNull();
        userDetails.Should().BeNull();
    }

    [Fact]
    public void ParseClientPrincipal_MissingFields_ReturnsNull()
    {
        var json = """{"otherField":"value"}""";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var (userId, userDetails) = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        userId.Should().BeNull();
        userDetails.Should().BeNull();
    }
}
