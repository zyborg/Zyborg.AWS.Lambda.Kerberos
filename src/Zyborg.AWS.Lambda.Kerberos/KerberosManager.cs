using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Zyborg.AWS.Lambda.Kerberos
{
    public class KerberosManager
    {
        /// <summary>
        /// This key will be searched for in the current set of process environment
        /// variables to determine if we are running in the context of a Lambda runtime.
        /// </summary>
        public const string AwsLambdaFuncNameEnvKey = "AWS_LAMBDA_FUNCTION_NAME";

        public const string LambdaWriteDir = "/tmp";
        public const string LambdaTaskDir = "/var/task";

        public const string LocalBinDir = LambdaTaskDir + "/local";
        public const string LocalLibDir = LambdaTaskDir + "/lib";
        public const string LocalEtcDir = LambdaTaskDir + "/etc";

        public const string KinitPath = LocalBinDir + "/kinit";
        public const string KlistPath = LocalBinDir + "/klist";

        public const string Krb5ConfigSource = LocalEtcDir + "/lambda-krb5.conf";
        public const string Krb5ConfigTarget = LambdaWriteDir + "/lambda-krb.conf";
        public const string Krb5KeyTabTarget = LambdaWriteDir + "/lambda.keytab";
        public const string Krb5CCacheTarget = LambdaWriteDir + "/lambda.ccache";

        public const string Krb5ConfigEnvKey = "KRB5_CONFIG";

        private bool _isLinux;
        private string _awsLambdaFuncName;

        private DateTime _lastKinit = DateTime.MinValue;
        private object _lastKinitLock = new object();

        private string _kinitArgs;
        private ProcessStartInfo _kinitStartInfo;

        public KerberosManager(KerberosOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            Options = options;

            _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            _awsLambdaFuncName = Environment.GetEnvironmentVariable(AwsLambdaFuncNameEnvKey);

            Enabled = _isLinux && !string.IsNullOrEmpty(_awsLambdaFuncName);

            Console.WriteLine($"Kerberos Manager is [{(Enabled ? "ENABLED" : "DISABLED")}]:");
            Console.WriteLine("* Is Linux: " + _isLinux);
            Console.WriteLine("* Lambda Function Name: " + _awsLambdaFuncName);

            // DEBUG:
            // foreach (var env in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().OrderBy(e => e.Key))
            // {
            //     Console.WriteLine($"ENV: [{env.Key}]=[{env.Value}]");
            // }
        }

        /// <summary>
        /// Indicates if the Kerberose Manager is enabled and will perform any management
        /// of Kerberos.  For this to resolve to true, the Manager must resolve that the
        /// running environment is Linux and that is running within a Lambda runtime context.
        /// When disabled the Manager will perform no actions during its initialization and
        /// refresh operations (noop).
        /// </summary>
        /// <value></value>
        public bool Enabled { get; }

        internal KerberosOptions Options { get; }

        public void Init(Stream keytab)
        {
            if (!Enabled)
                return;

            _kinitArgs = $"{Options.Principal} -k";

            Console.WriteLine("Persisting KRB5 configuration");
            PrepareKrb5Config();

            Console.WriteLine("Persisting KRB5 keytab");
            using (var fs = new FileStream(Krb5KeyTabTarget, FileMode.Create))
            {
                keytab.CopyTo(fs);
            }

            _kinitStartInfo = new ProcessStartInfo()
            {
                FileName = KinitPath,
                Arguments = _kinitArgs,
                UseShellExecute = false,
            };
    
            Console.WriteLine($"Initializing Kerberos TGT for principal [{Options.Principal}]");
            Process.Start(_kinitStartInfo).WaitForExit();
            _lastKinit = DateTime.Now;
            Console.WriteLine($"...completed at [{_lastKinit}]");
        }

        void PrepareKrb5Config()
        {
            Console.WriteLine("Reading in KRB5 configuration template...");
            var configSource = File.ReadAllText(Krb5ConfigSource);
            var configTarget = TemplateEvaluator.Eval(configSource, this);
            File.WriteAllText(Krb5ConfigTarget, configTarget);
            Console.WriteLine($"...wrote out KRB5 configuration to [{Krb5ConfigTarget}]");

            // Export twice, for our benefit, as well as any spawned children
            Environment.SetEnvironmentVariable(Krb5ConfigEnvKey, Krb5ConfigTarget);
            NativeEnv.SetEnv(Krb5ConfigEnvKey, Krb5ConfigTarget);
        }

        public void Refresh(bool force = false)
        {
            if (!Enabled)
                return;

            if (force || (DateTime.Now - _lastKinit) >= Options.TicketLifetime)
            {
                lock (_lastKinitLock)
                {
                    if (force || (DateTime.Now - _lastKinit) > Options.TicketLifetime)
                    {
                        Console.WriteLine("Kerberos TGT age as expired, regenerating...");
                        Process.Start(_kinitStartInfo).WaitForExit();
                        _lastKinit = DateTime.Now;
                        Console.WriteLine($"...completed at [{_lastKinit}]");
                    }
                }
            }
        }
    }
}