# Project Context

- **Owner:** boclifton-MSFT
- **Project:** GitHub custom MCP Registry implementation using a .NET 10 Minimal API backend.
- **Stack:** C#, .NET 10 Minimal API, ASP.NET Core
- **Created:** 2026-02-23T15:46:06.4008733Z

## Learnings

- Initial team setup completed with Alien cast naming.
- Implementing MCP registry endpoints is cleaner with a small in-memory store plus endpoint mapping extension methods, which keeps API behavior deterministic while remaining easy to swap for persistent storage later.
- Securing `POST /v0.1/publish` can stay localized by validating `X-Registry-Api-Key` directly in the endpoint handler against a config value (`RegistrySecurity:PublishApiKey`) and returning `401` before request validation/publish logic.
- For minimal APIs, custom API key checks are cleaner and more extensible when implemented as an ASP.NET authentication scheme plus an authorization policy, then attached with `.RequireAuthorization(...)` on the endpoint.
- REST clients may drop custom headers when following HTTP→HTTPS redirects, so protected request samples should target the HTTPS endpoint directly to keep auth behavior deterministic.
- API key auth is more reliable when both configured and provided keys are normalized (trim + optional surrounding quote removal), because some clients/variable sources send quoted values that otherwise trigger false `401` challenges.

## Team Updates

📌 Team update (2026-02-23): Platform scaffold complete with .NET 10 minimal API project and successful build — decided by Parker
📌 Team update (2026-02-23T10-11-04): X-Registry-Api-Key security gate added to POST /v0.1/publish with config-driven validation and 401 response for unauthorized requests — decided by Bishop
📌 Team update (2026-02-23T16-22-00): Publish security refactored to framework auth/authz using `publish-api-key` scheme + `publish-policy` and endpoint `.RequireAuthorization(...)` — decided by Bishop
📌 Team update (2026-02-23T16-45-00): Updated local `.http` host to HTTPS to avoid redirect-dependent auth header behavior for publish-policy examples — decided by Bishop
📌 Team update (2026-02-23T11-03-12): HTTP auth fix merged into canonical decisions log; inbox cleared — merged by Scribe
📌 Team update (2026-02-23T11-19-03): Publish API key normalization decision merged to canonical log; API key auth is more reliable when both configured and provided keys are normalized (trim + optional surrounding quote removal) — decided by Bishop, logged by Scribe
