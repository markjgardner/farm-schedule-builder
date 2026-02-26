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

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().Be("user-123");
        result.UserDetails.Should().Be("John Doe");
        result.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public void ParseClientPrincipal_MissingHeader_ReturnsNull()
    {
        var context = new DefaultHttpContext();

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().BeNull();
        result.UserDetails.Should().BeNull();
        result.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public void ParseClientPrincipal_InvalidBase64_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = "not-valid-base64!!!";

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().BeNull();
        result.UserDetails.Should().BeNull();
    }

    [Fact]
    public void ParseClientPrincipal_EmptyHeader_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = "";

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().BeNull();
        result.UserDetails.Should().BeNull();
    }

    [Fact]
    public void ParseClientPrincipal_InvalidJson_ReturnsNull()
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("not json"));

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().BeNull();
        result.UserDetails.Should().BeNull();
    }

    [Fact]
    public void ParseClientPrincipal_MissingFields_ReturnsNull()
    {
        var json = """{"otherField":"value"}""";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().BeNull();
        result.UserDetails.Should().BeNull();
    }

    [Fact]
    public void ParseClientPrincipal_WithRoles_ReturnsRoles()
    {
        var json = """{"userId":"user-123","userDetails":"user@example.com","userRoles":["anonymous","authenticated","admin"]}""";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().Be("user-123");
        result.UserDetails.Should().Be("user@example.com");
        result.UserRoles.Should().BeEquivalentTo(new[] { "anonymous", "authenticated", "admin" });
    }

    [Fact]
    public void ParseClientPrincipal_EmptyRoles_ReturnsEmptyList()
    {
        var json = """{"userId":"user-123","userDetails":"user@example.com","userRoles":[]}""";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().Be("user-123");
        result.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public void ParseClientPrincipal_MissingRolesField_ReturnsEmptyList()
    {
        var json = """{"userId":"user-123","userDetails":"user@example.com"}""";
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var context = new DefaultHttpContext();
        context.Request.Headers["x-ms-client-principal"] = base64;

        var result = FarmScheduler.Functions.Functions.AuthHelper.ParseClientPrincipal(context.Request);

        result.UserId.Should().Be("user-123");
        result.UserRoles.Should().BeEmpty();
    }
}
