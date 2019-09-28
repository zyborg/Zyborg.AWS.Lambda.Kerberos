using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Data.SqlClient;
using Zyborg.AWS.Lambda.Kerberos;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace Sample1
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

        private string SqlServer = "mssql1.example.com";

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

            return await ToUpperByMsSql(input);
        }

        public async Task<string> ToUpperByMsSql(string input)
        {
            var sqlCsb = new SqlConnectionStringBuilder
            {
                DataSource = SqlServer,
                IntegratedSecurity = true,
            };

            using var sqlCon = new SqlConnection(sqlCsb.ConnectionString);
            await sqlCon.OpenAsync();

            var sqlPrm = new SqlParameter
            {
                ParameterName = "@input",
                SqlDbType = System.Data.SqlDbType.VarChar,
                Size = input.Length + 1,
                Value = input,
            };

            using var sqlCmd = sqlCon.CreateCommand();
            sqlCmd.CommandText = @"
                SELECT SYSTEM_USER AS SystemUser
                       ,CURRENT_USER AS CurrentUser
                       ,CURRENT_TIMESTAMP AS CurrentTS
                       ,DB_NAME() AS CurrentDB
                       ,UPPER(@input) as ToUpperCase";
            sqlCmd.Parameters.Add(sqlPrm);
            sqlCmd.Prepare();

            using var sqlDat = await sqlCmd.ExecuteReaderAsync();
            var colNames = sqlDat.GetColumnSchema().Select(c => c.ColumnName).ToArray();
            var colValues = new object[colNames.Length];
            var result = new StringBuilder();

            while (await sqlDat.ReadAsync())
            {
                sqlDat.GetValues(colValues);

                var colNameValues = colValues.Select((v, i) => $"[{colNames[i]}]=[{v}]");
                result.Append(string.Join(";", colNameValues)).Append("\r\n");
            }

            return result.ToString();
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

            SqlServer = Environment.GetEnvironmentVariable(nameof(SqlServer))
                ?? SqlServer;
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
