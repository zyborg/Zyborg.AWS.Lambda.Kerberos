# README - Sample2 Server-side WebAPI

This project implements a trivial, sample WebAPI that hosts one primary API endpoint,
namely `/toupper`, which takes a query parameter named `input` and spits out a JSON
result object that includes the input value upper-cased, as well as details of
the callers identity.

It's meant as a complement to the `Sample2` project which demonstrates how to call
APIs that require Windows Authentication (Kerberos, Negotiate) from a Lambda function.
Therefore it has some requirements for how it must be setup and configured to properly
demonstrate this functionality.

This project is implemented as a ASP.NET Core 3.0 app because 3.0 is the first version
that supports a standalone Kestrel mode with support for Kerberos authentication.
It can also be deployed behind IIS and leverage IIS' native support for Kerberos
authentication.

A flag in the `appsettings.json` file named `UseAuthentication` is a boolean setting
that indicates whether the Kerberos authentication is implemented within in the app
itself or not.  When running behind IIS, this setting should be disabled and IIS should
be configured with NTLM and/or Keberos authentication.
