# Session Log: 2026-02-23 HTTP Authorized Fix

**Agents:** Bishop (Backend Dev), Coordinator (QA), Scribe (Memory)

**What Happened:**
1. Bishop traced HTTP authorized request failure to HTTPS redirect-caused header loss in `.http` samples.
2. Applied minimal fix: changed `GithubMcpRegistryDemo.http` host from `http://localhost:5218` to `https://localhost:7219`.
3. Security model unchanged: auth scheme + publish-policy remain operational.
4. Coordinator verified: build passed; unauthorized=401; authorized=200 over HTTPS target.

**Decision:** Use HTTPS endpoint directly in sample requests to ensure custom headers persist.

**Verification:** Build passed; auth tests confirmed working.

**Files Changed:**
- `GithubMcpRegistryDemo.http` (host variable updated)
- `.squad/decisions/inbox/bishop-http-auth-fix.md` (new decision)

**Outcome:** HTTP auth request samples now work reliably with framework auth/authz policy.
