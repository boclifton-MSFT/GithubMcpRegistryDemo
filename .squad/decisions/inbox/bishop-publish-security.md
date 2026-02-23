# Publish endpoint API key guard

**By:** Bishop (Backend Dev)  
**What:** Require `X-Registry-Api-Key` for `POST /v0.1/publish` and compare it to `RegistrySecurity:PublishApiKey` from configuration; return `401 Unauthorized` when missing or invalid.  
**Why:** Adds a minimal, deterministic security control for publish operations without affecting read-only registry endpoints.
