Sharpbrake
==========

![The Sharpbrake notifier for C#/.NET](https://s3.amazonaws.com/airbrake-github-assets/sharpbrake/arthur-sharpbrake.jpeg)

Introduction
------------

[Airbrake](https://airbrake.io) is an online tool that provides robust exception
tracking in most of your C#/.NET applications. In doing so, it allows you to
easily review errors, tie an error to an individual piece of code, and trace the
cause back to recent changes. The Airbrake dashboard provides easy
categorization, searching, and prioritization of exceptions so that when errors
occur, your team can quickly determine the root cause.

Sharpbrake is a C# notifier library for Airbrake. It provides minimalist API to
send C# exceptions to the Airbrake dashboard. The library perfectly suits any
type of C# applications.

**Important!** Use the current notifier only if your application targets .NET Framework
before the version 4.5.2. Refer to [sharpbrake](https://github.com/airbrake/sharpbrake)
to get a support of the modern .NET Frameworks (4.5.2+ and .NET Core).

Key features
------------

* Uses the new Airbrake JSON
  (v3)<sup>[[link](https://airbrake.io/docs/#create-notice-v3)]</sup>
* SSL support (all communication with Airbrake is encrypted by default)
* Support for .NET 3.5 and .NET 4.5 (up to .NET 4.5.2)
* Asynchronous exception reporting<sup>[[link](#notify)]</sup>
* Logging support<sup>[[link](#logfile)]</sup>
* Flexible configuration options (configure as many Airbrake notifiers in one
  application as you want)<sup>[[link](#configuration)]</sup>
* Support for environments<sup>[[link](#environment)]</sup>
* Support for proxying<sup>[[link](#proxy)]</sup>
* Filters support (filter out sensitive or unwanted data that shouldn't be sent
  to our servers)<sup>[[link](#blacklistkeys)]</sup>
* Ability to ignore errors from specified environments<sup>[[link](#ignoreenvironments)]</sup>
* Severity support<sup>[[link](#setting-severity)]</sup>

The library comes with the following integrations:

* Web frameworks
  * ASP.NET HTTP Module<sup>[[link](#aspnet-http-module)]</sup>

Installation
------------

### NuGet

Package                    | Description                                            | NuGet link
---------------------------|--------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------
Sharpbrake.Client          | C# client with support for .NET 3.5 and 4.5            | 
Sharpbrake.Http.Module     | HTTP module for ASP.NET request pipeline               | 

```
PM> Install-Package Sharpbrake.Client -Version 3.2.0
```

Examples
--------

### Basic example

This is the minimal example that you can use to test Sharpbrake with your
project.

```csharp
using System;
using Sharpbrake.Client;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var airbrake = new AirbrakeNotifier(new AirbrakeConfig
            {
                ProjectId = "113743",
                ProjectKey = "81bbff95d52f8856c770bb39e827f3f6"
            });

            try
            {
                throw new Exception("Oops!"));
            }
            catch (Exception ex)
            {
                var response = airbrake.NotifyAsync(ex).Result;
                Console.WriteLine("Status: {0}, Id: {1}, Url: {2}", response.Status, response.Id, response.Url);
            }
        }
    }
}
```

Configuration
-------------

Before using the library and its notifiers, you must configure them. In most
cases, it is sufficient to configure only one, default, notifier.

```csharp
var airbrake = new AirbrakeNotifier(new AirbrakeConfig
   {
       ProjectId = "113743",
       ProjectKey = "81bbff95d52f8856c770bb39e827f3f6"
   });
```

#### ProjectId & ProjectKey

You **must** set both `ProjectId` & `ProjectKey`.

To find your `ProjectId` and `ProjectKey` navigate to your project's _General
Settings_ and copy the values from the right sidebar.

![](https://s3.amazonaws.com/airbrake-github-assets/sharpbrake/project-id-key.png)

There are multiple ways to set these values:

* Setting explicitly:

  ```csharp
  var config = new AirbrakeConfig {
      ProjectId = "127348",
      ProjectKey = "e2046ca6e4e9214b24ad252e3c99a0f6"
  };
  ```

* Using `App.config` or `Web.config`:

  ```xml
  <appSettings>
      <add key="Airbrake.ProjectId" value="127348" />
      <add key="Airbrake.ProjectKey" value="e2046ca6e4e9214b24ad252e3c99a0f6" />
  </appSettings>
  ```

  ```csharp
  var settings = ConfigurationManager.AppSettings.AllKeys
      .Where(key => key.StartsWith("Airbrake", StringComparison.OrdinalIgnoreCase))
      .ToDictionary(key => key, key => ConfigurationManager.AppSettings[key]);

  var airbrakeConfiguration = AirbrakeConfig.Load(settings);
  ```

#### LogFile

The library can log responses from Airbrake via the `LogFile` option. The option
accepts a path to the log file. Supports relative (to your app's executable) and
absolute paths. By default, it's not set.

```csharp
var config = new AirbrakeConfig {
    LogFile = "airbrake.log"
};
```

#### Environment

Configures the environment the application is running in. Helps the Airbrake
dashboard to distinguish between exceptions occurring in different
environments. By default, it's not set.

```csharp
var config = new AirbrakeConfig {
    Environment = "production"
};
```

#### AppVersion

The version of your application that you can pass to differentiate exceptions
between multiple versions. It's not set by default.

```csharp
var config = new AirbrakeConfig {
    AppVersion = "1.0.1"
};
```

#### Host

By default, it is set to `airbrake.io`. A `host` is a web address containing a
scheme ("http" or "https"), a host and a port. You can omit the port (80 will be
assumed) and the scheme ("https" will be assumed).

```csharp
var config = new AirbrakeConfig {
    Host = "http://127.0.0.1:8000"
};
```

#### Proxy options

If your server is not able to directly reach Airbrake, you can use a built-in
proxy. By default, Sharpbrake uses a direct connection.

##### ProxyUri, ProxyUsername, ProxyPassword

```csharp
var config = new AirbrakeConfig {
    ProxyUri = "http://46.166.165.63:8080",
    ProxyUsername = "username",
    ProxyPassword = "s3kr3t"
};
```

#### IgnoreEnvironments

Setting this option allows Airbrake to filter exceptions occurring in unwanted
environments such as `test`. By default, it is not set, which means Sharpbrake
sends exceptions occurring in all environments.

```csharp
var config = new AirbrakeConfig {
    IgnoreEnvironments = new List<string> { "development" }
};
```

#### BlacklistKeys

Specifies which keys in the payload (parameters, session data, environment data,
etc) should be filtered. Before sending an error, filtered keys will be
substituted with the `[Filtered]` label.

```csharp
var config = new AirbrakeConfig {
    BlacklistKeys = new List<string> { "password", "creditCard", "email" }
};

// The dashboard will display this parameter as filtered, but other values won't
// be affected:
//   { user: 'John',
//     password: '[Filtered]',
//     email: '[Filtered]',
//     creditCard: '[Filtered]' }
```

**Note:** `BlacklistKeys` has higher priority than `WhitelistKeys`. It means
that if you set the same value into both blacklist and whitelist - that value
will be filtered out.

#### WhitelistKeys

Specifies which keys in the payload (parameters, session data, environment data,
etc) should _not_ be filtered. All other keys will be substituted with the
`[Filtered]` label.

```csharp
var config = new AirbrakeConfig {
    WhitelistKeys = new List<string> { "user", "email", "accountId" }
};

// The dashboard will display this parameter as is, but all other values will be
// filtered:
//   { user: 'John',
//     password: '[Filtered]',
//     email: 'john@example.com',
//     accountId: 42 }
```

API
---

### AirbrakeConfig

#### Load

`AirbrakeConfig.Load` accepts a `Dictionary` consisting of config option names
and their values.

```csharp
// Construct a dictionary with configuration options
var settings = ConfigurationManager.AppSettings.AllKeys
    .Where(key => key.StartsWith("Airbrake", StringComparison.OrdinalIgnoreCase))
    .ToDictionary(key => key, key => ConfigurationManager.AppSettings[key]);

// Create a config from that dictionary
var config = AirbrakeConfig.Load(settings);
```

### AirbrakeNotifier

#### Notify

`Notify` asynchronously sends an exception to the Airbrake dashboard and logs a
response from Airbrake. It's a convenience method
around [`NotifyAsync`](#notifyasync).

```csharp
try
{
    throw new Exception();
}
catch(Exception ex)
{
    airbrake.Notify(ex);
}
```

#### NotifyAsync

`NotifyAsync` is similar to [`Notify`](#notify), however it's more powerful
because it provides control over the response object from Airbrake. You can
subscribe to the `NotifyCompleted` event and define your custom logic in an
event handler:

```csharp
airbrake.NotifyCompleted += (sender, eventArgs) =>
{
    airbrakeResponse = eventArgs.Result;
    Console.WriteLine(airbrakeResponse.Url);
};

airbrake.NotifyAsync(ex);
```

#### `AddFilter`

A notice can be customized or ignored before it is sent to Airbrake via
`AddFilter`. A lambda expression that is passed to the `AddFilter` method
accepts a `Notice` that can be processed by your code. The `Notice` object is
pre-populated with errors, context and params, so you can freely modify these
values if you wish. The `Notice` object is not sent to Airbrake if the lambda
expression returns `null`:

```csharp
airbrake.AddFilter(notice =>
{
    // ignore notice if email is "test@example.com"
    if (notice.Context.User.Email == "test@example.com")
        return null;

    // clear environment variables with "token"-related keys
    foreach (var key in notice.EnvironmentVars.Keys)
        if (key.Contains("token"))
            notice.EnvironmentVars[key] = string.Empty;

    return notice;
});
```

### Notice

`Exception` and `HttpContext` are properties that can be used to retrieve the
values that the `Notice` object was built from.

#### Exception

Used to access additional exception properties. For example, if your exception is
an `HttpException`, you can ignore it if `GetHTTPCode()` returns 404:

```csharp
airbrake.AddFilter(notice =>
{
    var exception = notice.Exception as HttpException;
    if (exception != null && exception.GetHttpCode() == 404)
        return null;

    return notice;
});
```

#### HttpContext

Used to retrieve HTTP context properties:

```csharp
airbrake.AddFilter(notice =>
{
    notice.Params["response"] = notice.HttpContext.Response

    return notice;
});
```

**Note:** Notice that exceeds 64 KB is truncated before sending.

#### Setting severity

[Severity][what-is-severity] allows categorizing how severe an error is. By
default, it's set to `error`. To redefine severity, simply pass the `Severity`
as a parameter to the `NotifyAsync` (or `Notify`) method. For example:

```csharp
airbrake.NotifyAsync(ex, null,
    Severity.Critical
).Result;
```

ASP.NET Integration
-------------------

### ASP.NET HTTP Module

1. Install the `Sharpbrake.Http.Module` package from NuGet (you can use "Package
   Manager Console" from Visual Studio):

  ```
  PM> Install-Package Sharpbrake.Http.Module -Version 3.2.0
  ```

2. Configure `appSettings` in `Web.config` ([how to configure](#configuration)):

  ```xml
  <appSettings>
      <add key="Airbrake.ProjectId" value="113743"/>
      <add key="Airbrake.ProjectKey" value="81bbff95d52f8856c770bb39e827f3f6"/>
  </appSettings>
  ```

3. Add the `AirbrakeHttpModule` module to your `system.webServer` in
   `Web.config`:

  ```xml
  <system.webServer>
      <modules>
          <add name="Airbrake" type="Sharpbrake.Http.Module.AirbrakeHttpModule, Sharpbrake.Http.Module"/>
      </modules>
  </system.webServer>
  ```

Contributing
------------

* [Contribution guide](CONTRIBUTING.md)
* [Sharpbrake developer documentation](https://github.com/airbrake/sharpbrake/blob/master/docs/developer-howto.md)

License
-------

The project uses the MIT License. See [LICENSE.md](LICENSE.md) for details.

[what-is-severity]: https://airbrake.io/docs/airbrake-faq/what-is-severity/
