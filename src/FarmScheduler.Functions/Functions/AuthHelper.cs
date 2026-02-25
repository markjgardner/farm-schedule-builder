using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace FarmScheduler.Functions.Functions;

public static class AuthHelper
{
    public static (string? userId, string? userDetails) ParseClientPrincipal(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("x-ms-client-principal", out var headerValues))
            return (null, null);

        var header = headerValues.FirstOrDefault();
        if (string.IsNullOrEmpty(header))
            return (null, null);

        try
        {
            var decoded = Convert.FromBase64String(header);
            var json = Encoding.UTF8.GetString(decoded);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var userId = root.TryGetProperty("userId", out var uid) ? uid.GetString() : null;
            var userDetails = root.TryGetProperty("userDetails", out var ud) ? ud.GetString() : null;

            return (userId, userDetails);
        }
        catch
        {
            return (null, null);
        }
    }
}
