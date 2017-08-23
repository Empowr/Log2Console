#tool "nuget:?package=JetBrains.ReSharper.CommandLineTools"
#tool "nuget:?package=NUnit.ConsoleRunner"
#tool "nuget:?package=JetBrains.dotCover.CommandLineTools"
#addin "Cake.DocFx"
#tool "docfx.console"
#tool ReSharperReports
#addin Cake.ReSharperReports
#tool "nuget:?package=ReportUnit"
#tool "nuget:?package=ReportGenerator"


//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

//Define Directories
var scriptDirectory =  Directory(".");
var baseDirectory = scriptDirectory + Directory("../");
var sourceDirectory = baseDirectory + Directory("./Source/");
var documentationDirectory = Directory("./Docs/"); //Note, don't include base directory, working directory will be manually set in docFx
var solutionDirectory = sourceDirectory + Directory("./Solutions");
var artifactDirectory = baseDirectory + Directory("Release");
var nugetArtifactDirectory = artifactDirectory + Directory("Nuget");
var logsDirectory = artifactDirectory + Directory("Logs");

//Define Files
var solutionFile = solutionDirectory + File("JediToolkit.sln");
var solutionDotSettingsFile = solutionDirectory + File("JediToolkit.sln.DotSettings");
var logFile = logsDirectory + File("msbuild.log");
var nugetArtifactFiles = nugetArtifactDirectory.ToString() + "/**/*.nupkg";
var nugetSymbolArtifactFiles = nugetArtifactDirectory.ToString() + "/**/*symbols.nupkg";
var docFxConfigFile = File("docfx.json");
var releaseNotesFile = baseDirectory + documentationDirectory + File ("ReleaseNotes.md");

//NUnit Config
var unitTestFiles = new FilePath[]
    {
        sourceDirectory + File("./Testing/UnitTests/Jedi.Core.Tests/bin/" + configuration + "/Jedi.Core.Tests.exe")
    };
var dotCoverResultFile = logsDirectory + File("result.dcvr");
var dotCoverResultFile1 = logsDirectory + File("TestResult.xml");
var dotCoverReportFile = logsDirectory + File("result.html");
var resharperInspectionResultFile = logsDirectory + File("reshaper-inspection.xml");
var resharperInspectionReportFile = logsDirectory + File("reshaper-inspection.html");

//Nuget Config
var nuspecFiles = new FilePath[]
    {
        sourceDirectory + File("./Main/Jedi.Core/Jedi.Core.nuspec"), 
        sourceDirectory + File("./Main/Jedi.Configuration/Jedi.Configuration.nuspec"),
        sourceDirectory + File("./Main/Jedi.Configuration.Core/Jedi.Configuration.Core.nuspec"),
        sourceDirectory + File("./Main/Jedi.Configuration.Editor/Jedi.Configuration.Editor.nuspec"),
        sourceDirectory + File("./Main/Jedi.Configuration.Mongo/Jedi.Configuration.MongoDB.nuspec"),
        sourceDirectory + File("./Main/Jedi.SubSystem/Jedi.SubSystem.nuspec"),
        sourceDirectory + File("./Main/Jedi.Cake/Jedi.Cake.nuspec"),
        sourceDirectory + File("./Main/Jedi.WindowsServices/Jedi.WindowsServices.nuspec"),
    };
var localNuGetRepo = Directory("C:/NuGetRepo1");
var nugetServer = "http://192.168.15.132:81/nuget/Default";
var nugetPassword = "icm-dev" ;


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
CleanDirectory(logsDirectory);

//////////////////////////////////////////////////////////////////////
// BUILD TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Cleans the solution")
    .Does(() =>
{
   //CleanDirectory(buildDir);
    MSBuild(solutionFile, settings => settings		
        .WithTarget("Clean")
        .UseToolVersion(MSBuildToolVersion.VS2015)
        .SetPlatformTarget(PlatformTarget.MSIL) //AnyCPU
        .SetConfiguration(configuration)
        .SetMaxCpuCount(0) //Max CPU
        .AddFileLogger(buildLog)
        );
    
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
   //CleanDirectory(buildDir);
    MSBuild(solutionFile, settings => settings		
        .WithTarget("Build")
        .UseToolVersion(MSBuildToolVersion.VS2015)
        .SetPlatformTarget(PlatformTarget.MSIL) //AnyCPU
        .SetConfiguration(configuration)
        .SetMaxCpuCount(0) //Max CPU
        .AddFileLogger(buildLog)
        );
    
});

//////////////////////////////////////////////////////////////////////
// TESTING TASKS
//////////////////////////////////////////////////////////////////////
Task("RunUnitTests")
    .Description("Run NUnit Test Runner")  
    .IsDependentOn("Build")
    .Does(() =>
{
    NUnit3(unitTestFiles, new NUnit3Settings {
        Configuration = configuration,
        Full = true,
        StopOnError = false,
        TeamCity = true,
        Work = logsDirectory
        });

    ReportUnit(logsDirectory);
});

