# GitHub MCP Registry Demo

A minimal ASP.NET Core implementation of an [MCP (Model Context Protocol) Server Registry](https://modelcontextprotocol.io), built to demonstrate the registry API surface defined by the spec. Server metadata is stored in-memory with seed data so you can explore the endpoints immediately.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting Started

```bash
# Clone the repo
git clone https://github.com/boclifton-MSFT/GithubMcpRegistryDemo.git
cd GithubMcpRegistryDemo

# Run with HTTPS (recommended — matches the included .http file)
dotnet run --launch-profile https
```

The server will start on **https://localhost:7219** and **http://localhost:5218**.

> **Note:** Running plain `dotnet run` (without `--launch-profile https`) uses the `http` profile and only listens on `http://localhost:5218`.

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/v0.1/health` | — | Health check |
| `GET` | `/v0.1/servers` | — | List servers (supports `search`, `version`, `limit`, `cursor`, `updated_since` query params) |
| `GET` | `/v0.1/servers/{name}/versions` | — | List all versions of a server |
| `GET` | `/v0.1/servers/{name}/versions/{version}` | — | Get a specific server version (`latest` is supported) |
| `POST` | `/v0.1/publish` | API key | Publish or update a server entry |

### Query Parameters (List Servers)

| Parameter | Description |
|-----------|-------------|
| `search` | Filter by name, description, or title (case-insensitive) |
| `version` | `latest` or an exact version string |
| `limit` | Page size, 1–100 (default 20) |
| `cursor` | Opaque cursor for pagination |
| `updated_since` | RFC 3339 datetime to filter recently updated entries |

## Authentication

The `POST /v0.1/publish` endpoint is protected by an API key. Include the key in the `X-Registry-Api-Key` header:

```
X-Registry-Api-Key: dev-only-change-me
```

The key is configured in `appsettings.json` under `RegistrySecurity:PublishApiKey`. Requests without a valid key receive a **401 Unauthorized** response.

## Seed Data

The registry starts with two pre-loaded servers:

| Name | Versions | Description |
|------|----------|-------------|
| `io.github.demo/filesystem` | 1.0.0, 1.1.0 | Filesystem MCP server with local file operations |
| `io.github.demo/weather` | 0.9.0 | Weather MCP server exposing forecast tools |

## Testing with the .http File

The included `GithubMcpRegistryDemo.http` file contains sample requests for every endpoint. Open it in VS Code with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension and click **Send Request** above any block.

Examples:

```http
# Health check
GET https://localhost:7219/v0.1/health

# Search for servers
GET https://localhost:7219/v0.1/servers?search=filesystem&version=latest

# Publish (requires API key)
POST https://localhost:7219/v0.1/publish
Content-Type: application/json
X-Registry-Api-Key: dev-only-change-me

{
  "name": "io.github.demo/weather",
  "description": "Weather MCP server exposing forecast tools and alerts.",
  "version": "1.0.0",
  "title": "Weather",
  "repository": {
    "url": "https://github.com/example/weather-mcp",
    "source": "github"
  },
  "packages": [
    {
      "registryType": "npm",
      "registryBaseUrl": "https://registry.npmjs.org",
      "identifier": "@example/weather-mcp",
      "version": "1.0.0"
    }
  ]
}
```

## Project Structure

```
├── Program.cs                  # All application code (endpoints, store, auth, models)
├── GithubMcpRegistryDemo.http  # Sample HTTP requests for testing
├── appsettings.json            # Configuration (includes publish API key)
├── Properties/
│   └── launchSettings.json     # http and https launch profiles
└── .vscode/
    └── settings.json           # Workspace settings (preview SDK support)
```

## License

This project is provided as a demo/reference implementation.
