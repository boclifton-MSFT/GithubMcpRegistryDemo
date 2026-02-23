using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<InMemoryRegistryStore>();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = RegistrySecurityConstants.PublishApiKeyScheme;
        options.DefaultChallengeScheme = RegistrySecurityConstants.PublishApiKeyScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, PublishApiKeyAuthenticationHandler>(RegistrySecurityConstants.PublishApiKeyScheme, _ => { });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(RegistrySecurityConstants.PublishPolicyName, policy =>
    {
        policy.AuthenticationSchemes.Add(RegistrySecurityConstants.PublishApiKeyScheme);
        policy.RequireAuthenticatedUser();
    });

var app = builder.Build();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapRegistryEndpoints();
app.Run();

static class RegistrySecurityConstants
{
    public const string PublishApiKeyHeaderName = "X-Registry-Api-Key";
    public const string PublishApiKeyScheme = "publish-api-key";
    public const string PublishPolicyName = "publish-policy";
}

static class RegistryEndpointMapping
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;
    private static readonly Regex ServerNamePattern = new("^[a-zA-Z0-9.-]+/[a-zA-Z0-9._-]+$", RegexOptions.Compiled);
    private static readonly Regex VersionRangePattern = new(@"^(\^|~|>=|<=|>|<|=)|\*$|\.x$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void MapRegistryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v0.1/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/v0.1/servers", (HttpRequest request, InMemoryRegistryStore store) =>
        {
            var query = request.Query;
            var cursorValue = query["cursor"].ToString();
            var limitValue = query["limit"].ToString();
            var search = query["search"].ToString();
            var updatedSinceValue = query["updated_since"].ToString();
            var versionFilter = query["version"].ToString();

            var limit = DefaultLimit;
            if (!string.IsNullOrWhiteSpace(limitValue) &&
                (!int.TryParse(limitValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out limit) || limit < 1 || limit > MaxLimit))
            {
                return Results.BadRequest(new ErrorResponse("limit must be an integer between 1 and 100"));
            }

            var cursor = 0;
            if (!string.IsNullOrWhiteSpace(cursorValue) && !TryDecodeCursor(cursorValue, out cursor))
            {
                return Results.BadRequest(new ErrorResponse("cursor is invalid"));
            }

            DateTimeOffset? updatedSince = null;
            if (!string.IsNullOrWhiteSpace(updatedSinceValue))
            {
                if (!DateTimeOffset.TryParse(updatedSinceValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedUpdatedSince))
                {
                    return Results.BadRequest(new ErrorResponse("updated_since must be a valid RFC3339 datetime"));
                }

                updatedSince = parsedUpdatedSince;
            }

            if (!string.IsNullOrWhiteSpace(versionFilter) && string.Equals(versionFilter, "latest", StringComparison.OrdinalIgnoreCase) is false && VersionRangePattern.IsMatch(versionFilter))
            {
                return Results.BadRequest(new ErrorResponse("version must be 'latest' or an exact version"));
            }

            var matches = store.ListServers(search, updatedSince, versionFilter);
            if (cursor > matches.Count)
            {
                return Results.BadRequest(new ErrorResponse("cursor is out of range"));
            }

            var page = matches.Skip(cursor).Take(limit).ToList();
            var nextOffset = cursor + page.Count;
            var nextCursor = nextOffset < matches.Count ? EncodeCursor(nextOffset) : null;

            var response = new ServerListResponse(
                page.Select(item => ToServerResponse(item, store.IsLatest(item.Server.Name, item.Server.Version))).ToList(),
                new ListMetadata(page.Count, nextCursor));

            return Results.Ok(response);
        });

        app.MapGet("/v0.1/servers/{serverName}/versions", (string serverName, InMemoryRegistryStore store) =>
        {
            var normalizedName = NormalizeServerName(serverName);
            if (!store.TryGetVersions(normalizedName, out var versions))
            {
                return Results.NotFound(new ErrorResponse("Server not found"));
            }

            var response = new ServerListResponse(
                versions.Select(item => ToServerResponse(item, store.IsLatest(item.Server.Name, item.Server.Version))).ToList(),
                new ListMetadata(versions.Count, null));

            return Results.Ok(response);
        });

        app.MapGet("/v0.1/servers/{serverName}/versions/{version}", (string serverName, string version, InMemoryRegistryStore store) =>
        {
            var normalizedName = NormalizeServerName(serverName);
            var normalizedVersion = NormalizeVersion(version);

            if (!store.TryGetVersion(normalizedName, normalizedVersion, out var serverVersion))
            {
                return Results.NotFound(new ErrorResponse("Server version not found"));
            }

            return Results.Ok(ToServerResponse(serverVersion, store.IsLatest(serverVersion.Server.Name, serverVersion.Server.Version)));
        });

        app.MapPost("/v0.1/publish", (PublishServerRequest publishRequest, InMemoryRegistryStore store) =>
        {
            var validationError = ValidatePublishRequest(publishRequest);
            if (validationError is not null)
            {
                return Results.BadRequest(new ErrorResponse(validationError));
            }

            var normalizedName = NormalizeServerName(publishRequest.Name!);
            var normalizedVersion = publishRequest.Version!.Trim();
            var published = store.Publish(normalizedName, normalizedVersion, publishRequest);
            return Results.Ok(ToServerResponse(published, store.IsLatest(normalizedName, normalizedVersion)));
        })
        .RequireAuthorization(RegistrySecurityConstants.PublishPolicyName);
    }

    private static string? ValidatePublishRequest(PublishServerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required";
        }

        var normalizedName = NormalizeServerName(request.Name);
        if (!ServerNamePattern.IsMatch(normalizedName))
        {
            return "name must match namespace/server format";
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return "description is required";
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return "version is required";
        }

        var normalizedVersion = request.Version.Trim();
        if (string.Equals(normalizedVersion, "latest", StringComparison.OrdinalIgnoreCase) || VersionRangePattern.IsMatch(normalizedVersion))
        {
            return "version must be an exact value and cannot be 'latest' or a range";
        }

        return null;
    }

    private static ServerResponse ToServerResponse(ServerVersionRecord versionRecord, bool isLatest)
    {
        var registryMeta = new RegistryOfficialMeta("active", versionRecord.PublishedAt, versionRecord.UpdatedAt, isLatest);
        var responseMeta = new Dictionary<string, object?>
        {
            ["io.modelcontextprotocol.registry/official"] = registryMeta
        };

        return new ServerResponse(versionRecord.Server, responseMeta);
    }

    private static string EncodeCursor(int offset)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(offset.ToString(CultureInfo.InvariantCulture)));
    }

    private static bool TryDecodeCursor(string cursor, out int offset)
    {
        offset = 0;

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var value = Encoding.UTF8.GetString(bytes);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out offset) && offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeServerName(string serverName) => Uri.UnescapeDataString(serverName).Trim();
    private static string NormalizeVersion(string version) => Uri.UnescapeDataString(version).Trim();
}

