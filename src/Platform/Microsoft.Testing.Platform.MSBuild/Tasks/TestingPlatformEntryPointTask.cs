// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// This task generates the entry point for the Testing Platform.
/// </summary>
public sealed class TestingPlatformEntryPointTask : Build.Utilities.Task
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

        if (hostFactory is not null)
        {
            return GetHostedEntryPointSourceCode(language, rootNamespace, generateEntryPoint, hostFactory);
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
internal static class MicrosoftTestingPlatformApplication
{
    public static async global::System.Threading.Tasks.Task<int> RunAsync(string[] args)
    {
        global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder = await global::Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args);
        global::SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args);
        using (global::Microsoft.Testing.Platform.Builder.ITestApplication app = await builder.BuildAsync())
        {
            return await app.RunAsync();
        }
    }
}
{{(generateEntryPoint ? """

[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class MicrosoftTestingPlatformEntryPoint
{
    public static global::System.Threading.Tasks.Task<int> Main(string[] args)
        => MicrosoftTestingPlatformApplication.RunAsync(args);
}
""" : string.Empty)}}
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
    internal static class MicrosoftTestingPlatformApplication
    {
        public static async global::System.Threading.Tasks.Task<int> RunAsync(string[] args)
        {
            global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder = await global::Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args);
            global::{{rootNamespace}}.SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args);
            using (global::Microsoft.Testing.Platform.Builder.ITestApplication app = await builder.BuildAsync())
            {
                return await app.RunAsync();
            }
        }
    }
{{(generateEntryPoint ? """

    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal sealed class MicrosoftTestingPlatformEntryPoint
    {
        public static global::System.Threading.Tasks.Task<int> Main(string[] args)
            => MicrosoftTestingPlatformApplication.RunAsync(args);
    }
""" : string.Empty)}}
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
Friend Module MicrosoftTestingPlatformApplication
    Public Async Function RunAsync(args As String()) As Global.System.Threading.Tasks.Task(Of Integer)
        Dim builder = Await Global.Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args)
        SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)
        Using testApplication = Await builder.BuildAsync()
            Return Await testApplication.RunAsync()
        End Using
    End Function

End Module
{{(generateEntryPoint ? """

<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>
Module MicrosoftTestingPlatformEntryPoint
    Function Main(args As String()) As Integer
        Return MicrosoftTestingPlatformApplication.RunAsync(args).GetAwaiter().GetResult()
    End Function

    Public Function MainAsync(args As String()) As Global.System.Threading.Tasks.Task(Of Integer)
        Return MicrosoftTestingPlatformApplication.RunAsync(args)
    End Function
End Module
""" : string.Empty)}}
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

{{(generateEntryPoint ? string.Empty : "namespace Microsoft.TestingPlatform")}}

module internal MicrosoftTestingPlatformApplication =

    let runAsync args =
        task {
            let! builder = Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync args
            Microsoft.TestingPlatform.Extensions.SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)
            use! app = builder.BuildAsync()
            return! app.RunAsync()
        }
{{(generateEntryPoint ? """

module MicrosoftTestingPlatformEntryPoint =

    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
    [<EntryPoint>]
    let main args =
        MicrosoftTestingPlatformApplication.runAsync args
        |> Async.AwaitTask
        |> Async.RunSynchronously
""" : string.Empty)}}
"""
                : $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Microsoft.Testing.Platform.MSBuild
// </auto-generated>
//------------------------------------------------------------------------------

namespace {{rootNamespace}}

module internal MicrosoftTestingPlatformApplication =

    let runAsync args =
        task {
            let! builder = Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync args
            SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)
            use! app = builder.BuildAsync()
            return! app.RunAsync()
        }
{{(generateEntryPoint ? """

module MicrosoftTestingPlatformEntryPoint =

    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
    [<EntryPoint>]
    let main args =
        MicrosoftTestingPlatformApplication.runAsync args
        |> Async.AwaitTask
        |> Async.RunSynchronously
