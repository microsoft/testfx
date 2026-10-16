// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.FSharp

open System
open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text

module private Analysis =
    let private frameworkNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting."

    let private hasTypeName name (entity: FSharpEntity) =
        entity.TryGetFullName() = Some name

    let rec private isAttribute name (entity: FSharpEntity) =
        hasTypeName (frameworkNamespace + name) entity
        || match entity.BaseType with
           | Some baseType when baseType.HasTypeDefinition -> isAttribute name baseType.TypeDefinition
           | _ -> false

    let private hasAttribute name (attributes: seq<FSharpAttribute>) =
        attributes |> Seq.exists (fun attribute -> not attribute.IsUnresolved && isAttribute name attribute.AttributeType)

    let rec private normalizeType (typeInfo: FSharpType) =
        if typeInfo.IsAbbreviation then normalizeType typeInfo.AbbreviatedType else typeInfo

    let private validReturnType (typeInfo: FSharpType) =
        let typeInfo = normalizeType typeInfo
        typeInfo.HasTypeDefinition
        && (match typeInfo.TypeDefinition.TryGetFullName() with
            | Some "Microsoft.FSharp.Core.Unit"
            | Some "System.Void"
            | Some "System.Threading.Tasks.Task"
            | Some "System.Threading.Tasks.ValueTask" -> true
            | _ -> false)

    let rec private containsParameter (parameter: FSharpGenericParameter) (typeInfo: FSharpType) =
        let typeInfo = normalizeType typeInfo
        if typeInfo.IsGenericParameter then
            typeInfo.GenericParameter.Equals(parameter)
        else
            typeInfo.GenericArguments |> Seq.exists (containsParameter parameter)

    let private accessible discoverInternals (accessibility: FSharpAccessibility) =
        accessibility.IsPublic || (discoverInternals <> Some false && accessibility.IsInternal)

    let rec private accessibleEntity discoverInternals (entity: FSharpEntity) =
        accessible discoverInternals entity.Accessibility
        && (match entity.DeclaringEntity with
            | Some parent -> accessibleEntity discoverInternals parent
            | None -> true)

    let private hasDefaultImplementation (entity: FSharpEntity) (methodInfo: FSharpMemberOrFunctionOrValue) =
        let matchesSlot (signature: FSharpAbstractSignature) =
            let rec sameType (left: FSharpType) (right: FSharpType) =
                let left = normalizeType left
                let right = normalizeType right
                if left.Equals(right) then
                    true
                elif left.IsGenericParameter && right.IsGenericParameter then
                    let leftIndex = methodInfo.GenericParameters |> Seq.tryFindIndex (fun parameter -> parameter.Equals(left.GenericParameter))
                    let rightIndex = signature.MethodGenericParameters |> Seq.tryFindIndex (fun parameter -> parameter.Equals(right.GenericParameter))
                    leftIndex.IsSome && leftIndex = rightIndex
                else
                    let sameShape =
                        (left.HasTypeDefinition && right.HasTypeDefinition && left.TypeDefinition.Equals(right.TypeDefinition))
                        || (left.IsFunctionType && right.IsFunctionType)
                        || (left.IsTupleType && right.IsTupleType && left.IsStructTupleType = right.IsStructTupleType)
                    sameShape
                    && left.GenericArguments.Count = right.GenericArguments.Count
                    && Seq.forall2 sameType left.GenericArguments right.GenericArguments

            let parameters = methodInfo.CurriedParameterGroups |> Seq.collect id |> Seq.toArray
            let arguments = signature.AbstractArguments |> Seq.collect id |> Seq.toArray
            signature.DeclaringType.HasTypeDefinition
            && signature.DeclaringType.TypeDefinition.Equals(entity)
            && (signature.Name = methodInfo.LogicalName || signature.Name = methodInfo.CompiledName)
            && methodInfo.GenericParameters.Count = signature.MethodGenericParameters.Count
            && parameters.Length = arguments.Length
            && Array.forall2 (fun (parameter: FSharpParameter) (argument: FSharpAbstractParameter) -> sameType parameter.Type argument.Type) parameters arguments
            && sameType methodInfo.ReturnParameter.Type signature.AbstractReturnType

        entity.MembersFunctionsAndValues
        |> Seq.exists (fun memberInfo ->
            memberInfo.IsOverrideOrExplicitInterfaceImplementation
            && (memberInfo.ImplementedAbstractSignatures |> Seq.exists matchesSlot))

    let private validMethod discoverInternals entity (methodInfo: FSharpMemberOrFunctionOrValue) =
        methodInfo.IsInstanceMemberInCompiledCode
        // F# dispatch slots also represent virtual methods with a default implementation.
        && (not methodInfo.IsDispatchSlot || hasDefaultImplementation entity methodInfo)
        && not methodInfo.IsProperty
        && not methodInfo.IsPropertyAccessor
        && accessible discoverInternals methodInfo.Accessibility
        && (methodInfo.DeclaringEntity |> Option.exists (accessibleEntity discoverInternals))
        && validReturnType methodInfo.ReturnParameter.Type
        && (methodInfo.GenericParameters
            |> Seq.forall (fun parameter ->
                methodInfo.CurriedParameterGroups
                |> Seq.collect id
                |> Seq.exists (fun argument -> containsParameter parameter argument.Type)))

    let private hasJustification (attribute: FSharpAttribute) =
        let isMessage (value: obj | null) =
            match value with
            | :? string as message -> not (String.IsNullOrWhiteSpace(message))
            | _ -> false

        (attribute.ConstructorArguments |> Seq.tryHead |> Option.exists (fun (_, value) -> isMessage value))
        || (attribute.NamedArguments
            |> Seq.exists (fun (_, name, _, value) -> name = "IgnoreMessage" && isMessage value))

    let rec private unwrap expression =
        match expression with
        | Coerce(_, inner) -> unwrap inner
        | _ -> expression

    let private isConstant expression =
        match unwrap expression with
        | Const _ -> true
        | Value value -> value.LiteralValue.IsSome
        | Call(_, memberInfo, _, _, _) when
            memberInfo.CompiledName = "TypeOf"
            && (memberInfo.DeclaringEntity |> Option.exists (hasTypeName "Microsoft.FSharp.Core.Operators")) -> true
        | _ -> false

    let private argumentName expression =
        match unwrap expression with
        | Value value -> Some value.DisplayName
        | FSharpFieldGet(_, _, field) -> Some field.Name
        | Call(_, memberInfo, _, _, _) when memberInfo.IsPropertyGetterMethod -> Some memberInfo.DisplayName
        | _ -> None

    let private nameIsExpected (name: string) =
        name.StartsWith("expected", StringComparison.Ordinal)
        || name.StartsWith("_expected", StringComparison.Ordinal)
        || name.StartsWith("Expected", StringComparison.Ordinal)

    let private nameIsActual (name: string) =
        name.StartsWith("actual", StringComparison.Ordinal)

    let private swappedArguments expected actual =
        if isConstant expected then
            false
        elif isConstant actual then
            true
        else
            let expectedName = argumentName expected
            let actualName = argumentName actual
            (actualName |> Option.exists nameIsExpected) && not (expectedName |> Option.exists nameIsExpected)
            || (expectedName |> Option.exists nameIsActual) && not (actualName |> Option.exists nameIsActual)

    let analyze (context: RuleContext) =
        let discoverInternals = context.DiscoverInternals
        let report code resourcePrefix diagnosticRange (arguments: (obj | null) array) =
            context.Report code (resourcePrefix + "Title") (resourcePrefix + "MessageFormat") diagnosticRange arguments

        let checkIgnore name diagnosticRange (attributes: seq<FSharpAttribute>) =
            attributes
            |> Seq.filter (fun attribute -> not attribute.IsUnresolved && isAttribute "IgnoreAttribute" attribute.AttributeType)
            |> Seq.iter (fun attribute ->
                if not (hasJustification attribute) then
                    report "MSTEST0066" "IgnoreShouldHaveJustification" diagnosticRange [| box name |])

        let walker =
            { new TypedTreeCollectorBase() with
                override _.WalkEntity entity _ =
                    if hasAttribute "TestClassAttribute" entity.Attributes then
                        checkIgnore entity.DisplayName entity.DeclarationLocation entity.Attributes

                    for methodInfo in entity.MembersFunctionsAndValues do
                        if hasAttribute "TestMethodAttribute" methodInfo.Attributes then
                            if not (validMethod discoverInternals entity methodInfo) then
                                report "MSTEST0003" "TestMethodShouldBeValid" methodInfo.DeclarationLocation [| box methodInfo.DisplayName |]

                            checkIgnore methodInfo.DisplayName methodInfo.DeclarationLocation methodInfo.Attributes

                override _.WalkCall _ methodInfo _ _ arguments callRange =
                    if (methodInfo.DeclaringEntity |> Option.exists (hasTypeName (frameworkNamespace + "Assert")))
                       && List.contains methodInfo.CompiledName [ "AreEqual"; "AreNotEqual"; "AreSame"; "AreNotSame" ] then
                        let parameters = methodInfo.CurriedParameterGroups |> Seq.collect id |> Seq.toList

                        if parameters.Length = arguments.Length then
                            let namedArguments = List.zip parameters arguments
                            let expected =
                                namedArguments
                                |> List.tryPick (fun (parameter, argument) ->
                                    if parameter.Name = Some "expected" || parameter.Name = Some "notExpected" then Some argument else None)
                            let actual =
                                namedArguments
                                |> List.tryPick (fun (parameter, argument) ->
                                    if parameter.Name = Some "actual" then Some argument else None)

                            match expected, actual with
                            | Some expected, Some actual when swappedArguments expected actual ->
                                report "MSTEST0017" "AssertionArgsShouldBePassedInCorrectOrder" callRange [||]
                            | _ -> ()
            }

        context.TypedTree |> Option.iter (walkTast walker)

