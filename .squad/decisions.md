# Team Decisions

### 2026-02-23: Initial project direction
**By:** boclifton-MSFT (via Copilot)
**What:** Implement a GitHub custom MCP Registry using a .NET 10 Minimal API as the base API surface.
**Why:** This sets a concrete technical direction aligned with the project goal.

### 2026-02-23: Platform Scaffold Decision
**By:** Parker (DevOps/Platform)
**What:** Scaffold the API directly at repository root using `dotnet new webapi --framework net10.0 --name GithubMcpRegistryDemo --output . --no-openapi`.
**Why:** Keeps project naming aligned with repo context, uses ASP.NET Core Web API template with minimal API defaults, and avoids adding OpenAPI package dependencies during initial platform bootstrap.
**Verification:** `dotnet build --nologo` succeeds for `GithubMcpRegistryDemo.csproj` targeting `net10.0`.

### 2026-02-23: MCP Registry API Shape
**By:** Bishop (Backend Dev)
**What:** Implement `/v0.1` registry endpoints using a dedicated in-memory store with seeded versions, list metadata pagination (`count`, `nextCursor`), and explicit validation/404 error responses.
**Why:** Keeps the .NET 10 minimal API implementation small and deterministic while aligning response shapes and query/filter behavior with MCP registry expectations.
**Verification:** `dotnet build --nologo` succeeds for `GithubMcpRegistryDemo.csproj` after endpoint and model changes.

### 2026-02-23: Publish Endpoint API Key Guard
**By:** Bishop (Backend Dev)
**What:** Require `X-Registry-Api-Key` header for `POST /v0.1/publish` and validate against `RegistrySecurity:PublishApiKey` configuration; return `401 Unauthorized` when missing or invalid.
**Why:** Adds a minimal, deterministic security control for publish operations without affecting read-only registry endpoints.
**Verification:** `dotnet build` passed; unauthorized publish returns 401; authorized publish returns 200.
