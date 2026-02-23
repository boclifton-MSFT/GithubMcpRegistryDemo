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

### 2026-02-23: Publish endpoint security (consolidated)
**By:** Bishop (Backend Dev)
**What:** Implemented security for `POST /v0.1/publish` using ASP.NET Core authentication/authorization framework. Initially used header-based validation (`X-Registry-Api-Key`); refactored to custom `publish-api-key` authentication handler and `publish-policy` authorization policy with endpoint-level `.RequireAuthorization()`.
**Why:** Header-based approach was functional but not aligned with framework best practices. Refactoring centralizes access control in middleware, simplifies handler logic, and keeps publish business logic separated from access control.
**Evolution:** Initial minimal guard → framework-integrated auth/authz pipeline.
**Verification:** `dotnet build --nologo` passed; unauthorized requests return 401; authorized requests return 200.
