#tool nuget:?package=OpenCover&version=4.6.519
#tool nuget:?package=ReportGenerator&version=2.5.8
#tool nuget:?package=xunit.runner.console&version=2.1.0
#tool nuget:?package=NuGet.CommandLine&version=4.1.0

var target = Context.Argument("target", "Default");

var configuration =
    HasArgument("Configuration") ? Argument<string>("Configuration") :
    EnvironmentVariable("Configuration") != null ? EnvironmentVariable("Configuration") : "Release";

var buildSystem = Context.BuildSystem();

var isLocalBuild = buildSystem.IsLocalBuild;
var isRunningOnAppVeyor = buildSystem.AppVeyor.IsRunningOnAppVeyor;
var isRunningOnWindows = Context.IsRunningOnWindows();

var isPullRequest = buildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isBuildTagged = IsBuildTagged(buildSystem);

var buildNumber =
    HasArgument("BuildNumber") ? Argument<int>("BuildNumber") :
    isRunningOnAppVeyor ? AppVeyor.Environment.Build.Number :
    EnvironmentVariable("BuildNumber") != null ? int.Parse(EnvironmentVariable("BuildNumber")) : 0;

var artifactsDir = Directory("./artifacts");
var testResultsDir = Directory("./artifacts/test-results");
var nugetDir = System.IO.Path.Combine(artifactsDir, "nuget");

//
// Tasks
//

Task("Info")
    .Does(() =>
{
    Information("Target: {0}", target);
    Information("Configuration: {0}", configuration);
    Information("Build number: {0}", buildNumber);

    var projects = GetFiles("./src/**/*.csproj");
    foreach (var project in projects) {
        Information("{0} version: {1}", project.GetFilenameWithoutExtension(), GetVersion(project.FullPath));
    }
});

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
});

Task("Restore-Packages")
    .Does(() =>
{
    var nugetCmd = MakeAbsolute(new FilePath("tools/NuGet.CommandLine/tools/NuGet.exe"));
    var sln = MakeAbsolute(new FilePath("./Sharpbrake.NET35.sln"));

    var packageConfigs = GetFiles("./src/**/packages.config");
    packageConfigs.Add(GetFiles("./test/**/packages.config"));

    foreach (var packageConfig in packageConfigs)
    {
        using (var process = StartAndReturnProcess(
            nugetCmd,
            new ProcessSettings { Arguments = "restore " + packageConfig + " -SolutionDirectory " + sln.GetDirectory() }))
        {
            process.WaitForExit();
            if (process.GetExitCode() != 0)
                throw new Exception("Restoring packages for " + packageConfig.GetFilename() + " has failed!");
        }
    }
});

