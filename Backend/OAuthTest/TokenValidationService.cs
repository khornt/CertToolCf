using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace CertTool.Backend.OAuthTest;

/// <summary>Validates a Keycloak JWT access token locally using its published JWKS.</summary>
public sealed class TokenValidationService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<string> ValidateAsync(string tokenEndpoint, string expectedAudience, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return "No access token available. Retrieve a token first.";
        if (string.IsNullOrWhiteSpace(expectedAudience))
            return "Expected audience is required.";

        try
        {
            // Derive the discovery address from the configured Keycloak token endpoint,
            // rather than allowing the untrusted token to choose its own issuer or keys.
            var endpoint = new Uri(tokenEndpoint, UriKind.Absolute);
            if (endpoint.Scheme != Uri.UriSchemeHttps)
                return "Token endpoint must use HTTPS.";

            const string suffix = "/protocol/openid-connect/token";
            var path = endpoint.AbsolutePath.TrimEnd('/');
            if (!path.EndsWith(suffix, StringComparison.Ordinal))
                return "Expected a Keycloak token endpoint ending in " + suffix;

            var realmPath = path[..^suffix.Length];
            if (string.IsNullOrWhiteSpace(realmPath))
                return "Cannot determine the Keycloak realm from the token endpoint.";

            var baseIssuer = endpoint.GetLeftPart(UriPartial.Authority) + realmPath;
            var discoveryUrl = baseIssuer + "/.well-known/openid-configuration";

            using var metadataResponse = await Http.GetAsync(discoveryUrl);
            metadataResponse.EnsureSuccessStatusCode();
            var metadata = JObject.Parse(await metadataResponse.Content.ReadAsStringAsync());
            var issuer = metadata.Value<string>("issuer")
                         ?? throw new InvalidOperationException("Missing issuer in OIDC discovery response.");
            if (!string.Equals(issuer.TrimEnd('/'), baseIssuer.TrimEnd('/'), StringComparison.Ordinal))
                return $"Issuer mismatch in discovery metadata. Expected: {baseIssuer}; discovered: {issuer}";

            var jwksUrl = metadata.Value<string>("jwks_uri")
                          ?? throw new InvalidOperationException("Missing jwks_uri in OIDC discovery response.");
            if (!Uri.TryCreate(jwksUrl, UriKind.Absolute, out var jwksUri) ||
                jwksUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(jwksUri.Authority, endpoint.Authority, StringComparison.OrdinalIgnoreCase))
            {
                return "JWKS URL is not HTTPS on the expected Keycloak host.";
            }
                

            using var keysResponse = await Http.GetAsync(jwksUri);
            keysResponse.EnsureSuccessStatusCode();
            var keys = new JsonWebKeySet(await keysResponse.Content.ReadAsStringAsync());
            var signingKeys = keys.GetSigningKeys();
            if (signingKeys.Count == 0)
                return "Keycloak did not publish any usable signing keys.";

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = expectedAudience.Trim(),
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(5),
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
            };

            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            handler.ValidateToken(accessToken, parameters, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwt)
                return "Token type is not a JWT access token.";

            return $"VALID — signature, issuer, audience and lifetime verified.\n" +
                   $"Issuer: {jwt.Issuer}\n" +
                   $"Audience: {string.Join(", ", jwt.Audiences)}\n" +
                   $"Algorithm: {jwt.Header.Alg}\n" +
                   $"Key ID: {jwt.Header.Kid ?? "(none)"}\n" +
                   $"Expires (UTC): {jwt.ValidTo:yyyy-MM-dd HH:mm:ss}";
        }
        catch (SecurityTokenException ex)
        {
            return "INVALID — " + ex.Message;
        }
        catch (Exception ex)
        {
            return "Validation could not be completed — " + ex.Message;
        }
    }
}