""" : string.Empty)}}
""";
        }

        throw new InvalidOperationException($"Language not supported '{language}'");
    }

    private static string GetHostedEntryPointSourceCode(string language, string? rootNamespace, bool generateEntryPoint, string hostFactory)
    {
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
internal static class MicrosoftTestingPlatformApplication
{
    public static async global::System.Threading.Tasks.Task<int> RunAsync(string[] args)
    {
        if (global::Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.ShouldBypassApplicationHost(args))
        {
            global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder = await global::Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args);
            AddSelfRegisteredExtensions(builder, args);
            using (global::Microsoft.Testing.Platform.Builder.ITestApplication app = await builder.BuildAsync())
            {
                return await app.RunAsync();
            }
        }

        global::Microsoft.Extensions.Hosting.IHost host = await global::{{hostFactory}}();
        int exitCode;
        try
        {
            exitCode = await global::Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.RunTestingPlatformAsync(
                host,
                args,
                builder => AddSelfRegisteredExtensions(builder, args));
        }
        catch (global::System.Exception operationException)
        {
            try
            {
                await DisposeHostAsync(host);
            }
            catch (global::System.Exception disposeException)
            {
                throw new global::System.AggregateException(
                    "Host disposal failed while handling another exception.",
                    operationException,
                    disposeException);
            }

            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationException).Throw();
            throw;
        }

        await DisposeHostAsync(host);
        return exitCode;
    }

    private static void AddSelfRegisteredExtensions(global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder, string[] args)
        => global::SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args);

    private static async global::System.Threading.Tasks.Task DisposeHostAsync(global::Microsoft.Extensions.Hosting.IHost host)
    {
        if (host is global::System.IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            host.Dispose();
        }
    }

}
{{(generateEntryPoint ? """

[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class MicrosoftTestingPlatformEntryPoint
{
    public static global::System.Threading.Tasks.Task<int> Main(string[] args)
        => MicrosoftTestingPlatformApplication.RunAsync(args);
}
""" : string.Empty)}}
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
    internal static class MicrosoftTestingPlatformApplication
    {
        public static async global::System.Threading.Tasks.Task<int> RunAsync(string[] args)
        {
            if (global::Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.ShouldBypassApplicationHost(args))
            {
                global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder = await global::Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args);
                AddSelfRegisteredExtensions(builder, args);
                using (global::Microsoft.Testing.Platform.Builder.ITestApplication app = await builder.BuildAsync())
                {
                    return await app.RunAsync();
                }
            }

            global::Microsoft.Extensions.Hosting.IHost host = await global::{{hostFactory}}();
            int exitCode;
            try
            {
                exitCode = await global::Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.RunTestingPlatformAsync(
                    host,
                    args,
                    builder => AddSelfRegisteredExtensions(builder, args));
            }
            catch (global::System.Exception operationException)
            {
                try
                {
                    await DisposeHostAsync(host);
                }
                catch (global::System.Exception disposeException)
                {
                    throw new global::System.AggregateException(
                        "Host disposal failed while handling another exception.",
                        operationException,
                        disposeException);
                }

                global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationException).Throw();
                throw;
            }

            await DisposeHostAsync(host);
            return exitCode;
        }

        private static void AddSelfRegisteredExtensions(global::Microsoft.Testing.Platform.Builder.ITestApplicationBuilder builder, string[] args)
            => global::{{rootNamespace}}.SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args);

        private static async global::System.Threading.Tasks.Task DisposeHostAsync(global::Microsoft.Extensions.Hosting.IHost host)
        {
            if (host is global::System.IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                host.Dispose();
            }
        }

    }
{{(generateEntryPoint ? """

    [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal sealed class MicrosoftTestingPlatformEntryPoint
    {
        public static global::System.Threading.Tasks.Task<int> Main(string[] args)
            => MicrosoftTestingPlatformApplication.RunAsync(args);
    }
""" : string.Empty)}}
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
Friend Module MicrosoftTestingPlatformApplication
    Public Async Function RunAsync(args As String()) As Global.System.Threading.Tasks.Task(Of Integer)
        If Global.Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.ShouldBypassApplicationHost(args) Then
            Dim builder = Await Global.Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync(args)
            AddSelfRegisteredExtensions(builder, args)
            Using testApplication = Await builder.BuildAsync()
                Return Await testApplication.RunAsync()
            End Using
        End If

        Dim host As Global.Microsoft.Extensions.Hosting.IHost = Await Global.{{hostFactory}}()
        Dim exitCode As Integer = 0
        Dim runException As Global.System.Exception = Nothing
        Try
            exitCode = Await Global.Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.RunTestingPlatformAsync(
                host,
                args,
                Sub(builder) AddSelfRegisteredExtensions(builder, args))
        Catch ex As Global.System.Exception
            runException = ex
        End Try

        Await DisposeHostAsync(host, runException)
        If runException IsNot Nothing Then
            Global.System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(runException).Throw()
        End If

        Return exitCode
    End Function

    Private Sub AddSelfRegisteredExtensions(builder As Global.Microsoft.Testing.Platform.Builder.ITestApplicationBuilder, args As String())
        SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)
    End Sub

    Private Async Function DisposeHostAsync(host As Global.Microsoft.Extensions.Hosting.IHost, operationException As Global.System.Exception) As Global.System.Threading.Tasks.Task
        Dim disposeException As Global.System.Exception = Nothing
        Try
            Dim asyncDisposable = TryCast(host, Global.System.IAsyncDisposable)
            If asyncDisposable IsNot Nothing Then
                Await asyncDisposable.DisposeAsync()
            Else
                host.Dispose()
            End If
        Catch ex As Global.System.Exception
            disposeException = ex
        End Try

        If disposeException IsNot Nothing Then
            If operationException IsNot Nothing Then
                Throw New Global.System.AggregateException(
                    "Host disposal failed while handling another exception.",
                    operationException,
                    disposeException)
            End If

            Global.System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(disposeException).Throw()
        End If
    End Function

