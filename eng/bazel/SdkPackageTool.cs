// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length != 1)
{
    throw new ArgumentException("Expected one SDK package input manifest.");
}

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true,
};
PackageInputs input = JsonSerializer.Deserialize<PackageInputs>(File.ReadAllText(args[0]), options)
    ?? throw new InvalidDataException("Missing SDK package input manifest.");
XElement project = ReadProject(input.Project);
XElement versions = ReadProject(input.Versions);
XElement repositoryProperties = ReadProject(input.RepositoryProperties);
XElement sourceProperties = ReadProject(input.SourceProperties);
XElement centralPackages = ReadProject(input.CentralPackages);
if (Property(project, "PackageId") != input.PackageId || Property(project, "IncludeBuildOutput") != "false")
{
    throw new InvalidDataException("SDK/meta-package identity or assembly-free contract changed.");
}

string[] frameworks = Property(repositoryProperties, "SupportedNetFrameworks").Split(';');
if (!frameworks.SequenceEqual(input.Frameworks, StringComparer.Ordinal))
{
    throw new InvalidDataException("The native portable framework list differs from SupportedNetFrameworks.");
}

var substitutions = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["MSTestVersion"] = ExperimentalVersion(versions, "VersionPrefix"),
    ["MSTestWindowsUIAutomationVersion"] = ExperimentalVersion(versions, "VersionPrefix"),
    ["MicrosoftTestingPlatformVersion"] = ExperimentalVersion(versions, "TestingPlatformVersionPrefix"),
    ["MicrosoftTestingExtensionsJUnitReportVersion"] = ExperimentalVersion(repositoryProperties, "MicrosoftTestingExtensionsJUnitReportVersionPrefix"),
    ["MicrosoftTestingExtensionsCodeCoverageVersion"] = Property(versions, "MicrosoftTestingExtensionsCodeCoverageVersion"),
    ["MicrosoftNETTestSdkVersion"] = Property(centralPackages, "MicrosoftNETTestSdkVersion"),
    ["MicrosoftTestingExtensionsFakesVersion"] = Property(centralPackages, "MicrosoftTestingExtensionsFakesVersion"),
    ["MicrosoftPlaywrightVersion"] = Property(centralPackages, "MicrosoftPlaywrightVersion"),
    ["AspireHostingTestingVersion"] = Property(centralPackages, "AspireHostingTestingVersion"),
};
foreach ((string name, string value) in substitutions)
{
    if (!Regex.IsMatch(value, @"^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant))
    {
        throw new InvalidDataException($"Expected a literal component version for {name}: {value}");
    }
}

CheckFeatureVersion(centralPackages, "Aspire.Hosting.Testing", substitutions["AspireHostingTestingVersion"]);
CheckFeatureVersion(centralPackages, "Microsoft.Playwright.MSTest.v4", substitutions["MicrosoftPlaywrightVersion"]);
var generated = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (Template template in input.Templates)
{
    string text = Regex.Replace(File.ReadAllText(template.Source), @"\$\{([^{}]+)\}", match =>
        substitutions.TryGetValue(match.Groups[1].Value, out string? value)
            ? value
            : throw new InvalidDataException($"Unknown SDK template property: {match.Groups[1].Value}"));
    if (text.Contains("${", StringComparison.Ordinal))
    {
        throw new InvalidDataException($"Unresolved SDK template token: {template.Source}");
    }

    using var reader = XmlReader.Create(new StringReader(text), XmlSettings());
    _ = XDocument.Load(reader);
    generated.Add(template.Output, text);
}

string[] expectedFiles;
Dependency[] dependencies;
switch (input.PackageId)
{
    case "MSTest.Sdk":
        if (Property(project, "SuppressDependenciesWhenPacking") != "true" || input.Templates.Length != 2)
        {
            throw new InvalidDataException("MSTest.Sdk must generate its two templates and suppress NuGet dependencies.");
        }

        expectedFiles = project.Descendants("None")
            .Where(item => (string?)item.Attribute("Pack") == "true")
            .Select(item => ((string?)item.Attribute("PackagePath")
                ?? throw new InvalidDataException("An SDK asset has no PackagePath.")).Replace('\\', '/'))
            .Append("PACKAGE.md").ToArray();
        dependencies = [];
        break;
    case "MSTest":
        if (input.Templates.Length != 0)
        {
            throw new InvalidDataException("The MSTest meta-package has no generated templates.");
        }

        expectedFiles = frameworks.Select(framework => $"lib/{framework}/_._").Append("PACKAGE.md").ToArray();
        dependencies = MetaPackageDependencies(project, substitutions);
        break;
    default:
        throw new InvalidDataException($"Unknown SDK/meta-package: {input.PackageId}");
}

if (!expectedFiles.Order(StringComparer.Ordinal).SequenceEqual(input.Files.Select(file => file.Target).Order(StringComparer.Ordinal), StringComparer.Ordinal))
{
    throw new InvalidDataException("Native package files do not match the SDK/meta-package project contract.");
}

var metadataProperties = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["VersionPrefix"] = Property(versions, "VersionPrefix"),
    ["CommonPackageTags"] = Property(sourceProperties, "CommonPackageTags"),
    ["CommonProductDescription"] = Property(sourceProperties, "CommonProductDescription"),
};
string ExpandMetadata(string value) => Regex.Replace(value, @"\$\(([^()]+)\)", match =>
    metadataProperties.TryGetValue(match.Groups[1].Value, out string? replacement)
        ? replacement
        : throw new InvalidDataException($"Unknown package metadata property: {match.Groups[1].Value}"));

var request = new
{
    input.Output,
    input.NuspecOutput,
    input.VersionFile,
    Metadata = new
    {
        Id = input.PackageId,
        Authors = new[] { "Microsoft" },
        Description = ExpandMetadata(Property(project, "PackageDescription")),
        Tags = ExpandMetadata(Property(project, "PackageTags")).Split(' ', StringSplitOptions.RemoveEmptyEntries),
        Copyright = "\u00a9 Microsoft Corporation. All rights reserved.",
        License = new { Type = "expression", Value = Property(repositoryProperties, "PackageLicenseExpression") },
        RequireLicenseAcceptance = true,
        Readme = "PACKAGE.md",
        ProjectUrl = "https://github.com/microsoft/testfx",
        Repository = new { Type = "git", Url = "https://github.com/microsoft/testfx" },
        ReleaseNotes = ExpandMetadata(Property(sourceProperties, "PackageReleaseNotes"))
            + "\nExperimental native Bazel build, not an official release. Portable .NET 8/9/10/11 package scope; Windows, .NET Framework, UWP and NativeAOT execution are not validated.",
    },
    PackageTypes = Array.Empty<string>(),
    DependencyGroups = input.PackageId == "MSTest"
        ? frameworks.Select(framework => new { TargetFramework = framework, Dependencies = dependencies }).ToArray()
        : [],
    input.Files,
};
string requestText = JsonSerializer.Serialize(request, options);
foreach ((string path, string text) in generated)
{
    Write(path, text);
}

Write(input.VersionFile, substitutions["MSTestVersion"]);
Write(input.RequestOutput, requestText);

static XmlReaderSettings XmlSettings() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

static XElement ReadProject(string path)
{
    using var reader = XmlReader.Create(path, XmlSettings());
    return XDocument.Load(reader).Element("Project")
        ?? throw new InvalidDataException($"Expected an MSBuild Project element: {path}");
}

static string Property(XElement project, string name)
{
    XElement[] properties = project.Elements("PropertyGroup").Elements(name).ToArray();
    if (properties.Length != 1 || properties[0].Attribute("Condition") is not null || properties[0].Parent!.Attribute("Condition") is not null)
    {
        throw new InvalidDataException($"Expected exactly one unconditional {name} property.");
    }

    string value = properties[0].Value.Trim();
    return value.Length == 0 ? throw new InvalidDataException($"Empty {name} property.") : value;
}

static string ExperimentalVersion(XElement project, string name)
{
    string value = Property(project, name);
    _ = Version.Parse(value);
    return value + "-bazel";
}

static void CheckFeatureVersion(XElement centralPackages, string package, string expected)
{
    XElement[] entries = centralPackages.Descendants("PackageVersion").Where(item => (string?)item.Attribute("Include") == package).ToArray();
    if (entries.Length != 1 || (string?)entries[0].Attribute("Version") != expected)
    {
        throw new InvalidDataException($"The SDK feature version differs from the central {package} package version.");
    }
}

static Dependency[] MetaPackageDependencies(XElement project, Dictionary<string, string> versions)
{
    var projectDependencies = new Dictionary<string, (string id, string version, string condition)>(StringComparer.Ordinal)
    {
        ["$(RepoRoot)src/Adapter/MSTest.TestAdapter/MSTest.TestAdapter.csproj"] = ("MSTest.TestAdapter", versions["MSTestVersion"], ""),
        ["$(RepoRoot)src/TestFramework/TestFramework.Extensions/TestFramework.Extensions.csproj"] = ("MSTest.TestFramework", versions["MSTestVersion"], ""),
        ["$(RepoRoot)src/Platform/Microsoft.Testing.Extensions.TrxReport/Microsoft.Testing.Extensions.TrxReport.csproj"] = ("Microsoft.Testing.Extensions.TrxReport", versions["MicrosoftTestingPlatformVersion"], "'$(TargetFramework)'!='$(UwpMinimum)'"),
    };
    var dependencies = new List<Dependency>();
    foreach (XElement reference in project.Descendants("ProjectReference"))
    {
        string path = ((string?)reference.Attribute("Include") ?? "").Replace('\\', '/');
        string condition = Regex.Replace((string?)reference.Attribute("Condition") ?? "", @"\s", "");
        if (!projectDependencies.Remove(path, out (string id, string version, string condition) dependency) || condition != dependency.condition)
        {
            throw new InvalidDataException($"Unsupported meta-package project reference or condition: {path}");
        }

        dependencies.Add(new(dependency.id, dependency.version, ["Build", "Analyzers"]));
    }

    if (projectDependencies.Count != 0)
    {
        throw new InvalidDataException("A required meta-package project reference is missing.");
    }

    XElement[] references = project.Descendants("PackageReference").ToArray();
    XElement testSdk = references.Single(item => (string?)item.Attribute("Include") == "Microsoft.NET.Test.Sdk");
    if (references.Length != 2
        || !references.Any(item => (string?)item.Attribute("Include") == "Microsoft.Testing.Extensions.CodeCoverage")
        || (string?)testSdk.Attribute("PrivateAssets") != "none"
        || (string?)testSdk.Attribute("VersionOverride") != "$(MicrosoftNETTestSdkVersion)")
    {
        throw new InvalidDataException("The meta-package external dependencies or test SDK build-asset flow changed.");
    }

    dependencies.Add(new("Microsoft.NET.Test.Sdk", versions["MicrosoftNETTestSdkVersion"], []));
    dependencies.Add(new("Microsoft.Testing.Extensions.CodeCoverage", versions["MicrosoftTestingExtensionsCodeCoverageVersion"], ["Build", "Analyzers"]));
    return dependencies.ToArray();
}

static void Write(string path, string text)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

internal sealed record PackageInputs(
    string PackageId,
    string Project,
    string Versions,
    string RepositoryProperties,
    string SourceProperties,
    string CentralPackages,
    string[] Frameworks,
    Template[] Templates,
    PackageFile[] Files,
    string Output,
    string NuspecOutput,
    string VersionFile,
    string RequestOutput);

internal sealed record Template(string Source, string Output);

internal sealed record PackageFile(string Source, string Target);

internal sealed record Dependency(string Id, string Version, string[] Exclude);
