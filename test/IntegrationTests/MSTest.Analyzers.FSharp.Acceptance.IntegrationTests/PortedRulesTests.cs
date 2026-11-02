// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace MSTest.Analyzers.FSharp.Acceptance.IntegrationTests;

public sealed partial class FSharpAnalyzersTests
{
    [TestMethod]
    [DataRow("([||] : string[])", "int[]", true, DisplayName = "DeclaredArrays_RejectEmptyIncompatible")]
    [DataRow("([||] : string[])", "obj[]", false, DisplayName = "DeclaredArrays_EmptyReferenceCovariance")]
    [DataRow("([| \"value\"; null |] : string[])", "obj[]", false, DisplayName = "DeclaredArrays_ReferenceCovariance")]
    [DataRow("([| 1 |] : int[])", "obj[]", true, DisplayName = "DeclaredArrays_RejectValueArrayBoxing")]
    [DataRow("(([| 1 |] : obj[]) :> obj)", "int[]", true, DisplayName = "DeclaredArrays_RejectContentBasedTyping")]
    [DataRow("([||] : ConsoleColor[])", "int[]", true, DisplayName = "DeclaredArrays_RejectEmptyEnumAsInteger")]
    [DataRow("([||] : ConsoleColor[])", "ConsoleColor[]", false, DisplayName = "DeclaredArrays_EmptyEnumIdentity")]
    [DataRow("([| 1 |] : int[])", "int64[]", true, DisplayName = "DeclaredArrays_RejectElementWidening")]
    [DataRow("([||] : int[])", "'T[]", false, DisplayName = "DeclaredArrays_InferEmptyGeneric")]
    [DataRow("([| null; null |] : string[])", "'T[]", false, DisplayName = "DeclaredArrays_InferAllNullGeneric")]
    [DataRow("([||] : int[])", "'T", false, DisplayName = "DeclaredArrays_InferArrayAsGenericValue")]
    [DataRow("([||] : string[])", "System.Collections.Generic.IEnumerable<obj>", false, DisplayName = "DeclaredArrays_GenericInterfaceCovariance")]
    public async Task DataRows_UseDeclaredArrayTypes(string argument, string parameterType, bool shouldReport)
    {
        string generic = parameterType.Contains("'T", StringComparison.Ordinal) ? "<'T>" : string.Empty;
        string source = $$"""
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow({{argument}})>]
    member _.Run{{generic}}(value: {{parameterType}}) = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, "MSTEST0014");
        Assert.HasCount(shouldReport ? 1 : 0, diagnostics);
        foreach (JsonElement diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0014", diagnostic.GetProperty("ruleId").GetString());
        }
    }

    [TestMethod]
    [DataRow("DataRow()", "token: CancellationToken", false, DisplayName = "InjectedToken_TokenOnly")]
    [DataRow("DataRow(null : obj)", "token: CancellationToken", false, DisplayName = "InjectedToken_ExplicitNullTokenOnly")]
    [DataRow("DataRow(7)", "value: int, token: CancellationToken", false, DisplayName = "InjectedToken_DataAndToken")]
    [DataRow("DataRow(7, null)", "value: int, token: CancellationToken", false, DisplayName = "InjectedToken_ExplicitNullWithData")]
    [DataRow("DataRow([| 1; 2 |] : int[])", "values: int[], token: CancellationToken", false, DisplayName = "InjectedToken_TypedArray")]
    [DataRow("DataRow(1, 2)", "values: int[], token: CancellationToken", true, DisplayName = "InjectedToken_RejectTypedArrayExpansion")]
    [DataRow("DataRow(7)", "values: int[], token: CancellationToken", true, DisplayName = "InjectedToken_RejectScalarForTypedArray")]
    [DataRow("DataRow()", "values: int[], token: CancellationToken", true, DisplayName = "InjectedToken_RejectMissingTypedArray")]
    [DataRow("DataRow(1, 2)", "values: obj[], token: CancellationToken", false, DisplayName = "InjectedToken_ObjectArrayExpansion")]
    [DataRow("DataRow(1, null)", "values: obj[], token: CancellationToken", false, DisplayName = "InjectedToken_NullObjectArrayElement")]
    [DataRow("TokenRow([| \"value\" |], null)", "values: obj[], token: CancellationToken", false, DisplayName = "InjectedToken_ExplicitNullAfterArray")]
    [DataRow("DataRow(null, null)", "values: obj[], token: CancellationToken", false, DisplayName = "InjectedToken_ExplicitNullArrayAndToken")]
    [DataRow("DataRow(7)", "value: 'T, token: CancellationToken", false, DisplayName = "InjectedToken_GenericData")]
    [DataRow("DataRow([||] : int[])", "values: 'T[], token: CancellationToken", false, DisplayName = "InjectedToken_GenericEmptyArray")]
    [DataRow("DataRow(7)", "value: int, token: byref<CancellationToken>", true, DisplayName = "InjectedToken_ByRefIsNotInjected")]
    public async Task DataRows_NormalizeInjectedCancellationToken(string attribute, string parameters, bool shouldReport)
    {
        string generic = parameters.Contains("'T", StringComparison.Ordinal) ? "<'T>" : string.Empty;
        string source = $$"""
namespace Consumer
open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
type TokenRowAttribute(values: string[], token: obj) =
    inherit DataRowAttribute([| values :> obj; token |])
[<TestClass>]
type Tests() =
    [<TestMethod; {{attribute}}>]
    member _.Run{{generic}}({{parameters}}) = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, "MSTEST0014");
        Assert.HasCount(shouldReport ? 1 : 0, diagnostics);
        foreach (JsonElement diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0014", diagnostic.GetProperty("ruleId").GetString());
        }
    }

    [TestMethod]
    [DataRow("DataRow", true, "full", "args", true, DisplayName = "ConflictingNames_ExactAttribute")]
    [DataRow("DataRowAttribute", false, "full", "args", true, DisplayName = "ConflictingNames_ExactInitializer")]
    [DataRow("CustomRow", true, "full", "args", false, DisplayName = "ConflictingNames_CustomAttribute")]
    [DataRow("CustomRow", false, "full", "args", false, DisplayName = "ConflictingNames_CustomInitializer")]
    [DataRow("DataRow", true, " ", "args", false, DisplayName = "ConflictingNames_WhitespaceFullName")]
    [DataRow("DataRow", true, "full", " ", false, DisplayName = "ConflictingNames_WhitespaceArgumentsName")]
    [DataRow("TestDataRow<int>", false, "", "args", true, DisplayName = "ConflictingNames_TestDataRowEmptyFullName")]
    public async Task ConflictingDisplayNames_UseExactSupportedTypes(string typeName, bool attributeForm, string fullName, string argumentsName, bool shouldReport)
    {
        string initialization = $"{typeName}(1, DisplayName = \"{fullName}\", ArgumentsDisplayName = \"{argumentsName}\")";
        string source = $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type CustomRow(value: int) =
    inherit DataRowAttribute(value)
    override _.GetDisplayName(_, _) = "custom"
[<TestClass>]
type Tests() =
    [<TestMethod{{(attributeForm ? "; " + initialization : string.Empty)}}>]
    member _.Run(value: int) =
        {{(attributeForm ? "()" : $"""
let row = {initialization}
        ignore row
""")}}
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, "MSTEST0089");
        Assert.HasCount(shouldReport ? 1 : 0, diagnostics);
        foreach (JsonElement diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0089", diagnostic.GetProperty("ruleId").GetString());
        }
    }

    [TestMethod]
    [DataRow("MSTEST0030", DisplayName = "UnannotatedDerivedClass_Discovery")]
    [DataRow("MSTEST0078", DisplayName = "UnannotatedDerivedClass_Dependency")]
    public async Task UnannotatedDerivedClass_IsNotDiscoverable(string rule)
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type BaseTests() =
    [<TestMethod>]
    member _.InheritedTest() = ()
