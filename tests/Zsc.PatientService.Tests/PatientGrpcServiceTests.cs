using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Zsc.PatientService.Grpc;

namespace Zsc.PatientService.Tests;

// Covers the "grpc" transport named in Requirement #2 (R2.6) end to end
// through an in-process TestServer - not a mock of the gRPC stack.
public class PatientGrpcServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PatientGrpcServiceTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task GetPatientSummary_SeededPatient_ReturnsSummary()
    {
        var handler = _factory.Server.CreateHandler();
        using var channel = GrpcChannel.ForAddress(_factory.Server.BaseAddress, new GrpcChannelOptions { HttpHandler = handler });
        var client = new PatientGrpc.PatientGrpcClient(channel);

        // "pat-000001" is the first patient seeded by InMemoryPatientRepository.
        var reply = await client.GetPatientSummaryAsync(new PatientSummaryRequest { PatientId = "pat-000001" });

        Assert.Equal("pat-000001", reply.PatientId);
        Assert.Equal("Amara Osei", reply.DisplayName);
    }
}
