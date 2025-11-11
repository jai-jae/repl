using System.Net;
using Grpc.Core;
using ReplInternalProtocol.Coordinator;

namespace Repl.Server.Coordinator.InternalService;

public partial class ReplCoordinatorService
{
    public override Task<TravelOutResponse> TravelOut(TravelOutRequest request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    public override Task<TravelInResponse> TravelIn(TravelInRequest request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }
}
