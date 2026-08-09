// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using CP.BuildTools;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace RALE.Server.Build;

/// <summary>Defines the repository's restore, compile, test, and package pipeline.</summary>
public sealed class Build : NukeBuild
{
    /// <summary>The published NuGet package identifier.</summary>
    private const string PackageId = "CP.Reactive.Agentic.Loop.Engineer.MCP.Server";

    /// <summary>The MCP manifest's packages property name and package output directory name.</summary>
    private const string PackagesName = "packages";

    /// <summary>The version property name shared by the MCP manifest and NuSpec.</summary>
    private const string VersionName = "version";

    /// <summary>The MSBuild property that enables deterministic continuous-integration output.</summary>
    private const string ContinuousIntegrationBuildName = "ContinuousIntegrationBuild";

    /// <summary>The MSBuild property used to propagate the resolved MinVer version.</summary>
    private const string MinVerVersionOverrideName = "MinVerVersionOverride";

    /// <summary>Package entries that every produced server package must contain.</summary>
    private static readonly string[] RequiredPackageEntries =
    [
        ".mcp/server.json",
        "images/rale-image.ico",
        "images/rale-image.png",
        "images/rale-package-icon.png",
        "README.md",
        "skills/RALE/SKILL.md",
    ];

    /// <summary>Shared JSON formatting for the staged MCP manifest.</summary>
    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { WriteIndented = true, };

    /// <summary>The normalized MinVer version resolved for the current repository state.</summary>
    private string _minVerVersion = string.Empty;

    /// <summary>The SemVer package version resolved by MinVer.</summary>
    private string _packageVersion = string.Empty;

    /// <summary>Gets or sets the requested build configuration.</summary>
    [Parameter("Configuration to build. Defaults to Debug locally and Release on the build server.")]
    public Configuration Configuration { get; set; } =
        IsLocalBuild ? Configuration.Debug : Configuration.Release;

    /// <summary>Gets the target that clears generated package, test, and staging output.</summary>
    public Target Clean => static target => target
        .Executes(static () =>
        {
            _ = PackagesDirectory.CreateOrCleanDirectory();
            _ = TestResultsDirectory.CreateOrCleanDirectory();
            _ = PackageManifestFile.Parent.CreateOrCleanDirectory();
        });

    /// <summary>Gets the target that restores the complete solution.</summary>
    public Target Restore => target => target
        .DependsOn(Clean)
        .Executes(static () =>
        {
            _ = DotNetRestore(static settings => settings
                .SetProjectFile(SolutionFile)
                .SetProperty(ContinuousIntegrationBuildName, IsServerBuild));
        });

    /// <summary>Gets the target that resolves and propagates the authoritative MinVer version.</summary>
    public Target ResolveVersion => target => target
        .DependsOn(Restore)
        .Executes(() =>
        {
            var arguments = string.Join(
                ' ',
                "msbuild",
                $"\"{ServerProjectFile}\"",
                "-target:MinVer",
                "-property:Restore=false",
                "-getProperty:MinVerVersion,PackageVersion",
                "-nologo",
                "-verbosity:quiet");
            var process = ProcessTasks.StartProcess(DotNetPath, arguments, RootDirectory);
            _ = process.AssertZeroExitCode();

            var output = new StringBuilder();
            foreach (var line in process.Output)
            {
                _ = output.AppendLine(line.Text);
            }

            var outputText = output.ToString();
            var jsonStart = outputText.IndexOf('{', StringComparison.Ordinal);
            if (jsonStart < 0)
            {
                throw new InvalidOperationException("MinVer did not return its calculated MSBuild properties.");
            }

            using var result = JsonDocument.Parse(outputText[jsonStart..]);
            var properties = result.RootElement.GetProperty("Properties");
            _minVerVersion = properties.GetProperty("MinVerVersion").GetString() ?? string.Empty;
            _packageVersion = properties.GetProperty("PackageVersion").GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_minVerVersion) || string.IsNullOrWhiteSpace(_packageVersion))
            {
                throw new InvalidOperationException("MinVer returned an empty version.");
            }

