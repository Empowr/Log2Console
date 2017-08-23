#tool "nuget:?package=GitVersion.CommandLine"
#addin "Cake.Powershell"
#addin "Cake.Compression"
#tool nuget:?package=vswhere
#tool nuget:?package=nuget.commandline

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////
var buildToolVersion = MSBuildToolVersion.VS2017; //MSBuildToolVersion.VS2015

//Define Directories
var scriptDirectory =  Directory(".");
var baseDirectory = scriptDirectory + Directory("../");
var sourceDirectory = baseDirectory + Directory("./src/");
var documentationDirectory = Directory("./Docs/"); //Note, don't include base directory, working directory will be manually set in docFx
var solutionDirectory = sourceDirectory;
var artifactDirectory = baseDirectory + Directory("output");
var nugetArtifactDirectory = artifactDirectory + Directory("Nuget");
var logsDirectory = artifactDirectory + Directory("Logs");

//Define Files
var solutionFile = solutionDirectory + File("LogFmwk.sln");
var logFile = logsDirectory + File("msbuild.log");
//var nugetArtifactFiles = nugetArtifactDirectory.ToString() + "/**/*.nupkg";
//var nugetSymbolArtifactFiles = nugetArtifactDirectory.ToString() + "/**/*symbols.nupkg";

var nugetArtifactFiles = nugetArtifactDirectory.ToString() + "/*.nupkg";
var nugetSymbolArtifactFiles = nugetArtifactDirectory.ToString() + "/*symbols.nupkg";

var docFxConfigFile = File("docfx.json");
var releaseNotesFile = baseDirectory + documentationDirectory + File ("ReleaseNotes.md");
var buildDirectory = sourceDirectory + Directory("Log2Console/bin") + Directory(configuration);

//Nuget Config
var nuspecFiles = new FilePath[]
    {
        //sourceDirectory + File("./Lib/SettingConfigDbLib/Kiosk.Application.Config.csproj"),  //nuspec           
    };
var localNuGetRepo = Directory("C:/NuGetRepo");
var nugetServer = "http://192.168.15.132:81/nuget/Default";
var microServiceNugetServer = "http://192.168.15.132:81/nuget/MicroServices";
var nugetPassword = "icm-dev" ;

