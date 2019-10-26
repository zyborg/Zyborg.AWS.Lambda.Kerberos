using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Zyborg.AspNetCore.Authentication.NegotiatedToken;

namespace Sample4.Server
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
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            if (UseAuthentication)
            {
                _logger.LogInformation("Adding Negotiated Token *compound* authentication scheme");
                services.AddAuthentication(NegotiatedTokenDefaults.JwtBearerAuthenticationScheme)
                    .AddNegotiatedToken(options =>
                    {
                        SecurityKey sigKey;
                        using (var sha = SHA256.Create())
                        {
                            var password = "$00PERsecret";
                            var passwordHash = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                            sigKey = new SymmetricSecurityKey(passwordHash);
                        }

                        options.IssuerSigningKey = sigKey;

                    });
                services.AddAuthorization();
            }
            services.AddGrpc();
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
                endpoints.MapGrpcService<GreeterService>();

                endpoints.MapGetNegotiatedToken("/token");

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
