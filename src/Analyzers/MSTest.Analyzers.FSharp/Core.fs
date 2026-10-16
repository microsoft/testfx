// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.FSharp

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Resources
open System.Text.Json
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

module internal Symbols =
    let frameworkNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting."
    let hasTypeName name (entity: FSharpEntity) = entity.TryGetFullName() = Some name

    let rec inherits name (entity: FSharpEntity) =
        hasTypeName name entity
        || match entity.BaseType with
           | Some parent when parent.HasTypeDefinition -> inherits name parent.TypeDefinition
           | _ -> false

    let attribute name (attributes: seq<FSharpAttribute>) =
        attributes |> Seq.tryFind (fun attribute -> not attribute.IsUnresolved && inherits (frameworkNamespace + name) attribute.AttributeType)

    let hasAttribute name attributes = (attribute name attributes).IsSome
    let attributes name (values: seq<FSharpAttribute>) =
        values |> Seq.filter (fun value -> not value.IsUnresolved && inherits (frameworkNamespace + name) value.AttributeType) |> Seq.toList

    let rec normalizeType (t: FSharpType) = if t.IsAbbreviation then normalizeType t.AbbreviatedType else t
    let typeName (t: FSharpType) =
        let t = normalizeType t
        if t.HasTypeDefinition then t.TypeDefinition.TryGetFullName() else None

    let parameters (m: FSharpMemberOrFunctionOrValue) = m.CurriedParameterGroups |> Seq.collect id |> Seq.toList
    let members (entity: FSharpEntity) = entity.MembersFunctionsAndValues |> Seq.toList
    let isTest (m: FSharpMemberOrFunctionOrValue) = hasAttribute "TestMethodAttribute" m.Attributes
    let rec isTestClass (entity: FSharpEntity) =
        hasAttribute "TestClassAttribute" entity.Attributes
        || match entity.BaseType with
           | Some parent when parent.HasTypeDefinition -> isTestClass parent.TypeDefinition
           | _ -> false
    let positional (attribute: FSharpAttribute) = attribute.ConstructorArguments |> Seq.map snd |> Seq.toList
    let named name (attribute: FSharpAttribute) =
        attribute.NamedArguments |> Seq.tryPick (fun (_, key, _, value) -> if key = name then Some value else None)
    let nonemptyString (value: obj | null) =
        match value with
        | :? string as text -> not (String.IsNullOrWhiteSpace(text))
        | _ -> false

    let rec allEntities (entities: seq<FSharpEntity>) =
        seq {
            for entity in entities do
                yield entity
                yield! allEntities entity.NestedEntities
        }

type internal RuleContext =
    {
        FileName: string
        SourceText: ISourceText
        ParseFileResults: FSharpParseFileResults
        CheckFileResults: FSharpCheckFileResults option
        CheckProjectResults: FSharpCheckProjectResults option
        ProjectOptions: AnalyzerProjectOptions
        TypedTree: FSharpImplementationFileContents option
        AssemblyAttributes: FSharpAttribute list
        DiscoverInternals: bool option
        Settings: Map<string, string>
        Report: string -> string -> string -> range -> (obj | null) array -> unit
    }

