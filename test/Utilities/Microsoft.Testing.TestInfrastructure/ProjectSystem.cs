// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.Testing.TestInfrastructure;

public class VSSolution : Folder
{
    private const string SlnProjectTemplate = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 16
VisualStudioVersion = 16.0.28701.123
MinimumVisualStudioVersion = 10.0.40219.1
{0}
";

    private const string SlnGlobalSectionTemplate = @"
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{0}
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {{C0047A98-3108-4928-8D43-FB6F8A49E3AB}}
	EndGlobalSection
EndGlobal
";

    private readonly StringBuilder _projects = new();
    private readonly StringBuilder _globals = new();
    private readonly string _solutionFileName;

    public VSSolution(string? solutionFolder, string? solutionName)
        : base(solutionFolder)
    {
        if (string.IsNullOrEmpty(solutionFolder))
        {
            throw new ArgumentNullException(nameof(solutionFolder));
        }

        if (string.IsNullOrEmpty(solutionName))
        {
            throw new ArgumentNullException(nameof(solutionName));
        }

        _solutionFileName = $"{solutionName}.sln";
        SolutionFile = Path.Combine(FolderPath, _solutionFileName);
        AddOrUpdateFileContent(_solutionFileName, MergeSolutionContent());
    }

    public ICollection<Project> Projects { get; private set; } = new List<Project>();

    public string SolutionFile { get; private set; }

    public CSharpProject CreateCSharpProject(string projectName, params string[] tfm)
    {
        var newProject = new CSharpProject(FolderPath, projectName, tfm);
        Projects.Add(newProject);

        var projectGuid = Guid.NewGuid();
        var configGuid = Guid.NewGuid();

        _projects.AppendFormat(CultureInfo.InvariantCulture, @"Project(""{{{0}}}"") = ""{1}"", ""{2}"", ""{{{3}}}""
EndProject{4}", projectGuid, projectName, newProject.ProjectFile, configGuid, Environment.NewLine);

        _globals.AppendFormat(CultureInfo.InvariantCulture, @"{{{0}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{{{0}}}.Debug|Any CPU.Build.0 = Debug|Any CPU{1}", configGuid, Environment.NewLine);

        _globals.AppendFormat(CultureInfo.InvariantCulture, @"{{{0}}}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{{{0}}}.Release|Any CPU.Build.0 = Release|Any CPU{1}", configGuid, Environment.NewLine);

        AddOrUpdateFileContent(_solutionFileName, MergeSolutionContent());
        return newProject;
    }

    private string MergeSolutionContent() => $"{string.Format(CultureInfo.InvariantCulture, SlnProjectTemplate, _projects.ToString())}{string.Format(CultureInfo.InvariantCulture, SlnGlobalSectionTemplate, _globals.ToString())}";
}

public class CSharpProject : Project
{
    private readonly string _projectFileName;
    private XElement _projectContent = new("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), new XElement("PropertyGroup"), new XElement("ItemGroup"));

    public CSharpProject(string solutionFolder, string projectName, params string[]? tfms)
       : base(Path.Combine(solutionFolder, projectName))
    {
        if (string.IsNullOrEmpty(solutionFolder))
        {
            throw new ArgumentNullException(nameof(solutionFolder));
        }

        if (string.IsNullOrEmpty(projectName))
        {
            throw new ArgumentNullException(nameof(projectName));
        }

        if (tfms is null || tfms.Length == 0)
        {
            throw new ArgumentException("Invalid tfm", nameof(tfms));
        }

        _projectFileName = $"{projectName}.csproj";
        ProjectFile = Path.Combine(FolderPath, _projectFileName);

        if (tfms.Length > 1)
        {
            _projectContent.Element("PropertyGroup")?.Add(new XElement("TargetFrameworks", tfms.Aggregate((a, b) => $"{a};{b}")));
        }
        else
        {
            _projectContent.Element("PropertyGroup")?.Add(new XElement("TargetFramework", tfms[0]));
        }

        AddOrUpdateFileContent(_projectFileName, _projectContent.ToString());
    }

    public string ProjectFile { get; private set; }

    public void AddPackageReference(string name, string version)
    {
        _projectContent.Element("ItemGroup")?.Add(new XElement("PackageReference", new XAttribute("Include", name), new XAttribute("version", version)));
        AddOrUpdateFileContent(_projectFileName, _projectContent.ToString());
    }

    public void AddProjectReference(string projectPath)
    {
        _projectContent = XElement.Load(ProjectFile);
        _projectContent.Element("ItemGroup")?.Add(new XElement("ProjectReference", new XAttribute("Include", projectPath)));
        AddOrUpdateFileContent(_projectFileName, _projectContent.ToString());
    }
}

public abstract class Project : Folder
{
    public Project(string projectFolder)
        : base(projectFolder)
    {
    }
}

public abstract class Folder
{
    public Folder(string? folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            throw new ArgumentException("Invalid folder name", nameof(folderPath));
        }

        FolderPath = Path.GetFullPath(folderPath);
    }

    public string FolderPath { get; private set; }

    public string AddOrUpdateFileContent(string relativePath, string fileContent)
    {
        string finalPath = Path.Combine(FolderPath, relativePath);
        string? finalPathDirectory = Path.GetDirectoryName(finalPath) ?? throw new InvalidOperationException("Unexpected null 'finalPathDirectory'");
        Directory.CreateDirectory(finalPathDirectory);
        File.WriteAllText(finalPath, fileContent);
        return finalPath;
    }
}
