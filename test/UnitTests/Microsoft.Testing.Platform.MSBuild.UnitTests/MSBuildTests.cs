﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using Microsoft.Build.Framework;
#if NET8_0_OR_GREATER
using Moq;
#endif

namespace Microsoft.Testing.Platform.MSBuild.UnitTests;

[TestGroup]
public class MSBuildTests : TestBase
{
#if NET8_0_OR_GREATER
    private readonly Mock<IBuildEngine> _buildEngine;
    private readonly List<BuildErrorEventArgs> _errors;
#endif

    public MSBuildTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
#if NET8_0_OR_GREATER
        _buildEngine = new Mock<IBuildEngine>();
        _errors = new List<BuildErrorEventArgs>();
        _buildEngine.Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(e => _errors.Add(e));
#endif
    }

    public void Verify_Correct_Registration_Order_For_WellKnown_Extensions()
    {
#if !NET8_0_OR_GREATER
        // On netfx, net6.0, and net7.0 this is failing with:
        // Could not load file or assembly 'Microsoft.Build.Framework, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' or one of its dependencies. The system cannot find the file specified.
        // This is because the NuGet Package is "compatible" with netstandard2.0, so it can be installed everywhere, but it restores dlls only into specific (new) versions of .NET Framework and .NET.
        return;
#else
        InMemoryFileSystem inMemoryFileSystem = new();
        TestingPlatformEntryPointTask testingPlatformEntryPoint = new(inMemoryFileSystem)
        {
            BuildEngine = _buildEngine.Object,
            TestingPlatformEntryPointSourcePath = new CustomTaskItem("obj/entryPointFile"),
            Language = new CustomTaskItem("C#"),
            TestingPlatformBuilderHooks = new List<CustomTaskItem>()
            {
                new CustomTaskItem("95914C54-6C6E-4AF7-9327-4905E1CE9DB7")
                .Add("DisplayName", "DisplayName")
                .Add("TypeFullName", "TypeFullName"),

                // Microsoft.Testing.Extensions.TrxReport
                new CustomTaskItem("2006B3F7-93D2-4D9C-9C69-F41A1F21C9C7")
                .Add("DisplayName", "DisplayName")
                .Add("TypeFullName", "Microsoft.Testing.Extensions.TrxReport"),

                new CustomTaskItem("95914C54-6C6E-4AF7-9327-4905E1CE9DB9")
                .Add("DisplayName", "DisplayName")
                .Add("TypeFullName", "TypeFullName"),
            }.ToArray(),
        };

        testingPlatformEntryPoint.Execute();

        string expectedSourceOrder = """
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
        TypeFullName.AddExtensions(builder, args);
        TypeFullName.AddExtensions(builder, args);
        Microsoft.Testing.Extensions.TrxReport.AddExtensions(builder, args);
        using (global::Microsoft.Testing.Platform.Builder.ITestApplication app = await builder.BuildAsync())
        {
            return await app.RunAsync();
        }
    }
}
""";

        Assert.AreEqual(expectedSourceOrder, inMemoryFileSystem.Files["obj/entryPointFile"]);
#endif
    }

    private sealed class InMemoryFileSystem : IFileSystem
    {
        public Dictionary<string, string?> Files { get; } = new();

        public void CopyFile(string source, string destination) => throw new NotImplementedException();

        public void CreateDirectory(string directory) => throw new NotImplementedException();

        public Stream CreateNew(string path) => throw new NotImplementedException();

        public bool Exist(string path) => Files.ContainsKey(path);

        public void WriteAllText(string path, string? contents) => Files.Add(path, contents);
    }

    private sealed class CustomTaskItem : ITaskItem
    {
        private readonly Dictionary<string, string> _keyValuePairs = new();

        public CustomTaskItem(string itemSpec)
        {
            ItemSpec = itemSpec;
        }

        public CustomTaskItem Add(string key, string value)
        {
            _keyValuePairs[key] = value;
            return this;
        }

        public string ItemSpec { get; set; }

        public ICollection MetadataNames => throw new NotImplementedException();

        public int MetadataCount => _keyValuePairs.Count;

        public IDictionary CloneCustomMetadata() => throw new NotImplementedException();

        public void CopyMetadataTo(ITaskItem destinationItem) => throw new NotImplementedException();

        public string? GetMetadata(string metadataName) => _keyValuePairs.TryGetValue(metadataName, out string? value) ? value : null;

        public void RemoveMetadata(string metadataName) => throw new NotImplementedException();

        public void SetMetadata(string metadataName, string metadataValue) => throw new NotImplementedException();
    }
}
