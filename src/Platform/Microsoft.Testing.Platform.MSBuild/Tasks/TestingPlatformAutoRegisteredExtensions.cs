// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS8618 // Properties below are set by MSBuild.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// Task to generate the self-registered extensions source file.
/// </summary>
public sealed class TestingPlatformSelfRegisteredExtensions : Build.Utilities.Task
{
    private const string DisplayNameMetadataName = "DisplayName";
    private const string TypeFullNameMetadataName = "TypeFullName";
    private const string WellKnownBuilderHookMicrosoftTestingExtensionsTrx = "2006B3F7-93D2-4D9C-9C69-F41A1F21C9C7";
    private const string CSharpLanguageSymbol = "C#";
    private const string FSharpLanguageSymbol = "F#";
    private const string VBLanguageSymbol = "VB";

    /// <summary>
    /// Initializes a new instance of the <see cref="TestingPlatformSelfRegisteredExtensions"/> class.
    /// </summary>
    public TestingPlatformSelfRegisteredExtensions()
        : this(new FileSystem())
    {
    }

    internal TestingPlatformSelfRegisteredExtensions(IFileSystem fileSystem)
    {
        if (Environment.GetEnvironmentVariable("TESTINGPLATFORM_MSBUILD_LAUNCH_ATTACH_DEBUGGER") == "1")
        {
            Debugger.Launch();
        }

        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Gets or sets the path to the source file.
    /// </summary>
    [Required]
    public ITaskItem SelfRegisteredExtensionsSourcePath { get; set; }

    /// <summary>
    /// Gets or sets the language of the source file.
    /// </summary>
    [Required]
    public ITaskItem Language { get; set; }

    /// <summary>
    /// Gets or sets the builder hooks.
    /// </summary>
    [Required]
    public ITaskItem[] SelfRegisteredExtensionsBuilderHook { get; set; }

    /// <summary>
    /// Gets or sets the root namespace.
    /// </summary>
    public string? RootNamespace { get; set; }

    /// <summary>
    /// Gets or sets the path to the generated file.
    /// </summary>
    [Output]
    public ITaskItem SelfRegisteredExtensionsGeneratedFilePath { get; set; }

    private readonly string _expectedItemSpec = """
Expected item spec:
<ItemGroup>
    <!-- Unique stable identifier for the builder hook. -->
    <TestingPlatformBuilderHook Include="8E680F4D-E423-415A-9566-855439363BC0" >
        <!-- Display name for the builder hook. -->
        <DisplayName>MyBuilderHook</DisplayName>
        <!-- Full type name for the builder hook. -->
        <TypeFullName>Contoso.BuilderHook</TypeFullName>
    </TestingPlatformBuilderHook>
</ItemGroup>
Expected method signature
static Contoso.BuilderHook.AddExtensions(Microsoft.Testing.Platform.Builder.TestApplicationBuilder builder, string[] args)
""";

    private readonly IFileSystem _fileSystem;

    /// <inheritdoc />
    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, $"SelfRegisteredExtensionsSourcePath: '{SelfRegisteredExtensionsSourcePath.ItemSpec}'");
        Log.LogMessage(MessageImportance.Normal, $"Language: '{Language.ItemSpec}'");

        if (SelfRegisteredExtensionsBuilderHook.Length > 0)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("TestingPlatformExtensionFullTypeNames:");

