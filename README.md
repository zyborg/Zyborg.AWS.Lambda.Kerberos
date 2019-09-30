
# Zyborg.AWS.Lambda.Kerberos

Helps implement Kerberos-aware/authenticated AWS Lambda functions.

---

This package includes support for enabling Kerberos authentication in an AWS Lambda
context using native Linux tooling and support.  This can be used to allow Lambda
functions to interact with Kerberos-authenticated services.

Some examples of where this could be useful include

* calling into a Web Site or Web Service that is authenticated with the use of Windows
  authentication,
* interacting with Microsoft SQL Server using _integrated authentication_, or
* interacting with various services hosted on an Active Directory domain
controller (such as the DNS service in Active Directory).

This repository includes samples that demonstrate all three of these scenarios
as detailed [below](./README.md#samples).

## Overview and Implementation Details

This library incorporates native Linux tools from the
[Kerberos tooling (KRB5)](http://web.mit.edu/kerberos/www/) project to manage tickets
in a Kerberos realm (such as Active Directory) from within the context of a
Lambda execution environment hosting the .NET Core runtime.  The tooling was assembled
based on the environment of an Amazon Linux AMI as described in the [Lambda Runtimes
documentation](https://docs.aws.amazon.com/en_pv/lambda/latest/dg/lambda-runtimes.html).

### The `KerberosManager` Class

The primary entry into this functionality is the `KerberosManager` class.  You
construct an instance of this class while passing in an instance of the
`KerberosOptions` class which provides all the necessary configuration details.
(The configuration options are detailed further below.)

```csharp
KerberosManager km = new KerberosManager(new KerberosOptions
{
    Realm = "EXAMPLE.COM",
    RealmKdc = "DC1.example.com",
    Principal = "service-user@EXAMPLE.COM",
});
```

### Initializing with a _keytab_

With the manager instance constructed, you have to initialize the Kerberos context with
a **keytab** file that contains the credentials of the principal that you want the Lambda
function to operate under.  This will cache the keytab file for the life of the Lambda
function, and then initialize the Kerberos context with an initial ticket-granting
ticket (TGT).  This initialization step is typically performed at the start of the Lambda
lifecycle, such as in the Lambda function class constructor or the main entry point of
the Lambda program in a _custom runtime function_.

In this simple example, the keytab file is assumed to packaged up and deployed
with the Lambda function compiled code, and so it simply reads it in as a file.
This is a bit of a contrived example, and better and more secure approaches
are described later and demonstrated in the provided samples:

```csharp
using (var fs = System.IO.File.OpenRead("sample.keytab"))
{
    km.Init(fs);
}
```

### Refreshing the Kerberos Context

Subsequently, the Kerberos TGT will need to be periodically refreshed.  A
simple way to do this is to simply invoke the refresh operation at the start
of every Lambda function invocation.  The KerberosManager will automatically
determine if the ticket needs to be refreshed to preserve continuity in the
authenticated Kerberos context.  If so, it will renew the necessary ticket,
if not the refresh operation will resolve to a noop.

```csharp
km.Refresh();
```

## Configuring the `KerberosOptions`

Initializing the Kerberos Manager requires an of the `KerberosOptions` class which
defines the configuration details of the Kerberos environment.  Here are the options
that can be configured.

### `Realm`

This is the Kerberos realm, typically the domain of an Active Directory environment.
It should be specified in all capitals.

### `RealmKdc`


Identifies a Key Distribution Center (KDC) for the realm which will be used for ticket
granting requests.  The KDC is a domain controller for your domain.  If you are not sure
what it is you can discover it by using one of the following methods:

#### On Windows

You can issue this command on a domain-joined Windows computer, and capture the
`DC:` value to identify the KDC.

```pwsh
PS> nltest /dsgetdc:DOMAIN.COMPANY.COM

## The KDC/DC will be listed as "DC:"
```

#### On Linux

On a domain-joined Linux host, you can find the KDC in the `/etc/krb5.conf` file
identified under the target realm, by the KDC parameter.

### `Principal`

The `Principal` option indicates the user account that the Lambda function will be
identified as whenever it interacts with a Kerberos-authenticated service.  It needs
to be specified in a _fully-qualified_ form of `username@REALM`, with the realm
component typically in all caps.  For example:

```
some-user@EXAMPLE.COM
```







## Keytab

The _key table file_, or keytab for short, is a encrypted form of the Kerberos credentials
for one or more domain user credentials.  The KerberosManager uses a keytab in order to
present credentials for the Principal under which the Lambda function will be executed.
You need to initialize the KerberosManager by providing it an input stream that contains
the contents of a keytab.

There are many ways that you can achieve this, but one way is to generate a keytab file
offline and independent of the Lambda function on a Windows or Linux host and then make
it available to the Lambda function as an embedded resource or dynamically as an external
resource, such as from an S3 bucket or even an SSM parameter value.

To create a keytab file:

* On Windows:

```pwsh
## Password inline
PS> ktpass /princ username@EXAMPLE.COM /pass password /crypto AES256-SHA1 /ptype KRB5_NT_PRINCIPAL /out username.keytab

## Or, password prompted (BETTER!)
PS> ktpass /princ username@EXAMPLE.COM /pass *        /crypto AES256-SHA1 /ptype KRB5_NT_PRINCIPAL /out username.keytab

```

* On Linux:

```pwsh
## ktutil is an interactive keytab editor
PS> ktutil
  ktutil:  addent -password -p username@EXAMPLE.COM -k 1 -e aes256-cts
  Password for username@EXAMPLE.COM: [enter your password]
  ktutil:  wkt username.keytab
  ktutil:  quit
```

More information can be found here [here](https://kb.iu.edu/d/aumh).


## Samples

This repository contains samples that demonstrate three useful and
common scenarios that take advantage of this package, as described
here.

### Sample1 - Windows Authentication to a Web Site or Web Service

### Sample2 - Interacting with Microsoft SQL Server w/ Integrated Authentication

One of the most predominant use cases, and the one initially inspiring this solution,
is having Lambda functions interact with a SQL Server (MSSQL) database using
_integrated authentication_.  Specifically for MSSQL, the latest
[SQL Client](https://github.com/dotnet/SqlClient) supports integrated authentication
on the Linux platform with support from native
.

### Sample3 - Using Native Kerberos-enabled Linux Tooling

