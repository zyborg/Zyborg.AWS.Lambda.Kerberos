using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Sample2.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class ToUpper : ControllerBase
    {

        private readonly ILogger<ToUpper> _logger;

        public ToUpper(ILogger<ToUpper> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public object Get([FromQuery]string input)
        {
            _logger.LogInformation("Got input request:", input);
            _logger.LogInformation("  * Client: [{0}]:[{1}]",
                HttpContext.Connection.RemoteIpAddress,
                HttpContext.Connection.RemotePort);
            _logger.LogInformation("  * UserID: [{0}][{1}]",
                User.Identity.Name,
                User.Identity.AuthenticationType);
            _logger.LogInformation("  * QryInp: [{0}]", input);

            return new
            {
                YouAre = $"{User?.Identity?.AuthenticationType}:{User?.Identity?.Name}",
                TimeIs = DateTime.Now,
                Result = input?.ToUpper(),
            };
        }
    }
}