Task("RunUnitTestsCodeCover")
    .Description("Run NUnit Test Runner and DotCover")  
    .IsDependentOn("Build")
    .Does(() =>
{
    DotCoverCover(tool => 
    {
        tool.NUnit3(unitTestFiles, new NUnit3Settings {
        Configuration = configuration,
        Full = true,
        StopOnError = false,
        TeamCity = true,
        Work = logsDirectory
        });
    },
    dotCoverResultFile,
    new DotCoverCoverSettings()
        .WithFilter("+:module=*Jedi*;class=*;function=*;"));

    DotCoverReport(dotCoverResultFile,
        dotCoverReportFile,
        new DotCoverReportSettings {
        ReportType = DotCoverReportType.HTML
        });
});

Task("RunResharperInspection")
    .Description("Run Resharper Inspection")
    .IsDependentOn("Build")
    .Does(() =>
{
    var msBuildProperties = new Dictionary<string, string>();
    msBuildProperties.Add("configuration", configuration);
    msBuildProperties.Add("platform", "AnyCPU");
            
    InspectCode(solutionFile, new InspectCodeSettings {
        SolutionWideAnalysis = true,
        Profile = solutionDotSettingsFile,
        MsBuildProperties = msBuildProperties,
        OutputFile = resharperInspectionResultFile,
        ThrowExceptionOnFindingViolations = false
    });

    ReSharperReports(resharperInspectionResultFile, resharperInspectionReportFile);
});
//////////////////////////////////////////////////////////////////////
// DOCUMENTATION TASKS
//////////////////////////////////////////////////////////////////////
Task("BuildDocs")
    .Description("Build DocFX Documentation")
    .Does(() =>
{
    DocFx(docFxConfigFile, new DocFxSettings
    {
        OutputPath = documentationDirectory,
        WorkingDirectory = baseDirectory
    });
});

Task("DisplayReleaseNotes")  
    .Description("Display the Release Notes")  
    .Does(() =>
{	
    var releaseNote = ParseReleaseNotes(releaseNotesFile);
    var sb = new StringBuilder();
    sb.AppendLine(string.Format("Release Notes for Version: {0}", releaseNote.Version));
    foreach(var note in releaseNote.Notes)
    {
        sb.AppendLine(string.Format("\t{0}", note));
    }
    Information(sb.ToString());
});

//////////////////////////////////////////////////////////////////////
// NUGET TASKS
//////////////////////////////////////////////////////////////////////
Task("PackageNuGet")
    .IsDependentOn("Build")
    .Description("Create NuGet Packages")    
    .Does(() =>
{
    CleanDirectories(nugetArtifactDirectory);

    //Get the Release Notes
    var releaseNote = ParseReleaseNotes(releaseNotesFile);
    var sb = new StringBuilder();
    sb.AppendLine(string.Format("Release Notes for Version: {0}", releaseNote.Version));
    foreach(var note in releaseNote.Notes)
    {
        sb.AppendLine(string.Format("\t{0}", note));
    }
    var releaseNotes = sb.ToString();
    Information(sb.ToString());

    //Get the Version Number
    var gitVersion = GitVersion(new GitVersionSettings());
    var isPreRelease = !string.IsNullOrEmpty(gitVersion.PreReleaseTag);
    var version = isPreRelease 
            ? gitVersion.NuGetVersion
            : gitVersion.MajorMinorPatch + "." + gitVersion.CommitsSinceVersionSource;

    //Configure NuGet
    var nuGetPackSettings   = new NuGetPackSettings {                               
                                Version                 = version,
                                Copyright               = "Copyright © ICM 2016",
                                Description				= "Jedi Library",
                                ReleaseNotes            = new [] {releaseNotes},                                          
                                Symbols                 = true,
                                NoPackageAnalysis       = true,                                
                                OutputDirectory         = nugetArtifactDirectory,
                                Verbosity				= NuGetVerbosity.Detailed,
                                Properties				= new Dictionary<string,string>(){{"SemVersion", version}}
                            };
    Information("Building NuGet with Version: {0} (Is PreRelease: {1})",  version, isPreRelease);    
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
// TASK TARGETS
//////////////////////////////////////////////////////////////////////
Task("Test")
    .Description("Run all Tests")
    .IsDependentOn("Build")
    .IsDependentOn("RunUnitTests")
    .IsDependentOn("RunUnitTestsCodeCover")
    .IsDependentOn("RunResharperInspection")
    .Does(() =>
{
  Information("Going to build solution: " + solutionFile);
});

Task("Default")
    .Description("Default Target, will run: Clean, Build, and PackageNuGet")
    .IsDependentOn("Clean")
    .IsDependentOn("Build")
    .IsDependentOn("PackageNuGet")
    .Does(() =>
{
  Information("Going to build solution: " + solutionFile);
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);