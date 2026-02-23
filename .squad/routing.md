# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture and scope | Ripley | API shape, boundaries, technical trade-offs |
| Backend API implementation | Bishop | ASP.NET minimal API endpoints, handlers, validation |
| DevOps and tooling | Parker | dotnet CLI scaffolding, CI/build setup |
| Testing and quality | Lambert | Unit/integration tests, edge case validation |
| Documentation and examples | Dallas | README updates, API usage examples |
| Session logging | Scribe | Automatic — never needs routing |

## Rules

1. Eager by default — launch independent work in parallel.
2. Scribe runs after substantial work to merge decisions and logs.
3. Quick factual checks can be answered by the coordinator directly.
4. Use reviewer lockout semantics when work is rejected.
