using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Sample4.Model;

namespace Sample4.Server
{
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }

        public override Task<UpperReply> ToUpper(UpperRequest request, ServerCallContext context)
        {
            var conn = context.GetHttpContext().Connection;
            var user = context.GetHttpContext().User;

            _logger.LogInformation("Got input request:", request.Input);
            _logger.LogInformation("  * Client: [{0}]:[{1}]",                
                conn.RemoteIpAddress,
                conn.RemotePort);
            _logger.LogInformation("  * UserID: [{0}][{1}]",
                user.Identity.Name,
                user.Identity.AuthenticationType);

            return Task.FromResult(new UpperReply
            {
                YouAre = $"{user?.Identity?.AuthenticationType}:{user?.Identity?.Name}",
                TimeIs = Timestamp.FromDateTime(DateTime.Now),
                Result = request.Input?.ToUpper(),
            });
        }
    }
}
