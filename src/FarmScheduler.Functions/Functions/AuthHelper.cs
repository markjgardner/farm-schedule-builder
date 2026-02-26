using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace FarmScheduler.Functions.Functions;

public static class AuthHelper
{
    public record ClientPrincipalInfo(string? UserId, string? UserDetails, IReadOnlyList<string> UserRoles);

    private static readonly ClientPrincipalInfo Empty = new(null, null, Array.Empty<string>());

    public static ClientPrincipalInfo ParseClientPrincipal(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("x-ms-client-principal", out var headerValues))
            return Empty;

        var header = headerValues.FirstOrDefault();
        if (string.IsNullOrEmpty(header))
            return Empty;

        try
        {
            var decoded = Convert.FromBase64String(header);
            var json = Encoding.UTF8.GetString(decoded);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var userId = root.TryGetProperty("userId", out var uid) ? uid.GetString() : null;
            var userDetails = root.TryGetProperty("userDetails", out var ud) ? ud.GetString() : null;

            var userRoles = new List<string>();
            if (root.TryGetProperty("userRoles", out var roles) && roles.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in roles.EnumerateArray())
                {
                    var roleStr = role.GetString();
                    if (!string.IsNullOrEmpty(roleStr))
                        userRoles.Add(roleStr);
                }
            }

            return new ClientPrincipalInfo(userId, userDetails, userRoles);
        }
        catch
        {
            return Empty;
        }
    }
}