sealed class InMemoryRegistryStore
{
    private readonly Dictionary<string, List<ServerVersionRecord>> _servers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public InMemoryRegistryStore()
    {
        Seed();
    }

    public IReadOnlyList<ServerVersionRecord> ListServers(string? search, DateTimeOffset? updatedSince, string? versionFilter)
    {
        lock (_lock)
        {
            var selected = _servers.Values
                .Select(versions => ResolveVersion(versions, versionFilter))
                .Where(record => record is not null)
                .Cast<ServerVersionRecord>();

            if (!string.IsNullOrWhiteSpace(search))
            {
                selected = selected.Where(record =>
                    record.Server.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    record.Server.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (record.Server.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (updatedSince.HasValue)
            {
                selected = selected.Where(record => record.UpdatedAt >= updatedSince.Value);
            }

            return selected
                .OrderBy(record => record.Server.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public bool TryGetVersions(string serverName, out IReadOnlyList<ServerVersionRecord> versions)
    {
        lock (_lock)
        {
            if (!_servers.TryGetValue(serverName, out var entries))
            {
                versions = Array.Empty<ServerVersionRecord>();
                return false;
            }

            versions = entries
                .OrderByDescending(record => record.PublishedAt)
                .ThenByDescending(record => record.Server.Version, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return true;
        }
    }

    public bool TryGetVersion(string serverName, string version, out ServerVersionRecord record)
    {
        lock (_lock)
        {
            if (!_servers.TryGetValue(serverName, out var entries))
            {
                record = default!;
                return false;
            }

            record = ResolveVersion(entries, version)!;
            return record is not null;
        }
    }

    public bool IsLatest(string serverName, string version)
    {
        lock (_lock)
        {
            if (!_servers.TryGetValue(serverName, out var entries))
            {
                return false;
            }

            var latest = ResolveVersion(entries, "latest");
            return latest is not null && string.Equals(latest.Server.Version, version, StringComparison.OrdinalIgnoreCase);
        }
    }

    public ServerVersionRecord Publish(string serverName, string version, PublishServerRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var detail = new ServerDetail(
            serverName,
            request.Description!.Trim(),
            version,
            request.Title?.Trim(),
            request.Repository,
            request.Packages,
            request.Meta);

        lock (_lock)
        {
            if (!_servers.TryGetValue(serverName, out var versions))
            {
                versions = [];
                _servers[serverName] = versions;
            }

            var existingIndex = versions.FindIndex(item => string.Equals(item.Server.Version, version, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                var publishedAt = versions[existingIndex].PublishedAt;
                var updated = new ServerVersionRecord(detail, publishedAt, now);
                versions[existingIndex] = updated;
                return updated;
            }

            var created = new ServerVersionRecord(detail, now, now);
            versions.Add(created);
            return created;
        }
    }

    private static ServerVersionRecord? ResolveVersion(List<ServerVersionRecord> versions, string? versionFilter)
    {
        if (versions.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(versionFilter) || string.Equals(versionFilter, "latest", StringComparison.OrdinalIgnoreCase))
        {
            return versions
                .OrderByDescending(record => record.PublishedAt)
                .ThenByDescending(record => record.Server.Version, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        return versions.FirstOrDefault(record => string.Equals(record.Server.Version, versionFilter, StringComparison.OrdinalIgnoreCase));
    }

    private void Seed()
    {
        var filesystemV1Published = DateTimeOffset.Parse("2026-01-10T08:00:00Z", CultureInfo.InvariantCulture);
        var filesystemV2Published = DateTimeOffset.Parse("2026-02-10T08:00:00Z", CultureInfo.InvariantCulture);
        var weatherPublished = DateTimeOffset.Parse("2026-02-15T08:00:00Z", CultureInfo.InvariantCulture);

        _servers["io.github.demo/filesystem"] =
        [
            new ServerVersionRecord(
                new ServerDetail(
                    "io.github.demo/filesystem",
                    "Filesystem MCP server with local file operations.",
                    "1.0.0",
                    "Filesystem",
                    new RepositoryInfo("https://github.com/modelcontextprotocol/servers", "github"),
                    [new PackageInfo("npm", "https://registry.npmjs.org", "@modelcontextprotocol/server-filesystem", "1.0.0")]),
                filesystemV1Published,
                filesystemV1Published),
            new ServerVersionRecord(
                new ServerDetail(
                    "io.github.demo/filesystem",
                    "Filesystem MCP server with improved read/write support.",
                    "1.1.0",
                    "Filesystem",
                    new RepositoryInfo("https://github.com/modelcontextprotocol/servers", "github"),
                    [new PackageInfo("npm", "https://registry.npmjs.org", "@modelcontextprotocol/server-filesystem", "1.1.0")]),
                filesystemV2Published,
                filesystemV2Published)
        ];

        _servers["io.github.demo/weather"] =
        [
            new ServerVersionRecord(
                new ServerDetail(
                    "io.github.demo/weather",
                    "Weather MCP server exposing forecast tools.",
                    "0.9.0",
                    "Weather",
                    new RepositoryInfo("https://github.com/example/weather-mcp", "github"),
                    [new PackageInfo("npm", "https://registry.npmjs.org", "@example/weather-mcp", "0.9.0")]),
                weatherPublished,
                weatherPublished)
        ];
    }
}

sealed record ServerListResponse(IReadOnlyList<ServerResponse> Servers, ListMetadata Metadata);
sealed record ListMetadata(int Count, string? NextCursor);
sealed record ServerResponse(ServerDetail Server, [property: JsonPropertyName("_meta")] IDictionary<string, object?>? Meta = null);
sealed record ServerVersionRecord(ServerDetail Server, DateTimeOffset PublishedAt, DateTimeOffset UpdatedAt);
sealed record RegistryOfficialMeta(string Status, DateTimeOffset PublishedAt, DateTimeOffset UpdatedAt, bool IsLatest);
sealed record RepositoryInfo(string Url, string Source);
sealed record PackageInfo(string RegistryType, string RegistryBaseUrl, string Identifier, string Version);

sealed record ServerDetail(
    string Name,
    string Description,
    string Version,
    string? Title = null,
    RepositoryInfo? Repository = null,
    IReadOnlyList<PackageInfo>? Packages = null,
    [property: JsonPropertyName("_meta")] IDictionary<string, object?>? Meta = null);

sealed class PublishServerRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Title { get; init; }
    public RepositoryInfo? Repository { get; init; }
    public IReadOnlyList<PackageInfo>? Packages { get; init; }
    [JsonPropertyName("_meta")]
    public Dictionary<string, object?>? Meta { get; init; }
}

sealed record ErrorResponse(string Error);

sealed class PublishApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public PublishApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredApiKey = NormalizeApiKey(_configuration["RegistrySecurity:PublishApiKey"]);
        if (string.IsNullOrEmpty(configuredApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Publish API key is not configured"));
        }

        if (!Request.Headers.TryGetValue(RegistrySecurityConstants.PublishApiKeyHeaderName, out var providedApiKey) ||
            !string.Equals(NormalizeApiKey(providedApiKey.ToString()), configuredApiKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid publish API key"));
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "publish-client") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static string NormalizeApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        var normalized = apiKey.Trim();
        if (normalized.Length >= 2 && normalized.StartsWith('"') && normalized.EndsWith('"'))
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized;
    }
}
