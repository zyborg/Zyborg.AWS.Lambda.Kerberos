using System;
using System.Runtime.InteropServices;

namespace Zyborg.AWS.Lambda.Kerberos
{
    // This is needed because we couldn't set the environment such 
    // that it would be seen by the native Kerberos lib because of:
    //    https://yizhang82.dev/set-environment-variable
    public class NativeEnv
    {
        [DllImport("libc")]
        static extern IntPtr getenv(string name);

        [DllImport("libc")]
        static extern int setenv(string name, string value);

        public static string GetEnv(string name)
        {
            var env = getenv(name);
            var envStr = Marshal.PtrToStringAnsi(env);

            // TODO: do we need to release env here?

            return envStr;
        }

        public static void SetEnv(string name, string value)
        {
            setenv(name, value);
        }

        // static void SampleMain(string[] args)
        // {
        //     string envName = "MY_ENV";

        //     Console.WriteLine("MY_ENV={0}", Environment.GetEnvironmentVariable(envName));

        //     Environment.SetEnvironmentVariable(envName, "~/path");
        //     Console.WriteLine("Setting it to ~/path");

        //     Console.WriteLine("MY_ENV={0}", Environment.GetEnvironmentVariable(envName));

        //     IntPtr env = getenv(envName);
        //     string envStr = Marshal.PtrToStringAnsi(env);
        //     Console.WriteLine("getenv(MY_ENV)={0}", envStr);

        //     Console.WriteLine("Setting it using setenv");
        //     setenv(envName, "~/path");

        //     env = getenv(envName);
        //     envStr = Marshal.PtrToStringAnsi(env);
        //     Console.WriteLine("getenv(MY_ENV)={0}", envStr);
        // }        
    }
}