Task("Build")
    .IsDependentOn("Info")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-Packages")
    .Does(() =>
{
    var projects = GetFiles("./src/**/*.csproj");
    projects.Add(GetFiles("./test/**/*.csproj"));

    var msbuildCmd = "msbuild";

    foreach(var project in projects)
    {
        using (var process = StartAndReturnProcess(
            msbuildCmd,
            new ProcessSettings {
                Arguments = "/p:Configuration=" + configuration + " " + project
            }
        ))
        {
            process.WaitForExit();
            if (process.GetExitCode() != 0)
                throw new Exception("Build has failed!");
        }
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    var testFile = MakeAbsolute(new FilePath("./test/Sharpbrake.Client.Tests" + "/bin/" + configuration + "/Sharpbrake.Client.Tests.dll"));
    var testResultsFile = MakeAbsolute(testResultsDir.Path.CombineWithFilePath("Sharpbrake.Client").AppendExtension("xml"));;

    var workingDirectory = MakeAbsolute(new DirectoryPath("./test/Sharpbrake.Client.Tests")).FullPath;

    var xunitConsole = GetFiles("tools/xunit.runner.console/tools/xunit.console.exe")
        .OrderByDescending(file => file.FullPath)
        .FirstOrDefault();

    Action<ICakeContext> testAction = tool => {
        using (var process = tool.StartAndReturnProcess(
            xunitConsole,
            new ProcessSettings {
                Arguments = testFile + " -xml " + testResultsFile + " -nologo -noshadow",
                WorkingDirectory = workingDirectory
            }
        ))
        {
            process.WaitForExit();
            if (process.GetExitCode() != 0)
                throw new Exception("Tests have failed!");
        }
    };

    EnsureDirectoryExists(testResultsDir);

    var openCoverXml = MakeAbsolute(testResultsDir.Path.CombineWithFilePath("OpenCover").AppendExtension("xml"));;
    var coverageReportDir = System.IO.Path.Combine(testResultsDir, "report");

    var settings = new OpenCoverSettings
    {
        Register = "user",
        ReturnTargetCodeOffset = 0,
        WorkingDirectory = workingDirectory,
        ArgumentCustomization =
            args =>
                args.Append(
                    "-skipautoprops -mergebyhash -mergeoutput -oldstyle -hideskipped:All")
    }
    .WithFilter("+[*]* -[xunit.*]* -[*.Tests]*")
    .ExcludeByAttribute("*.ExcludeFromCodeCoverage*")
    .ExcludeByFile("*/*Designer.cs;*/*.g.cs;*/*.g.i.cs");

    OpenCover(testAction, openCoverXml, settings);

    // for non-local build coverage is uploaded to codecov.io so no need to generate the report
    if (FileExists(openCoverXml) && isLocalBuild)
    {
        ReportGenerator(openCoverXml, coverageReportDir,
            new ReportGeneratorSettings {
                ArgumentCustomization = args => args.Append("-reporttypes:html")
            }
        );
    }
});

Task("Publish-Coverage")
    .IsDependentOn("Run-Unit-Tests")
    .WithCriteria(() => !isLocalBuild && !isPullRequest)
    .Does(() =>
{
    var openCoverXml = MakeAbsolute(testResultsDir.Path.CombineWithFilePath("OpenCover").AppendExtension("xml"));;
    if (!FileExists(openCoverXml))
        throw new Exception("Missing \"" + openCoverXml + "\" file");

    UploadCoverageReport(Context, openCoverXml.FullPath);
})
.OnError(exception =>
{
    Information("Error: " + exception.Message);
});

Task("Create-Packages")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
{
    var projects = GetFiles("./src/**/*.csproj");
    var nugetCmd = MakeAbsolute(new FilePath("tools/NuGet.CommandLine/tools/NuGet.exe"));

    foreach (var project in projects)
    {
        var args = new StringBuilder();
        args.Append("pack ").Append(MakeAbsolute(project));
        args.Append(" -Symbols -Properties Configuration=").Append(configuration);
        args.Append(" -OutputDirectory ").Append(nugetDir);

        // nuget.exe pack Sharpbrake.Client.csproj -Symbols -Properties Configuration=configuration -OutputDirectory nugetDir
        using (var process = StartAndReturnProcess(
            nugetCmd,
            new ProcessSettings { Arguments = args.ToString() }
        ))
        {
            process.WaitForExit();
            if (process.GetExitCode() != 0)
                throw new Exception("Creating package for " + project.GetFilename() + " has failed!");
        }
    }
});