//Chocolatey Config
var chocolateyFiles = MakeAbsolute(buildDirectory).ToString().Replace(@"/",@"\") + @"\**";
var chocolateyNuSpecFile = sourceDirectory + Directory("KioskConfigEditor") + File("MicroServiceConfigeEitor.nuspec"); 
var chocolateyArtifactDirectory = artifactDirectory + Directory("Chocolatey");
var chocolateyServer = "http://192.168.15.132:81/nuget/ICMChoco";
var chocolateyArtifactFiles = chocolateyArtifactDirectory.ToString() + "/*.nupkg";

//Binary Artifact Config
var binaryArtifactDirectory = artifactDirectory + Directory("Application");
var binaryArtifactName = "Log2Console";

//Define Configuration
var buildLog = new MSBuildFileLogger
                {
                    AppendToLogFile = true,
                    LogFile = logFile,
                    MSBuildFileLoggerOutput = MSBuildFileLoggerOutput.All,
                    PerformanceSummaryEnabled = true,
                    ShowCommandLine = true,
                    ShowEventId = true,
                    ShowTimestamp = true,
                    Verbosity = Verbosity.Normal
                };

//Prepare Environment
EnsureDirectoryExists(logsDirectory);
EnsureDirectoryExists(nugetArtifactDirectory);
EnsureDirectoryExists(chocolateyArtifactDirectory);
EnsureDirectoryExists(binaryArtifactDirectory);
CleanDirectory(logsDirectory);

//////////////////////////////////////////////////////////////////////
// BUILD TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Cleans the solution")
    .Does(() =>
{
    DirectoryPath vsLatest  = VSWhereLatest();
    FilePath msBuildPathX64 = (buildToolVersion != MSBuildToolVersion.VS2017 || vsLatest==null)
                            ? null
                            : vsLatest.CombineWithFilePath("./MSBuild/15.0/Bin/MSBuild.exe");
    Information("Path of msbuild: " + msBuildPathX64);

    MSBuild(solutionFile, settings => 
    {
        settings		
            .WithTarget("Clean")
            .UseToolVersion(buildToolVersion)
            .SetPlatformTarget(PlatformTarget.MSIL) 
            .SetConfiguration(configuration)
            .SetMaxCpuCount(0) //Max CPU
            .AddFileLogger(buildLog);
        if(buildToolVersion == MSBuildToolVersion.VS2017) settings.ToolPath = msBuildPathX64;
    });  
    
});

Task("RestoreNuGet")
    .Description("Restore all NuGet Packages")    
    .Does(() =>
{
    NuGetRestore(solutionFile);
});

Task("Build")
    .Description("Build the Solution")  
    .IsDependentOn("RestoreNuGet")
    .Does(() =>
{
    DirectoryPath vsLatest  = VSWhereLatest();
    FilePath msBuildPathX64 = (buildToolVersion != MSBuildToolVersion.VS2017 || vsLatest==null)
                            ? null
                            : vsLatest.CombineWithFilePath("./MSBuild/15.0/Bin/MSBuild.exe");
    Information("Path of msbuild: " + msBuildPathX64);

    MSBuild(solutionFile, settings => 
    {
        settings		
            .WithTarget("Build")
            .UseToolVersion(MSBuildToolVersion.VS2017)
            .SetPlatformTarget(PlatformTarget.MSIL) 
            .SetConfiguration(configuration)
            .SetMaxCpuCount(0) //Max CPU
            .AddFileLogger(buildLog);
        if(buildToolVersion == MSBuildToolVersion.VS2017) settings.ToolPath = msBuildPathX64;
    });  
});

Task("Package")
    .Description("Create Deplpyment Artifacts")
    .Does(()=>
{
    CleanDirectories(binaryArtifactDirectory);

     var version = GetVersion();
     var binaryArchiveFile = binaryArtifactName + "." + version + ".zip";
     var binaryArchivePath = binaryArtifactDirectory + File(binaryArchiveFile);
     Zip(buildDirectory, binaryArchivePath);
});



//////////////////////////////////////////////////////////////////////
// NUGET TASKS
//////////////////////////////////////////////////////////////////////
public string GetVersion()
{
    var gitVersion = GitVersion(new GitVersionSettings());
    var isPreRelease = !string.IsNullOrEmpty(gitVersion.PreReleaseTag);
    var version = isPreRelease 
            ? gitVersion.MajorMinorPatch + "." + gitVersion.CommitsSinceVersionSource + "-" + gitVersion.PreReleaseLabel
            : gitVersion.MajorMinorPatch + "." + gitVersion.CommitsSinceVersionSource;
    return version;
}

Task("PrintVersion")
    .Description("Prints the Version Info")    
    .Does(() =>
{
    Information(GetVersion());
});


Task("PackageNuGet")
    //.IsDependentOn("Build")
    .Description("Create NuGet Packages")    
    .Does(() =>
{
    CleanDirectories(nugetArtifactDirectory);

    //Get the Version Number
    var gitVersion = GitVersion(new GitVersionSettings());
    var isPreRelease = !string.IsNullOrEmpty(gitVersion.PreReleaseTag);
    var version = GetVersion();

    //Configure NuGet
    var nuGetPackSettings   = new NuGetPackSettings {                               
                                Version                 = version,
                                Copyright               = "Copyright � ICM 2017",
                                Description				= "Jedi Library",
                                ReleaseNotes            = new [] {"Release Notes"},                                          
                                Symbols                 = true,
                                NoPackageAnalysis       = true,                                
                                OutputDirectory         = nugetArtifactDirectory,
                                Verbosity				= NuGetVerbosity.Detailed,
                                Properties				= new Dictionary<string,string>()
                                                            {
                                                                {"SemVersion", version},
                                                                {"Configuration", configuration},
                                                                {"Platform", "AnyCpu"},
                                                                //{"$ReleaseNotesData$", releaseNotes}
                                                            }
                            };
    Information("Building NuGet with Version: {0} (Is PreRelease: {1}, Configuration: {2})",  version, isPreRelease, configuration);    
    NuGetPack(nuspecFiles, nuGetPackSettings);
});

Task("DeployNuGetLocal")  
    .Description("Copy NuGet Packages to Local Repo")      
    .Does(() =>
{
    EnsureDirectoryExists(localNuGetRepo);
    CopyFiles(nugetArtifactFiles, localNuGetRepo);
});

Task("DeployNuGet")  
    .Description("Deploy NuGet Packages to NuGet Server")  
    .Does(() =>
{	
    var packages = GetFiles(nugetSymbolArtifactFiles);
            
    // Push the package.
    NuGetPush(packages, new NuGetPushSettings {
        Source = nugetServer,
        ApiKey = nugetPassword
    });	
});

//////////////////////////////////////////////////////////////////////
// CHOCOLATEY TASKS
//////////////////////////////////////////////////////////////////////
Task("PackageChocolatey")
    //.IsDependentOn("Build")
    .Description("Create Cohocolatey Packages")    
    .Does(() =>
{
    CleanDirectories(chocolateyArtifactDirectory);

    //Get the Version Number
    var gitVersion = GitVersion(new GitVersionSettings());
    var isPreRelease = !string.IsNullOrEmpty(gitVersion.PreReleaseTag);
    var version = GetVersion();
    //var version = gitVersion.NuGetVersionV2;
    //Configure NuGet
    Information("Going to Pack: " + chocolateyFiles);
    var chocolateyPackSettings   = new ChocolateyPackSettings {
                                     Id                      = "Log2Console",
                                     Title                   = "NLog Monitoring Tool (Install)",
                                     Version                 = version,
                                     Authors                 = new[] {"Michal Steyn"},
                                     Owners                  = new[] {"ICM"},
                                     Summary                 = "Configure Service Fabric and Mongo Configuration for Micro Services running in Service Fabric",
                                     Description             = "Configure Service Fabric and Mongo Configuration for Micro Services running in Service Fabric",
                                     ProjectUrl              = new Uri("http://192.168.15.132:7990/projects/TOOL/repos/log2console/browse"),
                                     PackageSourceUrl        = new Uri("http://192.168.15.132:7990/projects/TOOL/repos/log2console/browse"),
                                     ProjectSourceUrl        = new Uri("http://192.168.15.132:7990/projects/TOOL/repos/log2console/browse"),
                                     DocsUrl                 = new Uri("http://192.168.15.132:7990/projects/TOOL/repos/log2console/browse"),
                                     Tags                    = new [] {"log2console", "NLog", "Tools"},
                                     Copyright               = "ICM 2017",
                                     RequireLicenseAcceptance= false,
                                     ReleaseNotes            = new [] {"Bug fixes", "Issue fixes", "Typos"},
                                     Files                   = new [] {
                                                                          new ChocolateyNuSpecContent {Source = chocolateyFiles, Target = "tools"},
                                                                       },
                                     Debug                   = false,
                                     Verbose                 = true,
                                     Force                   = false,
                                     Noop                    = false,
                                     LimitOutput             = false,
                                     ExecutionTimeout        = 13,
                                     //CacheLocation           = @"C:\temp\choco",
                                     AllowUnofficial         = false,
                                     OutputDirectory         = chocolateyArtifactDirectory
                                 };

    Information("Building NuGet with Version: {0} (Is PreRelease: {1}, Configuration: {2})",  version, isPreRelease, configuration);    
    ChocolateyPack(chocolateyPackSettings);
    // ChocolateyPack(chocolateyNuSpecFile, chocolateyPackSettings);
});

Task("DeployChocolatey")  
    .Description("Deploy NuGet Packages to NuGet Server")  
    .Does(() =>
{	
    var packages = GetFiles(chocolateyArtifactFiles);
            
    // Push the package.
    ChocolateyPush(packages, new ChocolateyPushSettings  {
        Source = chocolateyServer,
        ApiKey = nugetPassword,
        Force = true
    });	
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .Description("Default Target, will run: Clean, Build, and PackageNuGet")
    .IsDependentOn("RestoreNuGet")
    .IsDependentOn("Clean")
    .IsDependentOn("Build")
    //.IsDependentOn("PackageNuGet")
    .IsDependentOn("Package")    
    .Does(() =>
{
  Information("Going to build solution: " + solutionFile);
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);