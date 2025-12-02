using Nuke.Common.Utilities;

// ReSharper disable AllUnderscoreLocalParameterName

namespace Build;

#pragma warning disable CA1822 // Mark members as static
public sealed class Build : NukeBuild
{
    [Solution( GenerateProjects = true )] readonly Solution Solution = default!;
    AbsolutePath ResultDirectory => RootDirectory / "result";
    AbsolutePath ResultNuGetDirectory => ResultDirectory / "nuget";
    AbsolutePath ReSharperSettingsFile => RootDirectory / "data/r#Settings.DotSettings";
    AbsolutePath TestDirectory => RootDirectory / "test";

    [Parameter]
    Boolean BuildServerOverride { get; }

    Configuration Configuration { get; } = Configuration.Release;

    [Parameter]
    Boolean MasterBranchOverride { get; }

    [GitRepository]
    GitRepository Repository { get; } = default!;

    String Version { get; set; } = "3.0.0";

    [Secret]
    String? NuGetApiKey => Environment.GetEnvironmentVariable( "NUGET_API_KEY" );

    [Secret]
    String? GitHubAccessToken => Environment.GetEnvironmentVariable( "ACCESS_TOKEN_GITHUB" );

    Int32 RequiredCoveragePercentage => 95;

    Target CleanBeforeBuild => _ => _
        .Executes( () =>
        {
            ResultDirectory.CreateOrCleanDirectory();
        } );

    Target RestoreDotNetTools => _ => _
        .DependsOn( CleanBeforeBuild )
        .Executes( () =>
        {
            DotNetToolRestore( new DotNetToolRestoreSettings() );
        } );

