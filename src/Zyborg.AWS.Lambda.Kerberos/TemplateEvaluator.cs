using System;
using System.Collections.Generic;

namespace Zyborg.AWS.Lambda.Kerberos
{
    /// <summary>
    /// A simple utility class to implement basic variable substitution/expansion
    /// in template strings in the context of Kerberos Manager/Options.
    /// </summary>
    public class TemplateEvaluator
    {
        private static Dictionary<string, Func<KerberosManager, string>> _replacements =
            new Dictionary<string, Func<KerberosManager, string>>
            {
                ["@@REALM-UCASE@@"] = (km) => km.Options.Realm.ToUpper(),
                ["@@REALM-LCASE@@"] = (km) => km.Options.Realm.ToLower(),
                ["@@KDC-HOSTNAME@@"] = (km) => km.Options.RealmKdc.ToUpper(),
                ["@@DEFAULT-CCACHE@@"] = (km) => KerberosManager.Krb5CCacheTarget,
                ["@@DEFAULT-KEYTAB@@"] = (km) => KerberosManager.Krb5KeyTabTarget,

                ["@@TICKET-LIFETIME@@"] = (km) =>
                    km.Options.TicketLifetime.TotalSeconds.ToString(),
                ["@@TICKET-RENEW-LIFETIME@@"] = (km) =>
                    km.Options.TicketRenewLifetime.TotalSeconds.ToString(),
            };
        
        public static string Eval(string template, KerberosManager km)
        {
            var value = template;
            foreach (var r in _replacements)
            {
                var rValue = r.Value(km);
                value = value.Replace(r.Key, rValue);
            }
            return value;
        }
    }
}