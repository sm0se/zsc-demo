# ZSC demo — Service Discovery Architecture (Phase 2+)

A synthetic, ZSC-flavored .NET monorepo demonstrating **runtime service discovery** and **distributed correlation IDs**. This is a refactored version demonstrating **complete decoupling** of services from hardcoded routing tables.

**Key Achievement**: Adding a new microservice requires **ZERO changes** to existing services (BFF, Interceptor, PatientService, CommonLib).

---

## Architecture Overview

### The Problem (Phase 1: Before)
- **Hardcoded routing** in `ServiceRouteMap` (CommonLib)
- **No correlation IDs**: Each service logs in isolation; no distributed tracing
- **Tight coupling**: Adding a service meant modifying CommonLib, Interceptor, and BFF
- **No runtime flexibility**: All routes compiled into binaries

### The Solution (Phase 2+: After)
- **Runtime service registry** (ServiceDiscovery) at port 5300
- **Self-registration**: Each service registers itself on startup with its address
- **Correlation ID propagation**: X-Correlation-Id header flows through entire call chain
- **Zero-change service addition**: New services don't touch existing code

---

## Components

| Project | Role | Port |
|---------|------|------|
| `src/Zsc.ServiceDiscovery` | Runtime registry for all services | 5300 |
| `src/Zsc.CommonLib` | Shared DTOs, events, IServiceDiscoveryClient, correlation ID constant | — |
| `src/Zsc.Interceptor` | Edge gateway; discovers services at runtime; propagates correlation IDs | 5200 |
| `src/Zsc.Bff` | Composes dashboard from services via Interceptor; includes correlation ID | 5000+ |
| `src/Zsc.PatientService` | Patient management REST + gRPC; self-registers with discovery | 5101/5102 |
| `src/Zsc.AuditService` | **[NEW]** Audit log endpoint; proves decoupling works | 5401 |

---

## Proof of Decoupling: AuditService

**AuditService** demonstrates that a new microservice can be added **without modifying existing code**.

### What AuditService Does
- Listens on port 5401
- Registers with ServiceDiscovery on startup (name: `audit-service`)
- Exposes `GET /audits/{id}` → dummy audit record
- Includes correlation ID middleware
- **Zero changes** to CommonLib, Interceptor, BFF, or PatientService

### Running AuditService

```bash
# Terminal 1: Start ServiceDiscovery first
dotnet run --project src/Zsc.ServiceDiscovery

# Terminal 2: Start AuditService (auto-registers)
dotnet run --project src/Zsc.AuditService

# Terminal 3: Start Interceptor (discovers & routes)
dotnet run --project src/Zsc.Interceptor

# Terminal 4: Call through Interceptor by service name
curl -H "X-Correlation-Id: test-001" \
  http://localhost:5200/api/audit-service/audits/audit-123

# Response includes correlation ID header
# Logs in AuditService show: Correlation ID: test-001
```

---

## Running Locally

Requires .NET 8 SDK.

### Full Stack (All Services)

```bash
# Terminal 1: Service Discovery (required first)
dotnet run --project src/Zsc.ServiceDiscovery

# Terminal 2: Patient Service
dotnet run --project src/Zsc.PatientService

# Terminal 3: Audit Service (optional, demonstrates decoupling)
dotnet run --project src/Zsc.AuditService

# Terminal 4: Interceptor
dotnet run --project src/Zsc.Interceptor

# Terminal 5: BFF
dotnet run --project src/Zsc.Bff
```

### Run Tests

```bash
# All 34 tests (includes new AuditService tests)
dotnet test

# Specific test suite
dotnet test tests/Zsc.ServiceDiscovery.Tests
dotnet test tests/Zsc.AuditService.Tests
```

### Verify End-to-End with Correlation ID

```bash
# Start services in background
dotnet run --project src/Zsc.ServiceDiscovery &
dotnet run --project src/Zsc.PatientService &
dotnet run --project src/Zsc.Interceptor &
dotnet run --project src/Zsc.Bff &

sleep 2

# Call BFF dashboard with correlation ID
curl -H "X-Correlation-Id: my-trace-123" \
  http://localhost:5000/api/patients/pat-000001/dashboard

# Check logs for correlation ID appearing in all services
```

---

## Test Coverage

**34 tests total** (10 original + 24 new), all passing:

| Suite | Tests | Coverage |
|-------|-------|----------|
| Zsc.CommonLib.Tests | 3 | ServiceRouteMap (deprecated) |
| Zsc.ServiceDiscovery.Tests | 8 | Register/resolve endpoints, validation |
| Zsc.Bff.Tests | 3 | Correlation ID generation/propagation |
| Zsc.Interceptor.Tests | 9 | Correlation ID + service discovery routing |
| Zsc.PatientService.Tests | 6 | Endpoints, self-registration |
| **Zsc.AuditService.Tests** | **3** | **Endpoint, correlation ID, decoupling proof** |
| **ServiceDiscovery Integration** | **2** | **Service routing via discovery** |
| **TOTAL** | **34** | **✅ All passing** |

---

## How Correlation IDs Work

Every request gets a unique `X-Correlation-Id`:

```
Client → BFF (generate/propagate)
       → Interceptor (propagate)
       → PatientService (log with ID)
       ← Response (include ID header)
```

All services log with correlation ID in their scope, enabling distributed request tracing across the entire call chain.

---

## Acceptance Criteria Met

✅ **Criterion 1**: Service registration is runtime-configurable (ServiceDiscovery)  
✅ **Criterion 2**: No compile-time route dependency (ServiceRouteMap marked [Obsolete])  
✅ **Criterion 3**: Concrete proof (AuditService added without modifying others)  
✅ **Criterion 4**: Correlation ID propagation (end-to-end tracing)  
✅ **Criterion 5**: All existing tests pass (34/34 passing)

---

## Production Roadmap

### Near Term
- Replace `InMemoryServiceRegistry` with distributed registry (Consul, etcd, K8s DNS)
- Add service health checks and heartbeats
- Implement correlation ID sampling for performance

### Medium Term
- Distributed tracing integration (Jaeger, Application Insights)
- Service retry policies
- Circuit breaker pattern for failing services

### Adding New Services
To add a new service (following the AuditService pattern):

1. Create project in `src/Zsc.NewService`
2. Add correlation ID middleware (copy from AuditService)
3. Add self-registration on startup (copy from AuditService)
4. Add endpoints and business logic
5. Create tests in `tests/Zsc.NewService.Tests`
6. Add to solution file

**Result: No changes needed to CommonLib, Interceptor, BFF, or PatientService.**

---

## References

- **Phase 1**: User story and acceptance criteria for decoupling
- **Phase 2**: Service discovery implementation + correlation ID propagation
- **Phase 3**: Comprehensive test coverage (34 tests)
- **Phase 4**: AuditService proof-of-concept + README documentation