            // Distinct by ItemSpec and take the first one.
            foreach (ITaskItem item in SelfRegisteredExtensionsBuilderHook.GroupBy(x => x.ItemSpec).Select(x => x.First()))
            {
                if (RoslynString.IsNullOrEmpty(item.GetMetadata(DisplayNameMetadataName)))
                {
                    Log.LogError("Missing 'DisplayName' metadata for item 'TestingPlatformBuilderHook'\n{0}", _expectedItemSpec);
                }

                if (RoslynString.IsNullOrEmpty(item.GetMetadata(TypeFullNameMetadataName)))
                {
                    Log.LogError("Missing 'TypeFullName' metadata for item 'TestingPlatformBuilderHook'\n{0}", _expectedItemSpec);
                }

                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $" Hook UID: '{item.ItemSpec}' DisplayName: '{item.GetMetadata(DisplayNameMetadataName)}' TypeFullName: '{item.GetMetadata(TypeFullNameMetadataName)}'");
            }

            Log.LogMessage(MessageImportance.Normal, stringBuilder.ToString());
        }

        if (!Log.HasLoggedErrors)
        {
            ITaskItem[] taskItems = Reorder(SelfRegisteredExtensionsBuilderHook);

            if (!Language.ItemSpec.Equals(CSharpLanguageSymbol, StringComparison.OrdinalIgnoreCase) &&
                !Language.ItemSpec.Equals(VBLanguageSymbol, StringComparison.OrdinalIgnoreCase) &&
                !Language.ItemSpec.Equals(FSharpLanguageSymbol, StringComparison.OrdinalIgnoreCase))
            {
                SelfRegisteredExtensionsGeneratedFilePath = default!;
                Log.LogError($"Language '{Language.ItemSpec}' is not supported.");
            }
            else
            {
                GenerateCode(Language.ItemSpec, RootNamespace, taskItems, SelfRegisteredExtensionsSourcePath, _fileSystem, Log);
                SelfRegisteredExtensionsGeneratedFilePath = SelfRegisteredExtensionsSourcePath;
            }
        }

        return !Log.HasLoggedErrors;
    }

    private static ITaskItem[] Reorder(ITaskItem[] items)
    {
        List<ITaskItem> result = new(items.Length);
        int wellKnownBuilderHook_MicrosoftTestingPlatformExtensions_index = -1;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].ItemSpec == WellKnownBuilderHookMicrosoftTestingExtensionsTrx)
            {
                wellKnownBuilderHook_MicrosoftTestingPlatformExtensions_index = i;
                continue;
            }

            result.Add(items[i]);
        }

        if (wellKnownBuilderHook_MicrosoftTestingPlatformExtensions_index != -1)
        {
            result.Add(items[wellKnownBuilderHook_MicrosoftTestingPlatformExtensions_index]);
        }

        return [.. result];
    }

    private static void GenerateCode(string language, string? rootNamespace, ITaskItem[] taskItems, ITaskItem testingPlatformEntryPointSourcePath, IFileSystem fileSystem, TaskLoggingHelper taskLoggingHelper)
    {
        StringBuilder builder = new();

        for (int i = 0; i < taskItems.Length; i++)
        {
            if (i != 0)
            {
                // Indent
                builder.Append("        ");
            }

            string global = language switch
            {
                CSharpLanguageSymbol => "global::",
                VBLanguageSymbol => "Global.",
                FSharpLanguageSymbol => "global.",
                _ => throw ApplicationStateGuard.Unreachable(),
            };

            builder.Append(CultureInfo.InvariantCulture, $"{global}{taskItems[i].GetMetadata(TypeFullNameMetadataName)}.AddExtensions(builder, args){(language == CSharpLanguageSymbol ? ";" : string.Empty)}");
            if (i < taskItems.Length - 1)
            {
                builder.AppendLine();
            }
        }

        string entryPointSource = GetSourceCode(language, rootNamespace, builder.ToString());
        taskLoggingHelper.LogMessage(MessageImportance.Normal, $"SelfRegisteredExtensions source:\n'{entryPointSource}'");
        fileSystem.WriteAllText(testingPlatformEntryPointSourcePath.ItemSpec, entryPointSource);
    }

    private static string GetSourceCode(string language, string? rootNamespace, string extensionsFragments)
    {
        if (language != VBLanguageSymbol && !RoslynString.IsNullOrEmpty(rootNamespace))
        {
            rootNamespace = NamespaceHelpers.ToSafeNamespace(rootNamespace);
        }

        if (language == CSharpLanguageSymbol)
        {
            return RoslynString.IsNullOrEmpty(rootNamespace)
                ? $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Microsoft.Testing.Platform.MSBuild
// </auto-generated>
//------------------------------------------------------------------------------

[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class SelfRegisteredExtensions
{
    public static void AddSelfRegisteredExtensions(this global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder, string[] args)
    {
        {{extensionsFragments}}
    }
}
"""
                : $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Microsoft.Testing.Platform.MSBuild
// </auto-generated>
//------------------------------------------------------------------------------

namespace {{rootNamespace}}
{
    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static class SelfRegisteredExtensions
    {
        public static void AddSelfRegisteredExtensions(this global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder, string[] args)
        {
            {{extensionsFragments}}
        }
    }
}
""";
        }
        else if (language == VBLanguageSymbol)
        {
            // NOTE: We don't use the value of RootNamespace here.
            // The compiler *already* wraps types in RootNamespace for Visual Basic
            // This is not the case for C# or F#.
            return $$"""
'------------------------------------------------------------------------------
' <auto-generated>
'     This code was generated by Microsoft.Testing.Platform.MSBuild
' </auto-generated>
'------------------------------------------------------------------------------

<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>
Friend Module SelfRegisteredExtensions
    <System.Runtime.CompilerServices.Extension>
    Public Sub AddSelfRegisteredExtensions(ByVal builder As Global.Microsoft.Testing.Platform.Builder.ITestApplicationBuilder, ByVal args As Global.System.String())
        {{extensionsFragments}}
    End Sub
End Module
""";
        }
        else if (language == FSharpLanguageSymbol)
        {
            return RoslynString.IsNullOrEmpty(rootNamespace)
                ? $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Microsoft.Testing.Platform.MSBuild
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.TestingPlatform.Extensions

open System.Runtime.CompilerServices

[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
[<Extension>]
type SelfRegisteredExtensions() =

    [<Extension>]
    static member AddSelfRegisteredExtensions (builder: Microsoft.Testing.Platform.Builder.ITestApplicationBuilder, args: string[]) =
        {{(extensionsFragments.Length > 0 ? extensionsFragments : "()")}}
"""
                : $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Microsoft.Testing.Platform.MSBuild
// </auto-generated>
//------------------------------------------------------------------------------

namespace {{rootNamespace}}

open System.Runtime.CompilerServices

[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
[<Extension>]
type SelfRegisteredExtensions() =

    [<Extension>]
    static member AddSelfRegisteredExtensions (builder: Microsoft.Testing.Platform.Builder.ITestApplicationBuilder, args: string[]) =
        {{(extensionsFragments.Length > 0 ? extensionsFragments : "()")}}
""";
        }

        throw new InvalidOperationException($"Language not supported '{language}'");
    }
}
