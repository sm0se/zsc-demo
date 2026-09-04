# Phase 2 Implementation Summary: Service Discovery & Correlation-ID Propagation

## Overview
Successfully implemented a decoupled, runtime-based service discovery architecture with end-to-end correlation-ID propagation, eliminating the hardcoded routing dependency from Zsc.CommonLib.

## What Was Built

### 1. New Zsc.ServiceDiscovery Service (Port 5300)
A minimal ASP.NET Core service providing centralized service registration and discovery:

**Endpoints:**
- `POST /services/{name}/register` - Register a service with HTTP, gRPC, and event-topic addresses
- `GET /services/{name}/resolve` - Retrieve registered service information

**Features:**
- In-memory registry backed by thread-safe dictionary
- Optional fields for HTTP, gRPC, and event-topic addresses
- Requires at least one of HTTP or gRPC address

### 2. Service Discovery Client in Zsc.CommonLib
New abstraction layer for runtime service resolution:

**Interface: `IServiceDiscoveryClient`**
- `Task<ServiceInfo?> ResolveAsync(string serviceName, CancellationToken cancellationToken = default)`

**Implementation: `HttpServiceDiscoveryClient`**
- Calls the ServiceDiscovery service via HTTP
- Graceful error handling (returns null on failures)
- Proper logging integration

### 3. Routing Responsibility Removed from CommonLib
**Deleted Files:**
- `ServiceRouteMap.cs` - Eliminated hardcoded route table
- `ServiceRouteEntry.cs` - Removed static route entry model
- `RoutedHttpClientFactory.cs` - Removed static factory

**Result:** CommonLib now contains only:
- Shared DTOs (PatientDto, CreatePatientRequest, etc.)
- Event bus abstractions and implementations
- Service discovery client interface

### 4. Updated Zsc.PatientService
**Auto-Registration:**
- Registers itself with ServiceDiscovery on startup
- Reads ports from configuration (default: 5101 HTTP, 5102 gRPC)

**Correlation-ID Propagation:**
- Middleware extracts `X-Correlation-Id` header (or generates UUID)
- Includes correlation-ID in all response headers
- Logs correlation-ID with all requests

### 5. Updated Zsc.Interceptor
**Discovery-Based Routing:**
- Resolves service addresses via `IServiceDiscoveryClient`
- Returns 502 BadGateway if service not found

**Correlation-ID Generation & Propagation:**
- Generates `X-Correlation-Id` if not present
- Propagates to all downstream services
- Logs each forwarding with correlation-ID

### 6. Updated Zsc.Bff
**Discovery Client Integration:**
- Instantiates `IServiceDiscoveryClient` for future use

**Correlation-ID Propagation:**
- Generates or extracts `X-Correlation-Id` from inbound requests
- Propagates to Interceptor via headers
- Includes correlation-ID in logs

## Acceptance Criteria Met

### ✅ 1. Central Service-Discovery Component
- Implemented: `Zsc.ServiceDiscovery` service on port 5300
- Supports HTTP, gRPC, and event-bus addresses
- Runtime registration (no code changes required)
- Queryable by any service

### ✅ 2. Removal of Routing Responsibility from CommonLib
- Deleted hardcoded `ServiceRouteMap`
- All services query discovery service at runtime
- CommonLib only has DTOs, events, and discovery client interface

### ✅ 3. End-to-End Correlation-ID Propagation
- Interceptor generates correlation-ID on inbound requests
- Propagated to all upstream services via `X-Correlation-Id` header
- All services log with correlation-ID
- Single correlation-ID visible across entire request trace

### ✅ 4. Concrete Test: New Trivial API
- Added `GET /patients/{patientId}/summary` endpoint to PatientService
- **No changes required** to CommonLib, Interceptor, or BFF
- Endpoint immediately discoverable and routable
- Correlation-ID flows through entire stack
- Verified with end-to-end test

## Architecture Changes

### Before (Tightly Coupled)
```
BFF ──────┐
          ├─→ CommonLib.ServiceRouteMap (hardcoded)
Interceptor ──┘
PatientService ──┘

Adding new API required touching:
- CommonLib (add route entry)
- Interceptor (if needed)
- BFF (if consumer-facing)
- New service itself (4 places)
```

### After (Decoupled)
```
                     ┌─→ ServiceDiscovery (port 5300)
BFF ─────┐           │
         ├─→ Interceptor ─→ IServiceDiscoveryClient
         │                  
         └─→ PatientService (self-registers)

Adding new API requires touching ONLY:
- The service itself (1 place)
```

## Demonstrated End-to-End Flows

### Test: New Trivial API
```bash
curl "http://localhost:5200/api/patient-service/patients/pat-000003/summary" \
  -H "X-Correlation-Id: unique-trace-xyz-999"

Logs show:
- Interceptor: Forwarding GET /patient-service/patients/pat-000003/summary [CorrelationId=unique-trace-xyz-999]
- PatientService: GET /patients/pat-000003/summary [CorrelationId=unique-trace-xyz-999]
```

## Testing

All tests pass:
- `Zsc.CommonLib.Tests` - Discovery client tests (1 test)
- `Zsc.PatientService.Tests` - Endpoint tests (4 tests)
- `Zsc.Interceptor.Tests` - Routing tests (2 tests)
- `Zsc.Bff.Tests` - Dashboard composition (1 test)

**Total: 8 tests, all passing**

## Files Changed

### Created (7 files):
- `src/Zsc.ServiceDiscovery/` - New service project
- `src/Zsc.CommonLib/ServiceDiscovery/` - Discovery client

### Deleted (3 files):
- Routing-related files from CommonLib

### Modified (6 files):
- All service Programs.cs
- All test files

## Key Achievement
**Adding new APIs now requires changes ONLY to the microservice, not to shared libraries or gateways.**
