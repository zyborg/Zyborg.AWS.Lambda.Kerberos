using System;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Zyborg.AWS.Lambda.Kerberos;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace Sample3
{
    public class Function
    {
        private KerberosManager _km;

        private IAmazonS3 _s3;

        private bool _kerberosInitialized = false;
        private SemaphoreSlim _kerberosInitLock = new SemaphoreSlim(1);
        private Mutex _kerberosInitMutex = new Mutex();

        private string KerberosRealm = "EXAMPLE.COM";
        private string KerberosRealmKdc = "DC1.EXAMPLE.COM";
        private string KerberosPrincipal = "sample_user@EXAMPLE.COM";
        private string KerberosKeytabS3Path = "default-s3-bucket/default-s3-key/path/to/a/keytab";

        private string NsServer = "dc1.example.com";

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            _s3 = new AmazonS3Client();

            ResolveConfig();
        }

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(string input, ILambdaContext context)
        {
            // We want to initialize the KerberosManager with a keytab that we pull down from S3
            // but we want to make sure we only do this once.  Since this requires the use of
            // async operations we cannot do it in the constructor and instead do it in the main
            // handler, but we have to guard the operation to make sure it only happens once.
            if (!_kerberosInitialized)
            {
                Console.WriteLine("Kerberos needs initialization");
                await InitKerberosOnce();
            }

            _km.Refresh();

            return await UpdateWindowsDNS();
        }

        public async Task<string> UpdateWindowsDNS()
        {
            var cmd = "/var/task/local/nsupdate";

            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // Enable Kerberos
            psi.ArgumentList.Add("-g");
            // Enable debugging
            //psi.ArgumentList.Add("-d");
            // Enable over TCP (default is UDP)
            //ps1.ArgumentList.Add("-v");

            var proc = Process.Start(psi);
            
            var nsupdateCommands = new[] {
                $"server {NsServer}",
                $"update add lambda-nsupdate-{DateTime.Now.ToString("yyyyMMddHHmmss")}.{KerberosRealm}"
                    + $" 600 TXT \"Lambda Sample3 was here at {DateTime.Now}\"",
                $"send",
            };

            foreach (var c in nsupdateCommands)
            {
                await proc.StandardInput.WriteAsync(c);
                await proc.StandardInput.WriteAsync("\r\n");
            }
            await proc.StandardInput.FlushAsync();
            proc.StandardInput.Close();

            proc.WaitForExit();
            var stdOut = await proc.StandardOutput.ReadToEndAsync();
            var stdErr = await proc.StandardError.ReadToEndAsync();

            Console.WriteLine($"STDOUT: [{stdOut}]");
            Console.WriteLine($"STDERR: [{stdErr}]");

            if (proc.ExitCode != 0)
            {
                if (!string.IsNullOrEmpty(stdErr))
                {
                    throw new Exception("NSUPDATE did not exit cleanly, STDERR: " + stdErr);
                }
                else
                {
                    throw new Exception("NSUPDATE did not exist cleanly -- no STDERR output");
                }
            }

            return "DONE!";
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

            NsServer = Environment.GetEnvironmentVariable(nameof(NsServer))
                ?? NsServer;
        }

        private async Task InitKerberosOnce()
        {
            try
            {
                // Only one thread should initialize the keytab
                Console.WriteLine("Waiting to lock to initialize Kerberos...");
                _kerberosInitLock.Wait();

                // Double-check to make sure another thread did not already take care of this
                if (_kerberosInitialized)
                {
                    Console.WriteLine("Kerberos already initialized by another task, SKIPPING");
                    return;
                }
                
                Console.WriteLine("Initializing Kerberos...");
                await InitKerberos().ConfigureAwait(true);

                _kerberosInitialized = true;
                Console.WriteLine("...Kerberos initialized");
            }
            finally
            {
                Console.WriteLine("Releasing lock");
                _kerberosInitLock.Release();
            }
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
