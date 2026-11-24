// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// This task generates the entry point for the Testing Platform.
/// </summary>
public sealed partial class TestingPlatformEntryPointTask : Build.Utilities.Task
{
    private const string CSharpLanguageSymbol = "C#";
    private const string FSharpLanguageSymbol = "F#";
    private const string VBLanguageSymbol = "VB";

    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestingPlatformEntryPointTask"/> class.
    /// </summary>
    public TestingPlatformEntryPointTask()
        : this(new FileSystem())
    {
    }

    internal TestingPlatformEntryPointTask(IFileSystem fileSystem)
    {
        // Stryker disable once String: The opt-in debugger hook cannot be exercised safely by an automated unit test.
        if (Environment.GetEnvironmentVariable("TESTINGPLATFORM_MSBUILD_LAUNCH_ATTACH_DEBUGGER") == "1")
        {
            Debugger.Launch();
        }

        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Gets or sets the path to the Testing Platform entry point source file.
    /// </summary>
    [Required]
    public required ITaskItem TestingPlatformEntryPointSourcePath { get; set; }

    /// <summary>
    /// Gets or sets the language of the project.
    /// </summary>
    [Required]
    public required ITaskItem Language { get; set; }

    /// <summary>
    /// Gets or sets the root namespace of the project.
    /// </summary>
    public string? RootNamespace { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the generated source should include a process entry point.
    /// The reusable testing-platform application helper is always generated.
    /// </summary>
    public bool GenerateEntryPoint { get; set; } = true;

    /// <summary>
    /// Gets or sets the fully qualified parameterless static method that asynchronously creates the application-owned host.
    /// </summary>
    public string? HostFactory { get; set; }

    /// <summary>
    /// Gets or sets the path to the generated Testing Platform entry point file. It stays <see langword="null"/>
    /// when the project language is not supported, in which case the task produces no output item.
    /// </summary>
    [Output]
    public ITaskItem? TestingPlatformEntryPointGeneratedFilePath { get; set; }

    /// <inheritdoc />
    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, $"TestingPlatformEntryPointSourcePath: '{TestingPlatformEntryPointSourcePath.ItemSpec}'");
        Log.LogMessage(MessageImportance.Normal, $"Language: '{Language.ItemSpec}'");

        if (!Language.ItemSpec.Equals(CSharpLanguageSymbol, StringComparison.OrdinalIgnoreCase) &&
            !Language.ItemSpec.Equals(VBLanguageSymbol, StringComparison.OrdinalIgnoreCase) &&
            !Language.ItemSpec.Equals(FSharpLanguageSymbol, StringComparison.OrdinalIgnoreCase))
        {
            TestingPlatformEntryPointGeneratedFilePath = null;
            Log.LogError($"Language '{Language.ItemSpec}' is not supported.");
        }
        else
        {
            string? hostFactory = RoslynString.IsNullOrEmpty(HostFactory) ? null : HostFactory.Trim();
            if (hostFactory is not null && !IsValidHostFactory(hostFactory))
            {
                Log.LogError(
                    "TestingPlatformHostFactory '{0}' is invalid. Specify a fully qualified static method path such as 'Contoso.Tests.TestHost.CreateHost'.",
                    hostFactory);
            }
            else
            {
                GenerateSource(Language.ItemSpec, RootNamespace, GenerateEntryPoint, hostFactory, TestingPlatformEntryPointSourcePath, _fileSystem, Log);
                TestingPlatformEntryPointGeneratedFilePath = TestingPlatformEntryPointSourcePath;
            }
        }

        return !Log.HasLoggedErrors;
    }

    private static bool IsValidHostFactory(string hostFactory)
    {
        string[] parts = hostFactory.Split('.');
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (string part in parts)
        {
            if (part.Length == 0 || (!char.IsLetter(part[0]) && part[0] != '_'))
            {
                return false;
            }

            for (int i = 1; i < part.Length; i++)
            {
                if (!char.IsLetterOrDigit(part[i]) && part[i] != '_')
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void GenerateSource(string language, string? rootNamespace, bool generateEntryPoint, string? hostFactory, ITaskItem testingPlatformEntryPointSourcePath, IFileSystem fileSystem, TaskLoggingHelper taskLoggingHelper)
    {
        string entryPointSource = GetEntryPointSourceCode(language, rootNamespace, generateEntryPoint, hostFactory);
        taskLoggingHelper.LogMessage(MessageImportance.Normal, $"Entrypoint source:\n'{entryPointSource}'");
        fileSystem.WriteAllText(testingPlatformEntryPointSourcePath.ItemSpec, entryPointSource);
    }

    private static string GetEntryPointSourceCode(string language, string? rootNamespace, bool generateEntryPoint, string? hostFactory)
    {
        if (language != VBLanguageSymbol && !RoslynString.IsNullOrEmpty(rootNamespace))
        {
            rootNamespace = NamespaceHelpers.ToSafeNamespace(rootNamespace);
        }

        return language switch
        {
            CSharpLanguageSymbol => hostFactory is null
                ? GetCSharpEntryPointSourceCode(rootNamespace, generateEntryPoint)
                : GetHostedCSharpEntryPointSourceCode(rootNamespace, generateEntryPoint, hostFactory),
            VBLanguageSymbol => hostFactory is null
                ? GetVisualBasicEntryPointSourceCode(generateEntryPoint)
                : GetHostedVisualBasicEntryPointSourceCode(generateEntryPoint, hostFactory),
            FSharpLanguageSymbol => hostFactory is null
                ? GetFSharpEntryPointSourceCode(rootNamespace, generateEntryPoint)
                : GetHostedFSharpEntryPointSourceCode(rootNamespace, generateEntryPoint, hostFactory),
            _ => throw new InvalidOperationException($"Language not supported '{language}'"),
        };
    }
}