    Target SetVersion => _ => _
        .OnlyWhenDynamic( () => IsServerBuild || BuildServerOverride )
        .DependsOn( RestoreDotNetTools )
        .Executes( () =>
        {
            var version = "3.0.0";

            // Read version
            var versionFile = RootDirectory / "version.json";

            String? lsVersion = null;
            versionFile.UpdateJson( versionJson =>
            {
                lsVersion = versionJson[ "version" ]
                    ?.ToString();
            } );

            if ( lsVersion is not null )
            {
                version = lsVersion;
                Log.Information( "Version from version: {0}", version );
            }

            var currentVersion = System.Version.Parse( version );
            version = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}.0";
            var assemblyVersion = version;
            var fileVersion = version;

            var branchName = Repository.Branch!;
            var isMaster = branchName.Equals( "master", StringComparison.OrdinalIgnoreCase );
            if ( !isMaster )
                version = $"{version}-preview";

            var informationalVersion = $"{version}.{Repository.Commit}";
            Version = version;

            Log.Information( "Version: {0} FileVersion: {1} InformationalVersion: {2}", version, fileVersion, informationalVersion );
            foreach ( var project in Solution.AllProjects )
            {
                var projectModel = ProjectModelTasks.ParseProject( project )!;
                projectModel.SetProperty( "Version", version );
                projectModel.SetProperty( "AssemblyVersion", assemblyVersion );
                projectModel.SetProperty( "FileVersion", fileVersion );
                projectModel.SetProperty( "InformationalVersion", informationalVersion );
                projectModel.Save();
                Log.Information( "SAVE...." );
            }
        } );

    Target Compile => _ => _
        .DependsOn( SetVersion )
        .Executes( () =>
        {
            Log.Information( "Running build: {Configuration}", Configuration );
            DotNetBuild( x => x.SetProjectFile( Solution.Path )
                .SetConfiguration( Configuration ) );
        } );

    Target Test => _ => _
        .DependsOn( Compile )
        .OnlyWhenDynamic( () => Repository.IsOnMainOrMasterBranch() || MasterBranchOverride )
        .Executes( () =>
        {
            DotNetTest( x => x.SetProjectFile( Solution )
                .SetConfiguration( Configuration )
                .EnableNoRestore()
                .EnableNoBuild() );
        } );

    Target TestWithCoverage => _ => _
        .DependsOn( Compile )
        .OnlyWhenDynamic( () => !Repository.IsOnMainOrMasterBranch() && !MasterBranchOverride )
        .Executes( () =>
        {
            var coverageFilters = new HashSet<String>
            {
                "+:ConfigurationPlaceholders",
                "+:ConfigurationPlaceholders.*",
                "-:ConfigurationPlaceholders.Test",
                "-:NukeBuild"
            };

            var attributeFilters = new HashSet<String>
            {
                "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute",
                "System.CodeDom.Compiler.GeneratedCodeAttribute"
            };

            var coverageFiltersString = String.Join( ';', coverageFilters );
            var attributeFiltersString = String.Join( ';', attributeFilters );

            var coverOutputs = new HashSet<String>();
            foreach ( var testsProject in GetTestsProjects() )
            {
                Log.Information( "Run coverage for text project {Name}", testsProject.Name );

                var dotCoverOutputFileName = ResultDirectory / $"{testsProject.Name}.dotCover.dcvr";
                var dotCoverOutputFileNameString = dotCoverOutputFileName.ToString()!.Replace( "\\", "/" );
                coverOutputs.Add( dotCoverOutputFileNameString );

                Log.Information( "Write result to {ReportFile}", dotCoverOutputFileNameString );

                var projectName = testsProject.Path!.ToString()!.Replace( "\\", "/" );
                DotNet(
                $"dotcover cover"
                + $" --snapshot-output \"{dotCoverOutputFileNameString}\""
                + $" --Filters \"{coverageFiltersString}\""
                + $" --AttributeFilters \"{attributeFiltersString}\""
                + $" -- test \"{projectName}\""
                + $" --no-build --no-restore --configuration {Configuration} --blame-hang-timeout 3m"
                );

                Log.Information( "Coverage successful for text project {Name}", testsProject.Name );
            }

            var dotCoverCombinedOutputFileName = ResultDirectory / $"{Solution.Name}.dotCover.dcvr";
            var dotCoverCombinedOutputFileNameString = dotCoverCombinedOutputFileName.ToString()!.Replace( "\\", "/" );
            var mergeSource = String.Empty;
            foreach ( var coverOutput in coverOutputs )
                if ( String.IsNullOrWhiteSpace( mergeSource ) )
                    mergeSource += coverOutput;
                else
                    mergeSource += "," + mergeSource;

            Log.Information( "Run dotCover merge from sources: {MergeSource}...", mergeSource );

            DotNet(
            $"dotcover merge"
            + $" --snapshot-output \"{dotCoverCombinedOutputFileNameString}\""
            + $" --snapshot-source \"{mergeSource}\""
            );

            Log.Information( "DotCover run successfully!" );
        } );

    Target ScanForVulnerabilities => _ => _
        .OnlyWhenDynamic( () => !Repository.IsOnMainOrMasterBranch() && !MasterBranchOverride )
        .DependsOn( Compile )
        .Executes( () =>
        {
            using var process = StartProcess( "dotnet", "list package --vulnerable --include-transitive --source https://api.nuget.org/v3/index.json" );
            process.AssertZeroExitCode();

            var hasErrors = false;
            foreach ( var x in process.Output )
                hasErrors = x.Text.Contains( "has the following vulnerable packages", StringComparison.OrdinalIgnoreCase ) || hasErrors;

            // ReSharper disable once InvertIf
            if ( hasErrors )
            {
                foreach ( var x in process.Output )
                    Log.Error( "{Text}", x.Text );

                throw new( "Found vulnerable packages." );
            }
        } );

    Target Analyze => _ => _
        .OnlyWhenDynamic( () => !Repository.IsOnMainOrMasterBranch() && !MasterBranchOverride )
        .DependsOn( Test, TestWithCoverage, ScanForVulnerabilities )
        .Executes( () =>
        {
            var outputFileName = ResultDirectory / $"{Solution.Name}.InspectionResult.json";
            var outputFileNameString = outputFileName.ToString()!.Replace( "\\", "/" );

            var reSharperSettings = ReSharperSettingsFile.ToString()!.Replace( "\\", "/" );
            var solutionName = Solution.Path!.ToString()!.Replace( "\\", "/" );
            DotNet( $"""
                     jb inspectcode /output="{outputFileNameString}" /swea -f="SARIF" --properties:"Configuration={Configuration}" /profile="{reSharperSettings}" --no-build "{solutionName}" --exclude="*.editorconfig"
                     """ );
        } );

    Target PrepareNuGetPublish => _ => _
        .DependsOn( Analyze )
        .Produces( ResultNuGetDirectory / "*.nupkg" )
        .Executes( () =>
        {
            Log.Information( "Start packing '{0}'", Solution.src.ConfigurationPlaceholders.Name );
            DotNetPack( x => x.SetProject<DotNetPackSettings>( Solution.src.ConfigurationPlaceholders )
                .SetConfiguration( Configuration )
                .EnableNoBuild()
                .EnableNoRestore()
                .SetNoDependencies( true )
                .SetIncludeSource( true )
                .SetIncludeSymbols( true )
                .SetSymbolPackageFormat( DotNetSymbolPackageFormat.snupkg )
                .SetOutputDirectory( ResultNuGetDirectory ) );
        } );

    Target PublishNuGetPackage => _ => _
        .DependsOn( PrepareNuGetPublish )
        .OnlyWhenDynamic( () => (IsServerBuild || BuildServerOverride) && !GitHubActions.Instance.IsPullRequest )
        .Executes( () =>
        {
            Log.Information( "Publishing packages; Is pull request: {IsPullRequest}", GitHubActions.Instance.IsPullRequest );

            GlobFiles( (String)ResultNuGetDirectory, "*.nupkg" )
                .ForEach( x =>
                {
                    Log.Information( "Start publishing package '{0}'", x );

                    // Push to NuGet.org
                    DotNetNuGetPush( c => c
                        .SetTargetPath( x )
                        .SetApiKey( NuGetApiKey )
                        .SetSource( "https://api.nuget.org/v3/index.json" )
                        .EnableSkipDuplicate() );

                    Log.Information( "Successfully published package '{0}' to nuget.org", x );

                    // Push to GitHub setup from within the GH action script
                    DotNetNuGetPush( c => c
                        .SetTargetPath( x )
                        .SetApiKey( GitHubAccessToken )
                        .SetSource( "github" )
                        .EnableSkipDuplicate() );

                    Log.Information( "Successfully published package '{0}' to github", x );
                } );
        } );

    Target CreateAndPushGitTag => _ => _
        .OnlyWhenDynamic( () => (IsServerBuild || BuildServerOverride) && !GitHubActions.Instance.IsPullRequest )
        .DependsOn( PublishNuGetPackage )
        .Executes( () =>
        {
            var tagName = $"{Version}-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}-release".Replace( '/', '_' ).Replace( '\\', '_' );
            Git( $"tag {tagName}", logOutput: true );
            Git( "push --tags", logOutput: true );
        } );

    Target Default => _ => _
        .DependsOn( CreateAndPushGitTag )
        .Executes( () =>
        {
            Log.Information( "Build completed!" );
        } );

    public static Int32 Main() =>
        Execute<Build>( x => x.Default );

    /// <summary>
    ///     Gets all tests projects.
    /// </summary>
    /// <returns>Test projects.</returns>
    IReadOnlyCollection<Project> GetTestsProjects()
    {
        var testProjects = new List<Project>();
        foreach ( var project in Solution.AllProjects )
        {
            var isTest = false;
            var projectDirectory = project.Directory;
            while ( projectDirectory is not null )
            {
                isTest = projectDirectory == TestDirectory;
                if ( isTest )
                    break;

                projectDirectory = projectDirectory.Parent;
            }

            if ( isTest )
                testProjects.Add( project );
        }

        return testProjects;
    }
    // ReSharper disable once InconsistentNaming
}
#pragma warning restore CA1822 // Mark members as static