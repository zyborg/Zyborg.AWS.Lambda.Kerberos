using System;

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
        public string RealmKdc { get; set; }

        /// <summary>
        /// The fully qualified principal to authenticate as in fully-qualified form
        /// of:  <c>username@REALM</c>
        /// </summary>
        public string  Principal { get; set; }
    }
}