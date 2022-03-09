using System;
using Microsoft.Extensions.Logging;

namespace Zyborg.AWS.Lambda.Kerberos
{
    public class KerberosOptions
    {
        /// <summary>
        /// The interval of time to obtain a new Ticket-Granting Ticket (TGT).
        /// Defaults to 24 hours.
        /// </summary>
        public TimeSpan TicketLifetime { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// The interval of time to renew the Ticket-Granting Ticket (TGT).
        /// Defaults to 7 days.
        /// </summary>
        public TimeSpan TicketRenewLifetime { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// The Kerberos realm (or domain for an AD network).
        /// </summary>
        public string Realm { get; set; }

        /// <summary>
        /// Specify the hostname for the Key Distribution Center (KDC) for the Kerberos
        /// realm.  <see cref="https://github.com/Microsoft/vscode-mssql/blob/master/KERBEROS_HELP.md#step-1-find-kerberos-kdc-key-distribution-center-1">
        /// Here</see> is one way to discover this if you are on a Windows machine in the
        /// Kerberos realm/domain.
        /// You <i>may</i> leave this unspecified if you provide a value for the
        /// <c>RealmKdcSrvName</c> which will be used to resolve the KDC host at
        /// initialization.
        /// </summary>
        public string RealmKdc { get; set; }

        /// <summary>
        /// Specifies a DNS SRV record name that will be used to resolve the Realm KDC at
        /// startup.  A typical record would look like
        /// <c>_kerberos._tcp.dc._msdcs.YOUR_DOMAIN_NAME</c>.
        /// </summary>
        public string RealmKdcSrvName { get; set; }

        /// <summary>
        /// The fully qualified principal to authenticate as in fully-qualified form
        /// of:  <c>username@REALM</c>
        /// </summary>
        public string  Principal { get; set; }

        /// <summary>
        /// Logger used for writing output by KerberosManager.
        /// </summary>
        /// <remarks>
        /// By default a simple consoler logger is used if this value is not
        /// specified to preserve prior behavior.  You can specify any logger
        /// of your choosing, even the <see cref="NullLogger" /> to suppress
        /// any logging.
        /// </remarks>
        public ILogger Logger { get; set; }
    }
}