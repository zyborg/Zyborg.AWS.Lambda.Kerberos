using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Grpc.Net.Client;
using Sample4.Model;
using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using Zyborg.AWS.Lambda.Kerberos;
using Zyborg.NegotiatedToken.Client;

namespace Sample4
{
    public class Function
    {
        private KerberosManager _km;

        private IAmazonS3 _s3;

        private string KerberosRealm = "EXAMPLE.COM";
        private string KerberosRealmKdc = "DC1.EXAMPLE.COM";
        private string KerberosPrincipal = "sample_user@EXAMPLE.COM";
        private string KerberosKeytabS3Path = "default-s3-bucket/default-s3-key/path/to/a/keytab";

        private string ServerAddress = "https://example.com";
        private string TokenPath = "/token";

        /// <summary>
        /// The main entry point for the custom runtime.
        /// </summary>
        /// <param name="args"></param>
        private static async Task Main(string[] args)
        {
            var funcInst = new Function();

            Console.WriteLine("Initializing Kerberos at BOOTSTRAP...");
            await funcInst.InitKerberos();
            Console.WriteLine("...Kerberos initialized");

            Func<string, ILambdaContext, Task<string>> func = funcInst.FunctionHandler;
            using(var handlerWrapper = HandlerWrapper.GetHandlerWrapper(func, new JsonSerializer()))
            using(var bootstrap = new LambdaBootstrap(handlerWrapper))
            {
                await bootstrap.RunAsync();
            }
        }

        public Function()
        {
            _s3 = new AmazonS3Client();

            ResolveConfig();
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        ///
        /// To use this handler to respond to an AWS event, reference the appropriate package from 
        /// https://github.com/aws/aws-lambda-dotnet#events
        /// and change the string input parameter to the desired event type.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(string input, ILambdaContext context)
        {
            _km.Refresh();

            var reply = await ToUpperByRpc(input);
            return System.Text.Json.JsonSerializer.Serialize(reply);
        }

        public async Task<UpperReply> ToUpperByRpc(string input)
        {
            using var httpHandler = new HttpClientHandler()
            {
                // If the API is behind a self-signed cert (for testing and
                // demonstration purposes) we need to ignore the TLS cert errors
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                CheckCertificateRevocationList = false,

                // Support any of the relatively recent TLS protos
                SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,

                PreAuthenticate = true,
                UseDefaultCredentials = true,
            };
            using var negTokHandler = new NegotiatedTokenHandler(TokenPath,httpHandler);
            using var httpClient = new HttpClient(negTokHandler);

            var grpcChannelOptions = new GrpcChannelOptions
            {
                HttpClient = httpClient,
                DisposeHttpClient = false,
            };

            using var channel = GrpcChannel.ForAddress(ServerAddress, grpcChannelOptions);
            var client = new Greeter.GreeterClient(channel);

            // var helloWorld = await client.SayHelloAsync(
            //     new HelloRequest { Name = "World" });
            // Console.WriteLine("HelloWorldMessage: {0}", helloWorld.Message);
            // return helloWorld.Message;

            var upperReply = await client.ToUpperAsync(
                new UpperRequest { Input = input });
            Console.WriteLine("Got RPC response:"
                + $" [{System.Text.Json.JsonSerializer.Serialize(upperReply)}]");

            return upperReply;
        }

        private void ResolveConfig()
        {
            // Resolve Kerberos configuration overrides
            KerberosRealm = Environment.GetEnvironmentVariable(nameof(KerberosRealm))
                ?? KerberosRealm;
            KerberosRealmKdc = Environment.GetEnvironmentVariable(nameof(KerberosRealmKdc))
                ?? KerberosRealmKdc;
            KerberosPrincipal = Environment.GetEnvironmentVariable(nameof(KerberosPrincipal))
                ?? KerberosPrincipal;
            KerberosKeytabS3Path = Environment.GetEnvironmentVariable(nameof(KerberosKeytabS3Path))
                ?? KerberosKeytabS3Path;

            ServerAddress = Environment.GetEnvironmentVariable(nameof(ServerAddress))
                ?? ServerAddress;
            TokenPath = Environment.GetEnvironmentVariable(nameof(TokenPath))
                ?? TokenPath;
        }

        private async Task InitKerberos()
        {
            _km = new KerberosManager(new KerberosOptions
            {
                Realm = KerberosRealm,
                RealmKdc = KerberosRealmKdc,
                Principal = KerberosPrincipal,
            });

            // S3 Path value is expected in <bucket>/<key> format
            var s3BucketKeyPath = KerberosKeytabS3Path.Split("/", 2, StringSplitOptions.RemoveEmptyEntries);
            if (s3BucketKeyPath.Length != 2)
            {
                throw new Exception("invalid Keytab S3 path");
            }

            var getObjectRequ = new GetObjectRequest
            {
                BucketName = s3BucketKeyPath[0],
                Key = s3BucketKeyPath[1],
            };

            Console.WriteLine($"Retrieving Kerberos keytab from bucket [{getObjectRequ.BucketName}] key path [{getObjectRequ.Key}]");

            using (var getResp = await _s3.GetObjectAsync(getObjectRequ))
            using (getResp.ResponseStream)
            {
                _km.Init(getResp.ResponseStream);
            }
        }
    }
}
