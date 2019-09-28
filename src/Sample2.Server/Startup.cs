using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sample2.Server
{
    public class Startup
    {
        private ILogger _logger;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            // Have to do this starting in ASP.NET Core 3.0, until we get to .Configure(...)
            var lf = LoggerFactory.Create(lb =>
            {
                lb.AddConsole();
            });
            _logger = lf.CreateLogger<Startup>();

            UseAuthentication = Configuration.GetValue(nameof(UseAuthentication), false);
            _logger.LogInformation("UseAuthentication setting: " + UseAuthentication);
        }

        public IConfiguration Configuration { get; }

        public bool UseAuthentication { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if (UseAuthentication)
            {
                _logger.LogInformation("Adding NEGOTIATE authentication scheme");
                services.AddAuthentication(
                    NegotiateDefaults.AuthenticationScheme
                ).AddNegotiate();
            }

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(ILogger<Startup> logger, IApplicationBuilder app, IWebHostEnvironment env)
        {
            // First, rewire
            _logger.LogInformation("REWIRING logging...");
            _logger = logger;
            _logger.LogInformation("...logging REWIRED");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHttpsRedirection();
            }

            app.UseRouting();

            if (UseAuthentication)
            {
                _logger.LogInformation("Enabling Authentication in Kestrel");
                app.UseAuthentication();
            }

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
