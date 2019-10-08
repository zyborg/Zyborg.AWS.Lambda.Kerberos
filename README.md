
# Zyborg.AWS.Lambda.Kerberos

Helps implement Kerberos-aware/authenticated AWS Lambda functions.

---

This package includes support for enabling Kerberos authentication in an AWS Lambda
context using native Linux tooling and support.  This can be used to allow Lambda
functions to interact with Kerberos-authenticated services.

Some examples of where this could be useful include

* **Windows Authenticated Web Sites** - calling into a Web Site or Web Service that
  is authenticated with the use of Windows authentication
* **SQL Server Integrated Authentication** - interacting with Microsoft SQL Server
  using an AD identity instead of SQL credentials
* **Kerberos-authenticated Services** - interacting with various services hosted on an
  Active Directory domain controller (such as the DNS service in Active Directory).

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
PS> ktpass /princ username@EXAMPLE.COM /crypto AES256-SHA1 /ptype KRB5_NT_PRINCIPAL /out username.keytab /pass password

## Or, password prompted (BETTER!)
PS> ktpass /princ username@EXAMPLE.COM /crypto AES256-SHA1 /ptype KRB5_NT_PRINCIPAL /out username.keytab /pass *

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

### [Sample2](src/Sample1) - Interacting with Microsoft SQL Server w/ Integrated Authentication

One of the most predominant use cases, and the one initially inspiring this solution,
is having Lambda functions interact with a SQL Server (MSSQL) database using
_integrated authentication_.  Specifically for MSSQL, the latest
[SQL Client](https://github.com/dotnet/SqlClient) supports integrated authentication
on the Linux platform with support from native Kerberos tooling and libraries.

This library, together with the latest SQL Client, allows your Lambda function to talk
to SQL Server using integrated authentication which can be a requirement in some
scenarios where mixed-mode authentication is undesirable or disallowed altogether.

### [Sample2](src/Sample2) - Windows Authentication to a Web Site or Web Service

A common scenario, especially in private networks is to make use of Windows
Integrated Authentication when talking to various intranet Web Sites or Web Services.
In this scenario, Windows Authentication provides a means of SSO so users need
not worry about multiple identities.

These sites are typically hosted behind IIS, although support does exist on Linux
systems when fronted by Apache or NGINX.  Most recently, with the .NET Core 3.0
release, standalone Kestrel applications have now added support across multiple
platforms for [integrated Windows Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/windowsauth?view=aspnetcore-3.0&tabs=visual-studio#kestrel).

Using this library, it's now possible for Lambda functions to interact with these
sites in a simple and straightforward manner.  This sample also includes a companion
[Web API project](src/Sample2.Server) that can be used either standalone or fronted
by IIS to demonstrate the Lambda support for Windows Authentication.

### [Sample3](src/Sample3) - Using Native Kerberos-enabled Linux Tooling

There are numerous tools native to the Linux platform that have been _Kerberized_
and in fact some of these allow interacting with, and managing various resources
or services that are part of an Active Directory domain.

One such service that is critical to the fundamental operation of AD is DNS, and
the DNS tooling on Linux, namely the `nsupdate` CLI tool, has incorporated native
Kerberos authentication for quite a while.

In this example, we show how to interact with AD domain controllers which host
the AD DNS system using nsupdate and its supporting libraries to manage DNS
records.  This is a very simple example, but you can extrapolate this to see how
one might be able to build a system that periodically refreshes records based
on the state of an AD network, or scavenges records that might represent outdated
or non-responding resources.