type DerivedTests() =
    inherit BaseTests()
[<TestClass>]
type Tests() =
    [<TestMethod; DependsOn(typeof<DerivedTests>, "InheritedTest")>]
    member _.Run() = ()
""";
        JsonElement diagnostic = Assert.ContainsSingle(await AnalyzeAsync(source, FindingsExitCode, rule));
        Assert.AreEqual(rule, diagnostic.GetProperty("ruleId").GetString());
    }

    [TestMethod]
    [DataRow(false, DisplayName = "DynamicData_NearestSource")]
    [DataRow(true, DisplayName = "DynamicData_NearestDisplayName")]
    public async Task DynamicData_UsesNearestDeclaringType(bool displayName)
    {
        string source = $$"""
namespace Consumer
open System.Reflection
open Microsoft.VisualStudio.TestTools.UnitTesting
type BaseTests() =
    static member Data(value: int) = [| [| box value |] |]
    static member Name(methodInfo: MethodInfo, data: obj[]) = "base"
[<TestClass>]
type Tests() =
    inherit BaseTests()
    static member Data(value: int) = [| [| box value |] |]
    static member Name(methodInfo: MethodInfo, data: obj[]) = "derived"
    [<TestMethod; DynamicData("Data", 1{{(displayName ? ", DynamicDataDisplayName = \"Name\"" : string.Empty)}})>]
    member _.Run(value: int) = ()