module internal Rules =
    let disabledByDefault = set [ "MSTEST0004"; "MSTEST0015"; "MSTEST0019"; "MSTEST0020"; "MSTEST0021"; "MSTEST0022"; "MSTEST0026"; "MSTEST0029"; "MSTEST0067" ]
    let information = set [ "MSTEST0001"; "MSTEST0004"; "MSTEST0015"; "MSTEST0016"; "MSTEST0019"; "MSTEST0020"; "MSTEST0021"; "MSTEST0022"; "MSTEST0023"; "MSTEST0026"; "MSTEST0029"; "MSTEST0037"; "MSTEST0045"; "MSTEST0046"; "MSTEST0049"; "MSTEST0051"; "MSTEST0054"; "MSTEST0058"; "MSTEST0061"; "MSTEST0064"; "MSTEST0066"; "MSTEST0067"; "MSTEST0068"; "MSTEST0071"; "MSTEST0073"; "MSTEST0074"; "MSTEST0075"; "MSTEST0076"; "MSTEST0077"; "MSTEST0079"; "MSTEST0080"; "MSTEST0083"; "MSTEST0084"; "MSTEST0085"; "MSTEST0086" ]

    let private resources = ResourceManager("MSTest.Analyzers.Resources", System.Reflection.Assembly.GetExecutingAssembly())

    let resource name =
        match resources.GetString(name, CultureInfo.CurrentUICulture) with
        | null -> invalidOp $"Missing MSTest analyzer resource '{name}'."
        | text -> text

    let private settings (projectFile: string) =
        // A project-local configuration also works for CLI and editor hosts which don't expose Roslyn analyzer options.
        let directory =
            match Path.GetDirectoryName(projectFile) with
            | null -> invalidOp $"The project path '{projectFile}' has no directory."
            | directory -> directory
        let path = Path.Combine(directory, "mstest-analyzers.json")
        if File.Exists(path) then
            use document = JsonDocument.Parse(File.ReadAllText(path))
            let names = HashSet<string>(StringComparer.Ordinal)
            document.RootElement.EnumerateObject()
            |> Seq.map (fun property ->
                if not (names.Add(property.Name)) then invalidOp $"Duplicate analyzer setting '{property.Name}'."
                let value =
                    match property.Value.ValueKind with
                    | JsonValueKind.String -> property.Value.GetString() |> Option.ofObj |> Option.defaultValue ""
                    | JsonValueKind.True -> "true"
                    | JsonValueKind.False -> "false"
                    | _ -> invalidOp $"Analyzer setting '{property.Name}' must be a string or boolean."
                if property.Name.StartsWith("MSTEST", StringComparison.Ordinal)
                   && property.Name.Length = 10
                   && (property.Name.Substring(6) |> Seq.forall Char.IsDigit)
                   && not (List.contains value [ "none"; "error"; "warning"; "suggestion"; "info"; "silent"; "hint" ]) then
                    invalidOp $"Invalid severity '{value}' for '{property.Name}'."
                property.Name, value)
            |> Map.ofSeq
        else
            Map.empty

    let create fileName sourceText parsed checkedFile checkedProject (projectOptions: AnalyzerProjectOptions) typedTree =
        let configuration = settings projectOptions.ProjectFileName
        let diagnostics = ResizeArray<Message>()
        let reported = HashSet<string * string * range>()
        let assemblyAttributes =
            checkedProject |> Option.map (fun (project: FSharpCheckProjectResults) -> project.AssemblySignature.Attributes |> Seq.toList) |> Option.defaultValue []
        let report code title message diagnosticRange (arguments: (obj | null) array) =
            let configured = configuration |> Map.tryFind code
            let severity =
                match configured with
                | Some "none" -> None
                | Some "error" -> Some Severity.Error
                | Some "warning" -> Some Severity.Warning
                | Some "suggestion" | Some "info" -> Some Severity.Info
                | Some "silent" | Some "hint" -> Some Severity.Hint
                | Some value -> invalidOp $"Invalid severity '{value}' for '{code}'."
                | None when disabledByDefault.Contains(code) -> None
                | None when code = "MSTEST0088" -> Some Severity.Error
                | None when information.Contains(code) -> Some Severity.Info
                | None -> Some Severity.Warning
            match severity with
            | Some severity ->
                let text = String.Format(CultureInfo.CurrentCulture, resource message, arguments)
                if reported.Add(code, text, diagnosticRange) then
                    diagnostics.Add {
                        Type = String.Format(CultureInfo.CurrentCulture, resource title, arguments)
                        Message = text
                        Code = code
                        Severity = severity
                        Range = diagnosticRange
                        Fixes = []
                    }
            | _ -> ()
        {
            FileName = fileName
            SourceText = sourceText
            ParseFileResults = parsed
            CheckFileResults = checkedFile
            CheckProjectResults = checkedProject
            ProjectOptions = projectOptions
            TypedTree = typedTree
            AssemblyAttributes = assemblyAttributes
            DiscoverInternals = checkedProject |> Option.map (fun _ -> Symbols.hasAttribute "DiscoverInternalsAttribute" assemblyAttributes)
            Settings = configuration
            Report = report
        }, diagnostics
