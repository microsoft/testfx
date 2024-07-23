// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS8618 // Properties below are set by MSBuild.

using System.Globalization;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Testing.Platform.MSBuild;

public sealed class TestingPlatformEntryPointTask : Build.Utilities.Task
{
    private const string DisplayNameMetadataName = "DisplayName";
    private const string TypeFullNameMetadataName = "TypeFullName";
    private const string WellKnownBuilderHookMicrosoftTestingExtensionsTrx = "2006B3F7-93D2-4D9C-9C69-F41A1F21C9C7";
    private const string CSharpLanguageSymbol = "C#";
    private const string FSharpLanguageSymbol = "F#";
    private const string VBLanguageSymbol = "VB";

    public TestingPlatformEntryPointTask()
        : this(new FileSystem())
    {
    }

    internal TestingPlatformEntryPointTask(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    [Required]
    public ITaskItem TestingPlatformEntryPointSourcePath { get; set; }

    [Required]
    public ITaskItem Language { get; set; }

    [Required]
    public ITaskItem[] TestingPlatformBuilderHooks { get; set; }

    [Output]
    public ITaskItem TestingPlatformEntryPointGeneratedFilePath { get; set; }

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

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, $"TestingPlatformEntryPointSourcePath: '{TestingPlatformEntryPointSourcePath.ItemSpec}'");
        Log.LogMessage(MessageImportance.Normal, $"Language: '{Language.ItemSpec}'");

        if (TestingPlatformBuilderHooks.Length == 0)
        {
            Log.LogError("Zero TestingPlatformBuilderHook items found, add once or disable the testing platform entrypoint generation adding\n<GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>\n{0}", _expectedItemSpec);
            return false;
        }

        if (TestingPlatformBuilderHooks.Length > 0)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("TestingPlatformExtensionFullTypeNames:");

            // Distinct by ItemSpec and take the first one.
            foreach (ITaskItem item in TestingPlatformBuilderHooks.GroupBy(x => x.ItemSpec).Select(x => x.First()))
            {
                if (string.IsNullOrEmpty(item.GetMetadata(DisplayNameMetadataName)))
                {
                    Log.LogError("Missing 'DisplayName' metadata for item 'TestingPlatformBuilderHook'\n{0}", _expectedItemSpec);
                }

                if (string.IsNullOrEmpty(item.GetMetadata(TypeFullNameMetadataName)))
                {
                    Log.LogError("Missing 'TypeFullName' metadata for item 'TestingPlatformBuilderHook'\n{0}", _expectedItemSpec);
                }

                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $" Hook UID: '{item.ItemSpec}' DisplayName: '{item.GetMetadata(DisplayNameMetadataName)}' TypeFullName: '{item.GetMetadata(TypeFullNameMetadataName)}'");
            }

            Log.LogMessage(MessageImportance.Normal, stringBuilder.ToString());
        }

        if (!Log.HasLoggedErrors)
        {
            IReadOnlyList<ITaskItem> taskItems = Reorder(TestingPlatformBuilderHooks);

            if (!Language.ItemSpec.Equals(CSharpLanguageSymbol, StringComparison.OrdinalIgnoreCase) &&
                !Language.ItemSpec.Equals(VBLanguageSymbol, StringComparison.OrdinalIgnoreCase) &&
                !Language.ItemSpec.Equals(FSharpLanguageSymbol, StringComparison.OrdinalIgnoreCase))
            {
                TestingPlatformEntryPointGeneratedFilePath = default!;
                Log.LogError($"Language '{Language.ItemSpec}' is not supported.");
            }
            else
            {
                GenerateEntryPoint(Language.ItemSpec, taskItems, TestingPlatformEntryPointSourcePath, _fileSystem, Log);
                TestingPlatformEntryPointGeneratedFilePath = TestingPlatformEntryPointSourcePath;
            }
        }

        return !Log.HasLoggedErrors;
    }

    private static IReadOnlyList<ITaskItem> Reorder(ITaskItem[] items)
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

        return result;
    }

    private static void GenerateEntryPoint(string language, IReadOnlyList<ITaskItem> taskItems, ITaskItem testingPlatformEntryPointSourcePath, IFileSystem fileSystem, TaskLoggingHelper taskLoggingHelper)
    {
        StringBuilder builder = new();

        for (int i = 0; i < taskItems.Count; i++)
        {
            if (i != 0)
            {
                // Indent
                builder.Append("        ");
            }

            builder.Append(CultureInfo.InvariantCulture, $"{taskItems[i].GetMetadata(TypeFullNameMetadataName)}.AddExtensions(builder, args){(language == CSharpLanguageSymbol ? ";" : string.Empty)}");
            if (i < taskItems.Count - 1)
            {
                builder.AppendLine();
            }
        }

        string entryPointSource = GetEntryPointSourceCode(language, builder.ToString());
        taskLoggingHelper.LogMessage(MessageImportance.Normal, $"Entrypoint source:\n'{entryPointSource}'");
        fileSystem.WriteAllText(testingPlatformEntryPointSourcePath.ItemSpec, entryPointSource);
    }

    private static string GetEntryPointSourceCode(string language, string extensionsFragments)
    {
        if (language == CSharpLanguageSymbol)
        {
            return $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Microsoft.Testing.Platform.MSBuild
// </auto-generated>
//------------------------------------------------------------------------------

[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class TestingPlatformEntryPoint
{
    public static async global::System.Threading.Tasks.Task<int> Main(string[] args)
    {
        global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder = await global::Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args);
        {{extensionsFragments}}
        using (global::Microsoft.Testing.Platform.Builder.ITestApplication app = await builder.BuildAsync())
        {
            return await app.RunAsync();
        }
    }
}
""";
        }
        else if (language == VBLanguageSymbol)
        {
            return $$"""
'------------------------------------------------------------------------------
' <auto-generated>
'     This code was generated by Microsoft.Testing.Platform.MSBuild
' </auto-generated>
'------------------------------------------------------------------------------

<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>
Module TestingPlatformEntryPoint

    Function Main(args As Global.System.String()) As Global.System.Int32
        Return MainAsync(args).Result
    End Function

    Public Async Function MainAsync(ByVal args() As Global.System.String) As Global.System.Threading.Tasks.Task(Of Integer)
        Dim builder = Await Global.Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args)
        {{extensionsFragments}}
        Using testApplication = Await builder.BuildAsync()
            Return Await testApplication.RunAsync()
        End Using
    End Function

End Module
""";
        }
        else if (language == FSharpLanguageSymbol)
        {
            return $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Microsoft.Testing.Platform.MSBuild
// </auto-generated>
//------------------------------------------------------------------------------

[<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
[<EntryPoint>]
let main args =
    task {
        let! builder = Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync args
        {{extensionsFragments}}
        use! app = builder.BuildAsync()
        return! app.RunAsync()
    }
    |> Async.AwaitTask
    |> Async.RunSynchronously
""";
        }

        throw new InvalidOperationException($"Language not supported '{language}'");
    }
}