/// F# compiler-service entry points for MSTest diagnostics.
[<RequireQualifiedAccess>]
module Analyzers =
    let private analyze (context: RuleContext) (diagnostics: ResizeArray<Message>) =
        if context.TypedTree.IsSome then
            Analysis.analyze context
            DataRules.analyze context
            DependencyRules.analyze context
            DeclarationRules.analyze context
            ExpressionRules.analyze context
        diagnostics |> Seq.toList

    /// Reports MSTest diagnostics using the CLI's fully checked implementation tree.
    [<CliAnalyzer("MSTest", "MSTest usage diagnostics for F#.", "https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/overview")>]
    let analyzeCli: Analyzer<CliContext> =
        fun context -> async {
            let ruleContext, diagnostics = Rules.create context.FileName context.SourceText context.ParseFileResults (Some context.CheckFileResults) (Some context.CheckProjectResults) context.ProjectOptions context.TypedTree
            return analyze ruleContext diagnostics
        }

    /// Reports MSTest diagnostics when the editor has a checked implementation tree.
    [<EditorAnalyzer("MSTest", "MSTest usage diagnostics for F#.", "https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/overview")>]
    let analyzeEditor: Analyzer<EditorContext> =
        fun context -> async {
            let ruleContext, diagnostics = Rules.create context.FileName context.SourceText context.ParseFileResults context.CheckFileResults context.CheckProjectResults context.ProjectOptions context.TypedTree
            return analyze ruleContext diagnostics
        }