            Environment.SetEnvironmentVariable(MinVerVersionOverrideName, _minVerVersion);
            Log.Information("MinVer version: {MinVerVersion}", _minVerVersion);
            Log.Information("Package version: {PackageVersion}", _packageVersion);
        });

    /// <summary>Gets the target that compiles every product and test project with the resolved version.</summary>
    public Target Compile => target => target
        .DependsOn(ResolveVersion)
        .Executes(() =>
        {
            foreach (var project in GetProductProjects())
            {
                _ = DotNetBuild(settings => settings
                    .SetProjectFile(project.Path)
                    .SetConfiguration(Configuration)
                    .SetNoRestore(true)
                    .SetProperty(ContinuousIntegrationBuildName, IsServerBuild)
                    .SetProperty(MinVerVersionOverrideName, _minVerVersion));
            }
        });

    /// <summary>Gets the target that runs all TUnit projects and emits Cobertura coverage.</summary>
    public Target Test => target => target
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach (var project in GetProductProjects().GetTestProjectInfos())
            {
                var coverageFile = TestResultsDirectory / $"{project.Name}.cobertura.xml";
                var arguments = string.Join(
                    ' ',
                    "test",
                    "--project",
                    $"\"{project.Path}\"",
                    "--configuration",
                    Configuration,
                    "--no-build",
                    "--no-restore",
                    "--results-directory",
                    $"\"{TestResultsDirectory}\"",
                    "--",
                    "--coverage",
                    "--coverage-output",
                    $"\"{coverageFile}\"",
                    "--coverage-output-format",
                    "cobertura");
                _ = ProcessTasks
                    .StartProcess(DotNetPath, arguments, RootDirectory)
                    .AssertZeroExitCode();
            }
        });

    /// <summary>Gets the target that tests, packs, and verifies the distributable NuGet package.</summary>
    public Target Pack => target => target
        .DependsOn(Test)
        .Executes(() =>
        {
            PreparePackageManifest();
            _ = DotNetPack(settings => settings
                .SetProject(ServerProjectFile)
                .SetConfiguration(Configuration)
                .SetNoBuild(true)
                .SetNoRestore(true)
                .SetOutputDirectory(PackagesDirectory)
                .SetProperty(ContinuousIntegrationBuildName, IsServerBuild)
                .SetProperty("McpManifestSource", PackageManifestFile)
                .SetProperty(MinVerVersionOverrideName, _minVerVersion));
            VerifyPackedPackage();
        });

    /// <summary>Gets the solution used for product builds.</summary>
    private static AbsolutePath SolutionFile => RootDirectory / "src" / "ReactiveAgenticLoopEngineer.slnx";

    /// <summary>Gets the NUKE bootstrap project, which is excluded from the running build graph.</summary>
    private static AbsolutePath NukeBuildProjectFile => RootDirectory / "build" / "_build.csproj";

    /// <summary>Gets the product project whose MinVer properties and package are evaluated.</summary>
    private static AbsolutePath ServerProjectFile => RootDirectory / "src" / "RALE.Server" / "RALE.Server.csproj";

    /// <summary>Gets the repository MCP manifest.</summary>
    private static AbsolutePath McpManifestFile => RootDirectory / ".mcp" / "server.json";

    /// <summary>Gets the repository README included in the package.</summary>
    private static AbsolutePath ReadmeFile => RootDirectory / "README.md";

    /// <summary>Gets the package output directory.</summary>
    private static AbsolutePath PackagesDirectory => RootDirectory / PackagesName;

    /// <summary>Gets the test result and coverage output directory.</summary>
    private static AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";

    /// <summary>Gets the generated manifest staged exclusively for packing.</summary>
    private static AbsolutePath PackageManifestFile =>
        RootDirectory / ".nuke" / "temp" / "package" / ".mcp" / "server.json";

    /// <summary>Runs the requested NUKE target.</summary>
    /// <returns>The process exit code.</returns>
    public static int Main() => Execute<Build>(static build => build.Compile);

    /// <summary>Reads product project metadata while excluding the executing build project.</summary>
    /// <returns>The product and test projects.</returns>
    private static List<DotNetProjectInfo> GetProductProjects()
    {
        var projects = new List<DotNetProjectInfo>();
        foreach (var project in SolutionFile.ReadSolutionProjectInfos())
        {
            if (project.Path != NukeBuildProjectFile)
            {
                projects.Add(project);
            }
        }

        return projects;
    }

    /// <summary>Reads a named text entry from a package.</summary>
    /// <param name="archive">The open package archive.</param>
    /// <param name="entryName">The entry name.</param>
    /// <returns>The entry content.</returns>
    private static string ReadPackageEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"The package does not contain {entryName}.");
        return ReadPackageEntry(entry);
    }

    /// <summary>Reads a text entry from a package.</summary>
    /// <param name="entry">The package entry.</param>
    /// <returns>The entry content.</returns>
    private static string ReadPackageEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Compares package versions without introducing a timing-sensitive equality operation.</summary>
    /// <param name="actualVersion">The discovered version.</param>
    /// <param name="expectedVersion">The expected version.</param>
    /// <returns><see langword="true"/> when both versions match.</returns>
    private static bool VersionsMatch(string? actualVersion, string expectedVersion)
    {
        if (actualVersion is null)
        {
            return false;
        }

        var actualBytes = Encoding.UTF8.GetBytes(actualVersion);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedVersion);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    /// <summary>Verifies the package contains every required repository asset.</summary>
    /// <param name="archive">The package archive.</param>
    private static void VerifyRequiredEntries(ZipArchive archive)
    {
        var archiveEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            _ = archiveEntries.Add(entry.FullName);
        }

        var missingEntries = new List<string>();
        foreach (var requiredEntry in RequiredPackageEntries)
        {
            if (!archiveEntries.Contains(requiredEntry))
            {
                missingEntries.Add(requiredEntry);
            }
        }

        if (missingEntries.Count != 0)
        {
            throw new InvalidOperationException(
                $"The package is missing required entries: {string.Join(", ", missingEntries)}");
        }
    }

    /// <summary>Verifies the packaged README is the repository README.</summary>
    /// <param name="archive">The package archive.</param>
    private static void VerifyPackagedReadme(ZipArchive archive)
    {
        var packagedReadme = ReadPackageEntry(archive, "README.md");
        var sourceReadme = File.ReadAllText(ReadmeFile);
        if (!string.Equals(packagedReadme, sourceReadme, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The packaged README does not match the repository README.");
        }
    }

    /// <summary>Creates a versioned MCP manifest in the temporary package staging directory.</summary>
    private void PreparePackageManifest()
    {
        var manifest = JsonNode.Parse(File.ReadAllText(McpManifestFile))?.AsObject()
            ?? throw new InvalidOperationException("The MCP server manifest is not a JSON object.");
        manifest[VersionName] = _packageVersion;

        var packages = manifest[PackagesName]?.AsArray()
            ?? throw new InvalidOperationException("The MCP server manifest does not contain a packages array.");
        if (packages.Count == 0 || packages[0] is not JsonObject package)
        {
            throw new InvalidOperationException("The MCP server manifest does not contain a package object.");
        }

        package[VersionName] = _packageVersion;
        var manifestJson = manifest
            .ToJsonString(ManifestJsonOptions)
            .ReplaceLineEndings("\n");
        File.WriteAllText(PackageManifestFile, $"{manifestJson}\n");
    }

    /// <summary>Verifies the package structure, README, MCP metadata, and NuSpec version.</summary>
    private void VerifyPackedPackage()
    {
        var packageFile = PackagesDirectory / $"{PackageId}.{_packageVersion}.nupkg";
        if (!File.Exists(packageFile))
        {
            throw new InvalidOperationException($"The expected package was not created: {packageFile}");
        }

        using var archive = ZipFile.OpenRead(packageFile);
        VerifyRequiredEntries(archive);
        VerifyPackagedReadme(archive);
        VerifyPackagedManifest(archive);
        VerifyPackagedNuspec(archive);
        Log.Information("Verified package content and version {PackageVersion}.", _packageVersion);
    }

    /// <summary>Verifies both package versions in the staged MCP manifest.</summary>
    /// <param name="archive">The package archive.</param>
    private void VerifyPackagedManifest(ZipArchive archive)
    {
        var manifest = JsonNode.Parse(ReadPackageEntry(archive, ".mcp/server.json"))?.AsObject()
            ?? throw new InvalidOperationException("The packaged MCP server manifest is not a JSON object.");
        VerifyExpectedVersion("packaged MCP manifest", manifest[VersionName]?.GetValue<string>());
        VerifyExpectedVersion(
            "packaged MCP package metadata",
            manifest[PackagesName]?[0]?[VersionName]?.GetValue<string>());
    }

    /// <summary>Verifies the package has one NuSpec with the resolved package version.</summary>
    /// <param name="archive">The package archive.</param>
    private void VerifyPackagedNuspec(ZipArchive archive)
    {
        ZipArchiveEntry? nuspecEntry = null;
        var nuspecCount = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            {
                nuspecEntry = entry;
                nuspecCount++;
            }
        }

        if (nuspecCount != 1 || nuspecEntry is null)
        {
            throw new InvalidOperationException(
                $"The package must contain exactly one nuspec file; found {nuspecCount}.");
        }

        var nuspec = XDocument.Parse(ReadPackageEntry(nuspecEntry));
        string? nuspecVersion = null;
        foreach (var element in nuspec.Descendants())
        {
            if (string.Equals(element.Name.LocalName, VersionName, StringComparison.Ordinal))
            {
                nuspecVersion = element.Value;
                break;
            }
        }

        VerifyExpectedVersion("NuGet package", nuspecVersion);
    }

    /// <summary>Verifies a discovered package version matches the resolved MinVer package version.</summary>
    /// <param name="source">The version source.</param>
    /// <param name="actualVersion">The discovered version.</param>
    private void VerifyExpectedVersion(string source, string? actualVersion)
    {
        if (!VersionsMatch(actualVersion, _packageVersion))
        {
            throw new InvalidOperationException(
                $"{source} version '{actualVersion}' does not match '{_packageVersion}'.");
        }
    }
}
