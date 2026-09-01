# ZSC demo — "before" architecture (Requirement #2)

A synthetic, ZSC-flavored .NET monorepo built to demo Zeiss Requirement #2
("Simplification of ZSC routing mechanisms from Interceptor to individual
microservices and removing current common library dependencies") using
Kenome's kencode product. This is **not** Zeiss's real ZSC codebase — Zeiss
didn't share it — it's a small, real, compilable stand-in that reproduces the
same coupling and the same pain points, so a live coding agent has something
concrete to refactor.

## Why a monorepo

The client's brief names four separate repos (Patient service, BFF,
Interceptor, Common library). Here they're four separate, real .NET projects
inside one solution instead of four physically separate repos — same
coupling story (project-reference graph, not repo boundaries), much simpler
to operate for a live demo. See `../kencode-zeiss-poc-requirements.md` for
the full requirement analysis this build is grounded in.

## Layout

| Project | Role |
|---|---|
| `src/Zsc.CommonLib` | Shared library: hardcoded `ServiceRouteMap` (service name → address), shared DTOs, and an `IEventBus` stub for the service-bus transport. **This is the coupling point.** |
| `src/Zsc.Interceptor` | Edge service. Forwards every inbound call based on `ServiceRouteMap`. No correlation-id propagation — every hop logs in isolation, which is why tracing a request across services is hard today. |
| `src/Zsc.Bff` | Composes a patient dashboard from two calls made through the Interceptor, deserializing CommonLib's DTOs directly — a compile-time dependency on the shared library. |
| `src/Zsc.PatientService` | The domain microservice. REST endpoints (get/create patient, history) + one gRPC endpoint (`GetPatientSummary`) + publishes a `PatientUpdated` event through the `IEventBus` stub — HTTP, gRPC, and event-driven, the three transports named in the requirement (R2.6). |
| `tests/*.Tests` | One xUnit project per `src` project. |

## The pain this reproduces

Adding one new API today means touching **CommonLib** (new route entry,
maybe a new DTO), **Interceptor** (implicitly, since it just forwards by
route name — but any new cross-cutting behavior lives here), and the
**individual microservice** — plus the **BFF** if it's consumer-facing. The
routing table lives in code, shared by project reference; there's no runtime
registration, no correlation id, and no way to add a service without
redeploying everything that already depends on `Zsc.CommonLib`.

This is deliberately *not* fixed here — the fix (a service-discovery
component, name-based resolution, correlation-id propagation) is the live
coding agent's actual deliverable, not something pre-built into this repo.

## Running locally

Requires the .NET 8 SDK (or use the Docker one-liners below — nothing is
installed on this machine's host).

```bash
dotnet test                                   # from the repo root
dotnet run --project src/Zsc.PatientService   # ports 5101 (HTTP), 5102 (gRPC)
dotnet run --project src/Zsc.Interceptor      # port 5200
dotnet run --project src/Zsc.Bff              # default Kestrel port
```

Without a local SDK:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test
```