Task("Publish-MyGet")
    .IsDependentOn("Create-Packages")
    .WithCriteria(() => !isLocalBuild && !isPullRequest && !isBuildTagged)
    .Does(() =>
{
    var serverUrl = EnvironmentVariable("MYGET_SERVER_URL");
    if (string.IsNullOrEmpty(serverUrl))
        throw new InvalidOperationException("Could not resolve MyGet server URL");

    var symbolServerUrl = EnvironmentVariable("MYGET_SYMBOL_SERVER_URL");

    var apiKey = EnvironmentVariable("MYGET_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
        throw new InvalidOperationException("Could not resolve MyGet API key");

    foreach (var package in GetFiles(nugetDir + "/*.nupkg"))
    {
        // symbols packages are pushed alongside regular ones so no need to push them explicitly
        if (package.FullPath.EndsWith("symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            continue;

        UploadPackage(Context, serverUrl, symbolServerUrl, apiKey, package);
    }
})
.OnError(exception =>
{
    Information("Error: " + exception.Message);
});

Task("Publish-NuGet")
    .IsDependentOn("Create-Packages")
    .WithCriteria(() => !isLocalBuild && !isPullRequest && isBuildTagged)
    .Does(() =>
{
    var serverUrl = EnvironmentVariable("NUGET_SERVER_URL");
    if (string.IsNullOrEmpty(serverUrl))
        throw new InvalidOperationException("Could not resolve NuGet server URL");

    var symbolServerUrl = EnvironmentVariable("NUGET_SYMBOL_SERVER_URL");

    var apiKey = EnvironmentVariable("NUGET_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
        throw new InvalidOperationException("Could not resolve NuGet API key");

    foreach (var package in GetFiles(nugetDir + "/*.nupkg"))
    {
        // symbols packages are pushed alongside regular ones so no need to push them explicitly
        if (package.FullPath.EndsWith("symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            continue;

        UploadPackage(Context, serverUrl, symbolServerUrl, apiKey, package);
    }
})
.OnError(exception =>
{
    Information("Error: " + exception.Message);
});

//
// Targets
//

Task("Default")
    .IsDependentOn("Create-Packages")
    .IsDependentOn("Publish-Coverage")
    .IsDependentOn("Publish-MyGet")
    .IsDependentOn("Publish-NuGet");

//
// Run build
//

RunTarget(target);


// **********************************************
// ***               Utilities                ***
// **********************************************

/// <summary>
/// Checks if build is tagged.
/// </summary>
private static bool IsBuildTagged(BuildSystem buildSystem)
{
    return buildSystem.AppVeyor.Environment.Repository.Tag.IsTag
           && !string.IsNullOrWhiteSpace(buildSystem.AppVeyor.Environment.Repository.Tag.Name);
}

/// <summary>
/// Get's version from AssemblyVersion attribute.
/// </summary>
private static string GetVersion(string csproj)
{
    var csprojInfo = new FileInfo(csproj);

    var assemblyInfo =
        new FileInfo(
            System.IO.Path.Combine(
                System.IO.Path.Combine(csprojInfo.DirectoryName, "Properties"), "AssemblyInfo.cs"));

    if (!assemblyInfo.Exists)
        throw new Exception("Not Found AssemblyInfo.cs file.");

    var versionRegex =
        new System.Text.RegularExpressions.Regex(@"\[assembly\: AssemblyVersion\(""(\d{1,})\.(\d{1,})\.(\d{1,})\.(\d{1,})""\)\]",
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    using (var reader = new StreamReader(assemblyInfo.FullName))
    {
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrEmpty(line))
                continue;

            var match = versionRegex.Match(line);
            if (match.Success)
            {
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}.{1}.{2}",
                    match.Groups[1].Value,
                    match.Groups[2].Value,
                    match.Groups[3].Value);
            }
        }
    }

    return null;
}

/// <summary>
/// Uploads coverage report (OpenCover.xml) to codecov.io.
/// </summary>
public static void UploadCoverageReport(ICakeContext context, string openCoverXml)
{
    const string url = "https://codecov.io/upload/v2";

    // query parameters: https://github.com/codecov/codecov-bash/blob/master/codecov#L1202
    var queryBuilder = new System.Text.StringBuilder(url);
    queryBuilder.Append("?package=bash-tbd&service=appveyor");
    queryBuilder.Append("&branch=").Append(context.EnvironmentVariable("APPVEYOR_REPO_BRANCH"));
    queryBuilder.Append("&commit=").Append(context.EnvironmentVariable("APPVEYOR_REPO_COMMIT"));
    queryBuilder.Append("&build=").Append(context.EnvironmentVariable("APPVEYOR_JOB_ID"));
    queryBuilder.Append("&pr=").Append(context.EnvironmentVariable("APPVEYOR_PULL_REQUEST_NUMBER"));
    queryBuilder.Append("&job=").Append(context.EnvironmentVariable("APPVEYOR_ACCOUNT_NAME"));
    queryBuilder.Append("%2F").Append(context.EnvironmentVariable("APPVEYOR_PROJECT_SLUG"));
    queryBuilder.Append("%2F").Append(context.EnvironmentVariable("APPVEYOR_BUILD_VERSION"));
    queryBuilder.Append("&token=").Append(context.EnvironmentVariable("CODECOV_TOKEN"));

    var request = (System.Net.HttpWebRequest) System.Net.WebRequest.Create(queryBuilder.ToString());
    request.Accept = "text/plain";
    request.Method = "POST";

    using (var requestStream = request.GetRequestStream())
    using (var openCoverXmlStream = new System.IO.FileStream(openCoverXml, System.IO.FileMode.Open, System.IO.FileAccess.Read))
    {
        var buffer = new byte[1024];
        int readBytes;
        while ((readBytes = openCoverXmlStream.Read(buffer, 0, buffer.Length)) > 0)
            requestStream.Write(buffer, 0, readBytes);
    }

    using (var response = (System.Net.HttpWebResponse) request.GetResponse())
    {
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            using (var responseStream = response.GetResponseStream())
            {
                if (responseStream != null)
                {
                    using (var responseStreamReader = new System.IO.StreamReader(responseStream))
                        context.Information(responseStreamReader.ReadToEnd());
                }
            }
        }
        else
        {
            context.Information("Status code: " + response.StatusCode);
        }
    }
}

/// <summary>
/// Uploads package to the repo.
/// </summary>
private static void UploadPackage(ICakeContext context, string serverUrl, string symbolServerUrl, string apiKey, FilePath packagePath)
{
    var nugetCmd = context.MakeAbsolute(new FilePath("tools/NuGet.CommandLine/tools/NuGet.exe"));

    Func<string, bool> Push = (nugetArgs) =>
    {
        var attempt = 0;
        var pushed = false;
        while (!pushed && attempt++ < 3)
        {
            using (var process = context.StartAndReturnProcess(nugetCmd, new ProcessSettings { Arguments = nugetArgs }))
            {
                process.WaitForExit();
                var exitCode = process.GetExitCode();
                if (exitCode != 0)
                {
                    if (attempt < 3)
                    {
                        context.Information("Package was not pushed. Error code: " + exitCode);
                        context.Information("Attempt: " + (attempt + 1));
                    }
                    continue;
                }
                pushed = true;
            }
        }
        return pushed;
    };

    var packageName = packagePath.GetFilename();

    var pushArgs = new StringBuilder();
    pushArgs.Append("push ").Append(context.MakeAbsolute(packagePath));
    pushArgs.Append(" -Source ").Append(serverUrl);
    pushArgs.Append(" -ApiKey ").Append(apiKey);
    pushArgs.Append(" -NonInteractive -NoSymbols");

    var packagePushed = Push(pushArgs.ToString());
    if (packagePushed && !string.IsNullOrEmpty(symbolServerUrl))
    {
        var symbolPackagePath = new FilePath(packagePath.FullPath.Replace(".nupkg", ".symbols.nupkg"));
        if (context.FileExists(symbolPackagePath))
        {
            var pushSymbolArgs = new StringBuilder();
            pushSymbolArgs.Append("push ").Append(context.MakeAbsolute(symbolPackagePath));
            pushSymbolArgs.Append(" -Source ").Append(symbolServerUrl);
            pushSymbolArgs.Append(" -ApiKey ").Append(apiKey);
            pushSymbolArgs.Append(" -NonInteractive");

            var symbolsPushed = Push(pushSymbolArgs.ToString());
            if (!symbolsPushed)
                context.Information("Failed to push symbols for package " + packageName);
        }
    }
    else
    {
       context.Information("Package " + packageName + " was NOT pushed to the repo!"); 
    }
}