End Module
{{(generateEntryPoint ? """

<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>
Module MicrosoftTestingPlatformEntryPoint
    Function Main(args As String()) As Integer
        Return MicrosoftTestingPlatformApplication.RunAsync(args).GetAwaiter().GetResult()
    End Function

    Public Function MainAsync(args As String()) As Global.System.Threading.Tasks.Task(Of Integer)
        Return MicrosoftTestingPlatformApplication.RunAsync(args)
    End Function
End Module
""" : string.Empty)}}
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

#nowarn "57"

{{(generateEntryPoint ? string.Empty : "namespace Microsoft.TestingPlatform")}}

module internal MicrosoftTestingPlatformApplication =

    let private addSelfRegisteredExtensions builder args =
        Microsoft.TestingPlatform.Extensions.SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)

    let private disposeHostAsync (host: Microsoft.Extensions.Hosting.IHost) (operationException: exn) =
        task {
            let mutable disposeException: exn = null
            try
                match box host with
                | :? System.IAsyncDisposable as asyncDisposable ->
                    do! asyncDisposable.DisposeAsync().AsTask()
                | _ ->
                    host.Dispose()
            with ex ->
                disposeException <- ex

            if not (isNull disposeException) then
                if not (isNull operationException) then
                    return raise (
                        System.AggregateException(
                            "Host disposal failed while handling another exception.",
                            operationException,
                            disposeException))

                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(disposeException).Throw()
        }

    let runAsync args =
        task {
            if Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.ShouldBypassApplicationHost args then
                let! builder = Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync args
                addSelfRegisteredExtensions builder args
                use! app = builder.BuildAsync()
                return! app.RunAsync()
            else
                let! host = {{hostFactory}}()
                let mutable exitCode = 0
                let mutable operationException: exn = null
                try
                    let! result =
                        Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.RunTestingPlatformAsync(
                            host,
                            args,
                            System.Action<_>(fun builder -> addSelfRegisteredExtensions builder args))
                    exitCode <- result
                with ex ->
                    operationException <- ex

                do! disposeHostAsync host operationException
                if not (isNull operationException) then
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationException).Throw()

                return exitCode
        }
{{(generateEntryPoint ? """

module MicrosoftTestingPlatformEntryPoint =

    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
    [<EntryPoint>]
    let main args =
        MicrosoftTestingPlatformApplication.runAsync args
        |> Async.AwaitTask
        |> Async.RunSynchronously
""" : string.Empty)}}
"""
                : $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Microsoft.Testing.Platform.MSBuild
// </auto-generated>
//------------------------------------------------------------------------------

#nowarn "57"

namespace {{rootNamespace}}

module internal MicrosoftTestingPlatformApplication =

    let private addSelfRegisteredExtensions builder args =
        SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)

    let private disposeHostAsync (host: Microsoft.Extensions.Hosting.IHost) (operationException: exn) =
        task {
            let mutable disposeException: exn = null
            try
                match box host with
                | :? System.IAsyncDisposable as asyncDisposable ->
                    do! asyncDisposable.DisposeAsync().AsTask()
                | _ ->
                    host.Dispose()
            with ex ->
                disposeException <- ex

            if not (isNull disposeException) then
                if not (isNull operationException) then
                    return raise (
                        System.AggregateException(
                            "Host disposal failed while handling another exception.",
                            operationException,
                            disposeException))

                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(disposeException).Throw()
        }

    let runAsync args =
        task {
            if Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.ShouldBypassApplicationHost args then
                let! builder = Microsoft.Testing.Platform.Builder.TestApplication.CreateBuilderAsync args
                addSelfRegisteredExtensions builder args
                use! app = builder.BuildAsync()
                return! app.RunAsync()
            else
                let! host = {{hostFactory}}()
                let mutable exitCode = 0
                let mutable operationException: exn = null
                try
                    let! result =
                        Microsoft.Testing.Extensions.MicrosoftExtensionsHostingExtensions.RunTestingPlatformAsync(
                            host,
                            args,
                            System.Action<_>(fun builder -> addSelfRegisteredExtensions builder args))
                    exitCode <- result
                with ex ->
                    operationException <- ex

                do! disposeHostAsync host operationException
                if not (isNull operationException) then
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationException).Throw()

                return exitCode
        }
{{(generateEntryPoint ? """

module MicrosoftTestingPlatformEntryPoint =

    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
    [<EntryPoint>]
    let main args =
        MicrosoftTestingPlatformApplication.runAsync args
        |> Async.AwaitTask
        |> Async.RunSynchronously
""" : string.Empty)}}
""";
        }

        throw new InvalidOperationException($"Language not supported '{language}'");
    }
}
