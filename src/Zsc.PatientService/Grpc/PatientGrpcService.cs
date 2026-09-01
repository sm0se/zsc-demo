using Grpc.Core;
using Zsc.PatientService.Data;

namespace Zsc.PatientService.Grpc;

// The gRPC side of PatientService - covers the "grpc" transport named in
// Requirement #2 (R2.6). Its address (5102) is the GrpcAddress hardcoded in
// Zsc.CommonLib.Routing.ServiceRouteMap.
public sealed class PatientGrpcService : PatientGrpc.PatientGrpcBase
{
    private readonly IPatientRepository _repository;

    public PatientGrpcService(IPatientRepository repository) => _repository = repository;

    public override Task<PatientSummaryReply> GetPatientSummary(PatientSummaryRequest request, ServerCallContext context)
    {
        var patient = _repository.Get(request.PatientId)
            ?? throw new RpcException(new Status(StatusCode.NotFound, $"No patient '{request.PatientId}'."));

        return Task.FromResult(new PatientSummaryReply
        {
            PatientId = patient.PatientId,
            DisplayName = patient.DisplayName,
            CaseCount = patient.History.Count,
        });
    }
}