""";
        Assert.IsEmpty(await AnalyzeAsync(source, 0, "MSTEST0018"));
    }

    [TestMethod]
    public async Task ParameterlessDataRow_UsesCompiledArity()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow()>]
    member _.Run() = ()
""";
        Assert.IsEmpty(await AnalyzeAsync(source, 0, "MSTEST0014"));
    }

    [TestMethod]
    public async Task ParameterlessDynamicData_UsesCompiledArity()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    static member Data() = [| [| box 1 |] |]
    [<TestMethod; DynamicData("Data")>]
    member _.Run(value: int) = ()
""";
        Assert.IsEmpty(await AnalyzeAsync(source, 0, "MSTEST0018"));
    }

    [TestMethod]
    public async Task ParameterlessMemberCondition_UsesCompiledArity()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Conditions =
    static member Ready() = true
[<TestClass; MemberCondition(typeof<Conditions>, "Ready")>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""";
        Assert.IsEmpty(await AnalyzeAsync(source, 0, "MSTEST0070"));
    }

    [TestMethod]
    public async Task MemberCondition_StillSearchesFlattenedBaseMembers()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type BaseConditions() =
    static member Ready() = true
type Conditions() =
    inherit BaseConditions()
    static member Ready(value: int) = true
[<TestClass; MemberCondition(typeof<Conditions>, "Ready")>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""";
        Assert.IsEmpty(await AnalyzeAsync(source, 0, "MSTEST0070"));
    }

    [TestMethod]
    public async Task ParameterlessFilterConstructor_UsesCompiledArity()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Filter() =
    interface ITestFilter with
        member _.Filter(_) = TestFilterResult.Run
[<assembly: TestFilterProvider(typeof<Filter>)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""";
        Assert.IsEmpty(await AnalyzeAsync(source, 0, "MSTEST0081"));
    }

    [TestMethod]
    [DataRow("ConsoleColor.Red", "ConsoleColor", false, DisplayName = "DataRow_EnumIdentity")]
    [DataRow("[| ConsoleColor.Red; ConsoleColor.Blue |]", "ConsoleColor[]", false, DisplayName = "DataRow_EnumArrayIdentity")]
    [DataRow("AttributeTargets.Class", "ConsoleColor", true, DisplayName = "DataRow_WrongEnum")]
    [DataRow("1", "ConsoleColor", true, DisplayName = "DataRow_IntegerIsNotEnum")]
    [DataRow("ConsoleColor.Red", "int", true, DisplayName = "DataRow_EnumIsNotInteger")]
    [DataRow("1", "float", false, DisplayName = "DataRow_WidenToDouble")]
    [DataRow("1", "int64", false, DisplayName = "DataRow_WidenToInt64")]
    [DataRow("1", "decimal", false, DisplayName = "DataRow_WidenToDecimal")]
    [DataRow("'c'", "int", false, DisplayName = "DataRow_WidenCharToInt32")]
    [DataRow("1L", "int", true, DisplayName = "DataRow_RejectInt64Narrowing")]
    [DataRow("1.0", "int", true, DisplayName = "DataRow_RejectDoubleNarrowing")]
    public async Task DataRows_PreserveEnumIdentityAndNumericConversions(string argument, string parameterType, bool shouldReport)
    {
        string source = $$"""
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow({{argument}})>]
    member _.Run(value: {{parameterType}}) = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, "MSTEST0014");
        Assert.HasCount(shouldReport ? 1 : 0, diagnostics);
        foreach (JsonElement diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0014", diagnostic.GetProperty("ruleId").GetString());
        }
    }

    [TestMethod]
    [DataRow("TestMethod", "member _.InheritedTest() = ()", true, false, DisplayName = "LegacyMember_HiddenTest")]
    [DataRow("TestMethod", "member _.InheritedTest() = ()", false, true, DisplayName = "LegacyMember_InheritedTest")]
    [DataRow("TestMethod", "member _.InheritedTest() = 42", false, false, DisplayName = "LegacyMember_InvalidReturn")]
    [DataRow("TestMethod", "static member InheritedTest() = ()", false, false, DisplayName = "LegacyMember_StaticTest")]
    [DataRow("TestInitialize", "member _.InheritedTest() = ()", true, false, DisplayName = "LegacyMember_HiddenFixture")]
    [DataRow("TestInitialize", "member _.InheritedTest() = ()", false, true, DisplayName = "LegacyMember_InheritedFixture")]
    [DataRow("TestInitialize", "member _.InheritedTest(value: int) = ()", false, false, DisplayName = "LegacyMember_ParameterizedFixture")]
    public async Task LegacyInheritedMembers_RequireEffectiveRuntimeSignatures(string attributeName, string declaration, bool hidden, bool shouldReport)
    {
        string source = $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    inherit Legacy.Fixture()
    {{(hidden ? $"""
[<{attributeName}>]
    member _.InheritedTest() = ()
""" : string.Empty)}}
    [<TestMethod>]
    member _.Run() = ()
#file Legacy/Legacy.fsproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <Compile Include="Fixture.fs" />
    <PackageReference Include="MSTest.TestFramework" Version="3.11.1" PrivateAssets="all" />
  </ItemGroup>
</Project>
#file Legacy/Fixture.fs
namespace Legacy
open Microsoft.VisualStudio.TestTools.UnitTesting
type Fixture() =
    [<{{attributeName}}>]
    {{declaration}}
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, "MSTEST0082");
        Assert.HasCount(shouldReport ? 1 : 0, diagnostics);
        foreach (JsonElement diagnostic in diagnostics)
        {
            Assert.AreEqual("MSTEST0082", diagnostic.GetProperty("ruleId").GetString());
        }
    }

    [TestMethod]
    public async Task ProjectAnalysis_ReusesSnapshotCachesAndInvalidatesChangedProjects()
    {
        const string program = """"
namespace EditorHost
open System
open System.IO
open System.Reflection
open System.Runtime.CompilerServices
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Text
open Microsoft.Extensions.DependencyInjection
open MSTest.Analyzers.FSharp

module Program =
    [<EntryPoint>]
    let main _ =
        async {
            let registration = """
namespace Consumer
open Microsoft.Extensions.DependencyInjection
module Registration =
    let register (services: IServiceCollection) =
        services.AddMSTestTestClassInjection()
"""
            let sample = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests(service: string) =
    [<TestMethod>]
    member _.Run() = ()
    [<TestMethod; DependsOn("Second")>]
    member _.First() = ()
    [<TestMethod; DependsOn("First")>]
    member _.Second() = ()
"""
            let root = Directory.GetCurrentDirectory()
            let registrationPath = Path.Combine(root, "Registration.fs")
            let samplePath = Path.Combine(root, "Sample.fs")
            File.WriteAllText(registrationPath, registration)
            File.WriteAllText(samplePath, sample)
            let references =
                [| yield! (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") :?> string).Split(Path.PathSeparator)
                   yield typeof<MSTestHostingServiceCollectionExtensions>.Assembly.Location
                   yield typeof<IServiceCollection>.Assembly.Location |]
                |> Array.distinct
                |> Array.map (fun path -> "-r:" + path)
            let checker = FSharpChecker.Create(keepAssemblyContents = true)
            let options =
                checker.GetProjectOptionsFromCommandLineArgs(
                    Path.Combine(root, "Analysis.fsproj"),
                    [| "--target:library"; "--targetprofile:netcore"; "--noframework"; yield! references; registrationPath; samplePath |])
            let checkProject () = async {
                let! project = checker.ParseAndCheckProject(options)
                let errors = project.Diagnostics |> Array.filter (fun diagnostic -> diagnostic.Severity = FSharpDiagnosticSeverity.Error)
                if not (Array.isEmpty errors) then failwithf "Samples failed to type-check: %A" errors
                return project
            }
            let context project path text : Async<EditorContext> = async {
                let sourceText = SourceText.ofString text
                let! parsed, answer = checker.ParseAndCheckFileInProject(path, 0, sourceText, options)
                let checkedFile =
                    match answer with
                    | FSharpCheckFileAnswer.Succeeded checkedFile -> checkedFile
                    | FSharpCheckFileAnswer.Aborted -> failwith "Sample type-check was aborted."
                return {
                    FileName = path
                    SourceText = sourceText
                    ParseFileResults = parsed
                    CheckFileResults = Some checkedFile
                    TypedTree = checkedFile.ImplementationFile
                    CheckProjectResults = Some project
                    ProjectOptions = AnalyzerProjectOptions.BackgroundCompilerOptions options
                    AnalyzerIgnoreRanges = Map.empty
                }
            }
            let codes (diagnostics: Message list) = diagnostics |> List.map (fun diagnostic -> diagnostic.Code) |> List.sort
            let! project = checkProject ()
            let! registrationContext = context project registrationPath registration
            let! sampleContext = context project samplePath sample
            let! registrationDiagnostics = Analyzers.analyzeEditor registrationContext
            if codes registrationDiagnostics <> [ "MSTEST0088" ]
               || registrationDiagnostics.Head.Range.FileName <> registrationPath
               || registrationDiagnostics.Head.Range.StartLine <> 6 then
                failwithf "Incorrect registration diagnostics: %A" registrationDiagnostics
            let! sampleDiagnostics = Analyzers.analyzeEditor sampleContext
            if codes sampleDiagnostics <> [ "MSTEST0078"; "MSTEST0078" ]
               || (sampleDiagnostics |> List.exists (fun diagnostic -> diagnostic.Range.FileName <> samplePath)) then
                failwithf "Cross-file hosting injection or cycle reporting changed: %A" sampleDiagnostics
            let flags = BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic
            let analyzerAssembly = Assembly.Load("MSTest.Analyzers.FSharp")
            let accessor = analyzerAssembly.GetType("MSTest.Analyzers.FSharp.DeclarationRules", true).GetMethod("get_hostingLocations", flags)
            if isNull accessor then failwith "Hosting analysis is missing its per-snapshot cache."
            let cache = accessor.Invoke(null, [||]) :?> ConditionalWeakTable<FSharpCheckProjectResults, Lazy<range list>>
            let mutable cached = Unchecked.defaultof<Lazy<range list>>
            if not (cache.TryGetValue(project, &cached)) || not cached.IsValueCreated || cached.Value.Length <> 1 then
                failwith "Hosting locations were not computed once for the checked project."
            let graphAccessor = analyzerAssembly.GetType("MSTest.Analyzers.FSharp.DependencyRules", true).GetMethod("get_dependencyDiagnostics", flags)
            if isNull graphAccessor then failwith "Dependency analysis is missing its per-snapshot cache."
            let graphCache =
                graphAccessor.Invoke(null, [||])
                |> Option.ofObj
                |> Option.defaultWith (fun () -> failwith "Dependency cache was not initialized.")
            let graphValue (snapshot: FSharpCheckProjectResults) =
                let values = [| box snapshot; null |]
                let found = graphCache.GetType().GetMethod("TryGetValue").Invoke(graphCache, values) :?> bool
                if found then Some values[1] else None
            let graph =
                match graphValue project with
                | Some value when not (isNull value) -> value
                | _ -> failwith "Dependency diagnostics were not cached for the checked project."
            if not (graph.GetType().GetProperty("IsValueCreated").GetValue(graph) :?> bool) then
                failwith "Dependency analysis did not compute the cached diagnostics."
            let! repeated = [| for _ in 1 .. 4 -> Analyzers.analyzeEditor registrationContext |] |> Async.Parallel
            if repeated |> Array.exists (fun diagnostics -> diagnostics <> registrationDiagnostics) then
                failwithf "Concurrent analysis changed diagnostics: %A" repeated
            let mutable reused = Unchecked.defaultof<Lazy<range list>>
            if not (cache.TryGetValue(project, &reused)) || not (Object.ReferenceEquals(cached, reused)) then
                failwith "Per-file analysis replaced the checked-project cache."
            if not (graphValue project |> Option.exists (fun value -> Object.ReferenceEquals(graph, value))) then
                failwith "Per-file analysis replaced the dependency cache."
            let! partial = Analyzers.analyzeEditor { registrationContext with CheckProjectResults = None }
            if partial <> registrationDiagnostics then failwithf "Partial snapshot lost hosting diagnostics: %A" partial
            let settingsPath = Path.Combine(root, "mstest-analyzers.json")
            let settings = File.ReadAllText(settingsPath)
            File.WriteAllText(settingsPath, settings.Replace("\"warning\"", "\"none\"", StringComparison.Ordinal))
            let! disabled = Analyzers.analyzeEditor registrationContext
            if not disabled.IsEmpty then failwithf "Disabled hosting diagnostics were reported: %A" disabled
            checker.InvalidateConfiguration(options)
            let! disabledProject = checkProject ()
            let! disabledContext = context disabledProject samplePath sample
            let! disabledGraph = Analyzers.analyzeEditor disabledContext
            if not disabledGraph.IsEmpty || (graphValue disabledProject).IsSome then
                failwithf "Disabled dependency analysis still computed a graph: %A" disabledGraph
            File.WriteAllText(settingsPath, settings)
            File.WriteAllText(registrationPath, registration.Replace("services.AddMSTestTestClassInjection()", "services", StringComparison.Ordinal))
            checker.InvalidateConfiguration(options)
            let! updated = checkProject ()
            let! updatedContext = context updated samplePath sample
            let! updatedDiagnostics = Analyzers.analyzeEditor updatedContext
            if codes updatedDiagnostics <> [ "MSTEST0063"; "MSTEST0078"; "MSTEST0078" ] then
                failwithf "A new checked project reused stale analysis: %A" updatedDiagnostics
            let mutable refreshed = Unchecked.defaultof<Lazy<range list>>
            if not (cache.TryGetValue(updated, &refreshed)) || Object.ReferenceEquals(cached, refreshed) || not refreshed.Value.IsEmpty then
                failwith "The updated checked project did not get independent hosting locations."
            if not (graphValue updated |> Option.exists (fun value -> not (Object.ReferenceEquals(graph, value)))) then
                failwith "The updated checked project did not get independent dependency diagnostics."
            printfn "PROJECT_ANALYSIS_VALIDATED:SNAPSHOT_REUSED;REFRESHED;PARTIAL;DISABLED"
            return 0
        }
        |> Async.RunSynchronously
"""";
        using TestAsset asset = await GenerateConsumerAsync(program, editorHost: true, hosting: true);
        await WriteRuleConfigurationAsync(asset.TargetAssetPath, ["MSTEST0063", "MSTEST0078", "MSTEST0088"], new Dictionary<string, string>
        {
            ["MSTestHostTestClassInjectionEnabled"] = "false",
            ["PublishAot"] = "true",
        });
        await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\"", cancellationToken: testContext.CancellationToken);
        await AssertPackedProvenanceAsync(asset.TargetAssetPath);
        using DotnetMuxer host = new();
        int exitCode = await host.ExecuteAsync(
            $"\"{Path.Combine(asset.TargetAssetPath, "bin", "Debug", "net10.0", "Consumer.dll")}\"",
            asset.TargetAssetPath,
            testContext.CancellationToken);
        Assert.AreEqual(0, exitCode, host.StandardOutput + host.StandardError);
        Assert.AreEqual("PROJECT_ANALYSIS_VALIDATED:SNAPSHOT_REUSED;REFRESHED;PARTIAL;DISABLED", host.StandardOutput.Trim());
    }

    [TestMethod]
    public async Task DataRows_PreserveDistinctGenericDiagnosticsAtTheSameLocation()
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(null, null)>]
    member _.Run<'T, 'U>(first: 'T, second: 'U) = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, FindingsExitCode, "MSTEST0014");
        Assert.HasCount(2, diagnostics);
        Assert.AreSequenceEqual(
            new[] { "The type of the generic parameter 'T' could not be inferred.", "The type of the generic parameter 'U' could not be inferred." },
            diagnostics.Select(diagnostic => diagnostic.GetProperty("message").GetProperty("text").GetString()!).Order().ToArray());
    }

    [TestMethod]
    [DataRow("""{"MSTEST0017":"invalid"}""", "Invalid severity", DisplayName = "InvalidConfiguration_InvalidSeverity")]
    [DataRow("""{"MSTEST0017":"none","MSTEST0017":"error"}""", "Duplicate analyzer setting", DisplayName = "InvalidConfiguration_DuplicateSetting")]
    public async Task InvalidConfiguration_FailsWithoutSuccessShapedFallback(string settings, string expectedError)
    {
        const string source = """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""";
        using TestAsset asset = await GenerateConsumerAsync(source);
        await File.WriteAllTextAsync(Path.Combine(asset.TargetAssetPath, "mstest-analyzers.json"), settings, testContext.CancellationToken);
        await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\"", cancellationToken: testContext.CancellationToken);
        string analyzerPath = await AssertPackedProvenanceAsync(asset.TargetAssetPath);
        string reportPath = Path.Combine(asset.TargetAssetPath, "diagnostics.sarif");
        (int exitCode, string standardOutput, string standardError) = await RunAnalyzerCliAsync(asset.TargetAssetPath, analyzerPath, reportPath);
        Assert.AreEqual(22, exitCode, standardOutput + standardError);
        Assert.Contains(expectedError, standardOutput + standardError);
        Assert.IsFalse(File.Exists(reportPath), "Invalid configuration must not produce a success-shaped diagnostic report.");
    }

    [TestMethod]
    public void RuleInventory_AccountsForEveryDiagnosticThroughMSTEST0089()
    {
        string source = File.ReadAllText(Path.Combine(Constants.Root, "src", "Analyzers", "MSTest.Analyzers", "Helpers", "DiagnosticIds.cs"));
        string[] current = [.. Regex.Matches(source, @"(?m)^\s*public const string \w+ = ""(MSTEST\d+)"";")
            .Select(match => match.Groups[1].Value)
            .Where(rule => string.CompareOrdinal(rule, "MSTEST0089") <= 0)];
        string[] excluded = ["MSTEST0026", "MSTEST0027", "MSTEST0028", "MSTEST0033", "MSTEST0040", "MSTEST0047", "MSTEST0055"];
        (string Rule, string Source, bool ShouldReport)[] cases = [.. DataRuleCases.Cases, .. DeclarationRuleCases.Cases, .. ExpressionRuleCases.Cases];
        string[] pilotAndCrossAssembly = ["MSTEST0003", "MSTEST0017", "MSTEST0066", "MSTEST0082"];
        string[] ported = [.. cases.Select(scenario => scenario.Rule).Concat(pilotAndCrossAssembly).Distinct().Order()];
        Assert.HasCount(77, ported);
        Assert.AreSequenceEqual(current.Except(excluded).Order().ToArray(), ported);
        foreach (string rule in ported.Except(pilotAndCrossAssembly))
        {
            Assert.IsNotEmpty(cases.Where(scenario => scenario.Rule == rule && scenario.ShouldReport), $"{rule} has no positive package-consumer case.");
            Assert.IsNotEmpty(cases.Where(scenario => scenario.Rule == rule && !scenario.ShouldReport), $"{rule} has no negative package-consumer case.");
        }
    }

    [TestMethod]
    public async Task DefaultConfiguration_DoesNotEnableOpposingDesignPreferences()
    {
        const string source = """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
    [<TestInitialize>]
    member _.Initialize() = ()
    [<TestCleanup>]
    member _.Cleanup() = ()
    interface IDisposable with
        member _.Dispose() = ()
""";
        Assert.IsEmpty(await AnalyzeAsync(source, 0, useDefaults: true));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task InheritedFixture_ReportsOnlyMismatchedFrameworkIdentity(bool legacy)
    {
        string frameworkPackage = Directory.GetFiles(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework.*.nupkg")
            .Concat(Directory.GetFiles(Constants.ArtifactsTmpPackages, "MSTest.TestFramework.*.nupkg")).Single();
        using System.IO.Compression.ZipArchive frameworkArchive = System.IO.Compression.ZipFile.OpenRead(frameworkPackage);
        using Stream manifest = frameworkArchive.GetEntry("MSTest.TestFramework.nuspec")!.Open();
        string version = legacy ? "3.11.1" : XDocument.Load(manifest).Descendants().Single(element => element.Name.LocalName == "version").Value;
        string source = $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    inherit Legacy.Fixture()
    [<TestMethod>]
    member _.Run() = ()
#file Legacy/Legacy.fsproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup>
    <Compile Include="Fixture.fs" />
    <PackageReference Include="MSTest.TestFramework" Version="{{version}}" PrivateAssets="all" />
  </ItemGroup>
</Project>
#file Legacy/Fixture.fs
namespace Legacy
open Microsoft.VisualStudio.TestTools.UnitTesting
type OldTestAttribute() =
    inherit TestMethodAttribute()
type Fixture() =
    [<TestInitialize>]
    member _.Initialize() = ()
    [<OldTest>]
    member _.InheritedTest() = ()
""";
        JsonElement[] diagnostics = await AnalyzeAsync(source, legacy ? FindingsExitCode : 0, "MSTEST0082");
        if (legacy)
        {
            Assert.HasCount(2, diagnostics);
            foreach (JsonElement diagnostic in diagnostics)
            {
                Assert.AreEqual("MSTEST0082", diagnostic.GetProperty("ruleId").GetString());
                Assert.Contains("Microsoft.VisualStudio.TestPlatform.TestFramework", diagnostic.GetProperty("message").GetProperty("text").GetString()!);
            }
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }

    public static IEnumerable<TestDataRow<(string Rule, string Source, bool ShouldReport)>> PortedRuleCases
    {
        get
        {
            IEnumerable<(string Rule, string Source, bool ShouldReport)> cases = DataRuleCases.Cases.Concat(DeclarationRuleCases.Cases).Concat(ExpressionRuleCases.Cases);
            int index = 0;
            foreach ((string Rule, string Source, bool ShouldReport) scenario in cases)
            {
                yield return new(scenario) { DisplayName = $"PortedRules_{scenario.Rule}_{(scenario.ShouldReport ? "Positive" : "Negative")}_{++index}" };
            }
        }
    }

    public static IEnumerable<TestDataRow<(string Rule, string Source, bool ShouldReport, IReadOnlyDictionary<string, string> Settings)>> ConfiguredRuleCases =>
        DeclarationRuleCases.ConfiguredCases.Select((scenario, index) => new TestDataRow<(string Rule, string Source, bool ShouldReport, IReadOnlyDictionary<string, string> Settings)>(scenario)
        {
            DisplayName = $"ConfiguredRules_{scenario.Rule}_{(scenario.ShouldReport ? "Positive" : "Negative")}_{index + 1}",
        });

    [TestMethod]
    [DynamicData(nameof(ConfiguredRuleCases))]
    public async Task ConfiguredRules_HonorProjectCapabilities(string rule, string source, bool shouldReport, IReadOnlyDictionary<string, string> settings)
    {
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, rule, settings);
        Assert.AreEqual(shouldReport, diagnostics.Length > 0);
        foreach (JsonElement diagnostic in diagnostics)
        {
            Assert.AreEqual(rule, diagnostic.GetProperty("ruleId").GetString());
        }
    }

    [TestMethod]
    [DynamicData(nameof(PortedRuleCases))]
    public async Task PortedRules_ReportOnlyApplicableDiagnostics(string rule, string source, bool shouldReport)
    {
        JsonElement[] diagnostics = await AnalyzeAsync(source, shouldReport ? FindingsExitCode : 0, rule);
        if (shouldReport)
        {
            Assert.IsNotEmpty(diagnostics);
            foreach (JsonElement diagnostic in diagnostics)
            {
                Assert.AreEqual(rule, diagnostic.GetProperty("ruleId").GetString());
                Assert.IsNotNull(diagnostic.GetProperty("message").GetProperty("text").GetString());
                JsonElement region = diagnostic.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region");
                Assert.IsGreaterThan(0, region.GetProperty("startLine").GetInt32());
            }
        }
        else
        {
            Assert.IsEmpty(diagnostics);
        }
    }
}
