// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.FSharp

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text

module internal DependencyRules =
    let private visible context (entity: FSharpEntity) =
        let rec loop (entity: FSharpEntity) =
            (entity.Accessibility.IsPublic || (context.DiscoverInternals <> Some false && entity.Accessibility.IsInternal))
            && entity.GenericParameters.Count = 0
            && (entity.DeclaringEntity |> Option.forall loop)
        loop entity

    let private runnable context (entity: FSharpEntity) =
        Symbols.isTestClass entity && visible context entity && not entity.IsAbstractClass && not entity.IsFSharpModule

    let private hasBody (m: FSharpMemberOrFunctionOrValue) =
        not m.IsDispatchSlot
        || (m.DeclaringEntity |> Option.exists (fun owner ->
            owner.MembersFunctionsAndValues |> Seq.exists (fun implementation ->
                implementation.IsOverrideOrExplicitInterfaceImplementation
                && (implementation.ImplementedAbstractSignatures |> Seq.exists (fun slot ->
                    slot.Name = m.LogicalName
                    && slot.DeclaringType.HasTypeDefinition && slot.DeclaringType.TypeDefinition.Equals(owner)
                    && slot.MethodGenericParameters.Count = m.GenericParameters.Count
                    && (slot.AbstractArguments |> Seq.collect id |> Seq.map (fun p -> p.Type.Format(FSharpDisplayContext.Empty)) |> Seq.toList)
                       = (Symbols.parameters m |> List.map (fun p -> p.Type.Format(FSharpDisplayContext.Empty))))))))

    let private methodRunnable context (m: FSharpMemberOrFunctionOrValue) =
        Symbols.isTest m && m.IsInstanceMemberInCompiledCode && hasBody m
        && (m.Accessibility.IsPublic || (context.DiscoverInternals <> Some false && m.Accessibility.IsInternal))

    let private signature (m: FSharpMemberOrFunctionOrValue) =
        m.CompiledName, m.GenericParameters.Count, (Symbols.parameters m |> List.map (fun p -> p.Type.Format(FSharpDisplayContext.Empty)))

    let private effectiveMembers (entity: FSharpEntity) =
        let seen = HashSet<_>()
        let rec collect (entity: FSharpEntity) =
            seq {
                for memberInfo in entity.MembersFunctionsAndValues do
                    if not memberInfo.IsPropertyAccessor && not memberInfo.IsConstructor
                       && (memberInfo.Accessibility.IsPrivate || seen.Add(signature memberInfo)) then yield memberInfo
                match entity.BaseType with
                | Some parent when parent.HasTypeDefinition -> yield! collect parent.TypeDefinition
                | _ -> ()
            }
        collect entity |> Seq.toList

    let private readTarget (attribute: FSharpAttribute) =
        if Symbols.positional attribute |> List.exists (fun value -> value = null) then None
        else
            let target = Symbols.positional attribute |> List.tryPick (function :? FSharpType as t -> Some t | _ -> None)
            let name = Symbols.positional attribute |> List.tryPick (function :? string as name -> Some name | _ -> None)
            if target.IsNone && name.IsNone then None else Some (target, name)

    let rec private canonicalAttribute (entity: FSharpEntity) =
        let names = [ "TestClassAttribute"; "TestMethodAttribute"; "TestInitializeAttribute"; "TestCleanupAttribute"; "ClassInitializeAttribute"; "ClassCleanupAttribute" ]
        if names |> List.exists (fun name -> Symbols.hasTypeName (Symbols.frameworkNamespace + name) entity) then Some entity
        else entity.BaseType |> Option.bind (fun t -> if t.HasTypeDefinition then canonicalAttribute t.TypeDefinition else None)

    let private validReturn (m: FSharpMemberOrFunctionOrValue) =
        [ "Microsoft.FSharp.Core.Unit"; "System.Void"; "System.Threading.Tasks.Task"; "System.Threading.Tasks.ValueTask" ]
        |> List.exists (fun name -> Symbols.typeName m.ReturnParameter.Type = Some name)

    let private genericMethod (m: FSharpMemberOrFunctionOrValue) =
        m.GenericParameters
        |> Seq.exists (fun parameter ->
            m.DeclaringEntity
            |> Option.forall (fun owner -> owner.GenericParameters |> Seq.forall (fun parent -> not (parent.Equals(parameter)))))

    let private sameRuntimeSignature (left: FSharpMemberOrFunctionOrValue) (right: FSharpMemberOrFunctionOrValue) =
        let returnType (m: FSharpMemberOrFunctionOrValue) =
            match Symbols.typeName m.ReturnParameter.Type with
            | Some "Microsoft.FSharp.Core.Unit" | Some "System.Void" -> "System.Void"
            | _ -> m.ReturnParameter.Type.Format(FSharpDisplayContext.Empty)
        left.CompiledName = right.CompiledName
        && left.IsInstanceMemberInCompiledCode = right.IsInstanceMemberInCompiledCode
        && left.GenericParameters.Count = right.GenericParameters.Count
        && returnType left = returnType right
        && (Symbols.compiledParameters left |> List.map (fun p -> p.Type.Format(FSharpDisplayContext.Empty)))
           = (Symbols.compiledParameters right |> List.map (fun p -> p.Type.Format(FSharpDisplayContext.Empty)))

    let rec private inheritableAttribute (entity: FSharpEntity) =
        match entity.Attributes |> Seq.tryFind (fun attribute ->
            not attribute.IsUnresolved && Symbols.hasTypeName "System.AttributeUsageAttribute" attribute.AttributeType) with
        | Some usage ->
            match Symbols.named "Inherited" usage with
            | Some (:? bool as inherited) -> inherited
            | _ -> true
        | None -> entity.BaseType |> Option.exists (fun t -> t.HasTypeDefinition && inheritableAttribute t.TypeDefinition)

    let private wouldRun context (testClass: FSharpEntity) (parent: FSharpEntity) framework name (attribute: FSharpAttribute) (m: FSharpMemberOrFunctionOrValue) =
        let parameters = Symbols.compiledParameters m
        let instanceFixture = name = "TestInitializeAttribute" || name = "TestCleanupAttribute"
        let classFixture = name = "ClassInitializeAttribute" || name = "ClassCleanupAttribute"
        let testContext (parameter: FSharpParameter) =
            Symbols.typeName parameter.Type = Some (Symbols.frameworkNamespace + "TestContext")
            && parameter.Type.HasTypeDefinition
            && ([ "MSTest.TestFramework"; "MSTest.TestFramework.Extensions"; "Microsoft.VisualStudio.TestPlatform.TestFramework"; "Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions" ]
                |> List.contains parameter.Type.TypeDefinition.Assembly.SimpleName)
        let signatureValid =
            m.Accessibility.IsPublic && validReturn m && hasBody m
            && not m.IsProperty && not m.IsPropertyAccessor && not m.IsConstructor
            && (if classFixture then
                    not m.IsInstanceMemberInCompiledCode && not (genericMethod m)
                    && ((name = "ClassCleanupAttribute" && parameters.IsEmpty)
                        || (match parameters with | [ parameter ] -> testContext parameter | _ -> false))
                    && (attribute.ConstructorArguments |> Seq.exists (fun (t, value) ->
                        Symbols.typeName t = Some (Symbols.frameworkNamespace + "InheritanceBehavior") && Object.Equals(value, 1)))
                else
                    m.IsInstanceMemberInCompiledCode
                    && (not instanceFixture || (parameters.IsEmpty && not (genericMethod m))))
        let currentAttribute (candidate: FSharpMemberOrFunctionOrValue) =
            candidate.Attributes |> Seq.exists (fun attribute ->
                not attribute.IsUnresolved
                && (canonicalAttribute attribute.AttributeType |> Option.exists (fun canonical ->
                    canonical.DisplayName = name && canonical.Assembly.SimpleName = framework)))
        let rec hidden (entity: FSharpEntity) =
            if entity.Equals(parent) then false
            else
                let decision =
                    entity.MembersFunctionsAndValues |> Seq.tryPick (fun candidate ->
                        if candidate.CompiledName <> m.CompiledName || candidate.IsProperty || candidate.IsPropertyAccessor then None
                        elif candidate.IsOverrideOrExplicitInterfaceImplementation && sameRuntimeSignature candidate m then
                            Some (not (inheritableAttribute attribute.AttributeType) || currentAttribute candidate)
                        elif instanceFixture
                             && candidate.Accessibility.IsPublic && candidate.IsInstanceMemberInCompiledCode
                             && (Symbols.compiledParameters candidate).IsEmpty && validReturn candidate && hasBody candidate then Some true
                        elif not instanceFixture
                             && methodRunnable context candidate && validReturn candidate && currentAttribute candidate
                             && sameRuntimeSignature candidate m then Some true
                        else None)
                decision
                |> Option.defaultWith (fun () -> entity.BaseType |> Option.exists (fun t -> t.HasTypeDefinition && hidden t.TypeDefinition))
        signatureValid && (classFixture || not (hidden testClass))

    type private DependencyDiagnostic =
        {
            Range: range
            Suffix: string
            Arguments: (obj | null) array
        }

    let private dependencyDiagnostics = ConditionalWeakTable<FSharpCheckProjectResults, Lazy<DependencyDiagnostic list>>()

    let private collectDependencyDiagnostics (context: RuleContext) (project: FSharpCheckProjectResults) =
        let diagnostics = ResizeArray<DependencyDiagnostic>()
        let entities = Symbols.allEntities project.AssemblySignature.Entities |> Seq.toList
        let currentAssembly = entities |> List.tryHead |> Option.map (fun e -> e.Assembly.SimpleName)
        let belongsToCurrent (entity: FSharpEntity) = currentAssembly = Some entity.Assembly.SimpleName
        let memberCache = Dictionary<FSharpEntity, FSharpMemberOrFunctionOrValue list>()
        let members entity =
            match memberCache.TryGetValue(entity) with
            | true, members -> members
            | _ ->
                let members = effectiveMembers entity
                memberCache[entity] <- members
                members
        let selfReferences = HashSet<_>()
        let report (attribute: FSharpAttribute) suffix args =
            diagnostics.Add { Range = attribute.Range; Suffix = suffix; Arguments = args }
        let validate (entity: FSharpEntity) (methodInfo: FSharpMemberOrFunctionOrValue option) attributes =
            for attribute in Symbols.attributes "DependsOnAttribute" attributes do
                if methodInfo |> Option.exists (Symbols.isTest >> not) then
                    report attribute "NoEffect" [| box methodInfo.Value.DisplayName |]
                elif methodInfo.IsNone && not (runnable context entity) then
                    report attribute "NoEffectOnClass" [| box entity.DisplayName |]
                else
                    match readTarget attribute with
                    | Some (targetType, targetName) when targetType.IsSome || runnable context entity ->
                        let target =
                            match targetType with
                            | None -> Some entity
                            | Some t when t.HasTypeDefinition && not t.TypeDefinition.IsArrayType -> Some t.TypeDefinition
                            | _ -> None
                        match target with
                        | None -> report attribute "NotATestClass" [| box (targetType.Value.Format(FSharpDisplayContext.Empty)) |]
                        | Some target ->
                            if not (belongsToCurrent target) then report attribute "OtherAssembly" [| box target.DisplayName |]
                            elif not (runnable context target) then
                                report attribute (if target.IsAbstractClass && Symbols.isTestClass target then "AbstractTarget" else "NotATestClass") [| box target.DisplayName |]
                            else
                                match targetName with
                                | Some name when not (String.IsNullOrWhiteSpace(name)) ->
                                    let candidates = members target |> List.filter (fun m -> m.DisplayName = name || m.CompiledName = name)
                                    if candidates.IsEmpty then report attribute "MethodNotFound" [| box target.DisplayName; box name |]
                                    elif not (candidates |> List.exists Symbols.isTest) then report attribute "NotATestMethod" [| box target.DisplayName; box name |]
                                    elif methodInfo |> Option.exists (fun m -> m.DisplayName = name && entity.Equals(target)) then
                                        selfReferences.Add(attribute.Range) |> ignore
                                        report attribute "SelfReference" [| box name |]
                                | _ -> ()
                    | _ -> ()
        for entity in entities do
            validate entity None entity.Attributes
            for memberInfo in entity.MembersFunctionsAndValues do validate entity (Some memberInfo) memberInfo.Attributes
        let nodes =
            entities |> List.filter (runnable context)
            |> List.collect (fun entity -> members entity |> List.filter (methodRunnable context) |> List.map (fun m -> entity, m.DisplayName))
            |> List.distinctBy (fun (e, name) -> e.TryGetFullName(), name)
        let key (e: FSharpEntity, name) = (e.TryGetFullName() |> Option.defaultValue e.DisplayName) + "." + name
        let duplicates = Dictionary<FSharpEntity, bool>()
        for entity in nodes |> List.map fst |> List.distinct do
            let seen = HashSet<_>()
            let rec check (entity: FSharpEntity) =
                (entity.MembersFunctionsAndValues
                 |> Seq.exists (fun m -> methodRunnable context m && not m.IsOverrideOrExplicitInterfaceImplementation && not (seen.Add(signature m))))
                || (entity.BaseType |> Option.exists (fun t -> t.HasTypeDefinition && check t.TypeDefinition))
            duplicates[entity] <- check entity
        let nodesByOwner = nodes |> List.groupBy fst |> dict
        let edges (entity: FSharpEntity, methodName) =
            let applications =
                [ yield! Symbols.attributes "DependsOnAttribute" entity.Attributes |> List.map (fun a -> a, true)
                  for m in members entity do
                      if m.DisplayName = methodName && methodRunnable context m then
                          yield! Symbols.attributes "DependsOnAttribute" m.Attributes |> List.map (fun a -> a, false) ]
            (if duplicates[entity] then [] else applications) |> List.collect (fun (attribute, classLevel) ->
                match readTarget attribute with
                | Some (targetType, targetName) ->
                    let target = match targetType with | None -> Some entity | Some t when t.HasTypeDefinition -> Some t.TypeDefinition | _ -> None
                    match target with
                    | Some target when belongsToCurrent target && runnable context target ->
                        match nodesByOwner.TryGetValue(target) with
                        | true, candidates ->
                            candidates |> List.choose (fun (owner, name) ->
                                if (targetName.IsNone || targetName = Some name)
                                   && not ((classLevel || targetName.IsNone) && owner.Equals(entity) && name = methodName) then Some ((owner, name), attribute) else None)
                        | _ -> []
                    | _ -> []
                | _ -> [])
        let graph = nodes |> List.map (fun node -> node, edges node) |> dict
        for start in nodes do
            let visited = HashSet<string>()
            let rec findCycle node path =
                if visited.Count >= 512 then None
                else
                    graph[node] |> List.tryPick (fun (target, attribute) ->
                        if key target = key start then Some (List.rev (target :: path), attribute)
                        elif visited.Add(key target) then findCycle target (target :: path) |> Option.map (fun (p, _) -> p, attribute)
                        else None)
            match findCycle start [] with
            | Some (path, attribute) when path.Length > 1 || not (selfReferences.Contains(attribute.Range)) ->
                report attribute "Cycle" [| box (String.Join(" > ", key start :: (path |> List.map key))) |]
            | _ -> ()
        diagnostics |> Seq.toList

    let analyze (context: RuleContext) =
        match context.CheckProjectResults with
        | None -> ()
        | Some project ->
            if context.Settings.TryFind("MSTEST0078") <> Some "none" then
                for diagnostic in dependencyDiagnostics.GetValue(project, fun project -> lazy (collectDependencyDiagnostics context project)).Value do
                    if diagnostic.Range.FileName = context.FileName then
                        context.Report "MSTEST0078" "DependsOnShouldBeValidTitle" ("DependsOnShouldBeValidMessageFormat_" + diagnostic.Suffix) diagnostic.Range diagnostic.Arguments
            let entities = Symbols.allEntities project.AssemblySignature.Entities |> Seq.toList
            for entity in entities do
                if entity.DeclarationLocation.FileName = context.FileName && runnable context entity then
                    let framework =
                        Symbols.attribute "TestClassAttribute" entity.Attributes
                        |> Option.bind (fun attribute -> canonicalAttribute attribute.AttributeType)
                        |> Option.map (fun attribute -> attribute.Assembly.SimpleName)
                    let rec bases (entity: FSharpEntity) =
                        seq {
                            match entity.BaseType with
                            | Some t when t.HasTypeDefinition ->
                                yield t.TypeDefinition
                                yield! bases t.TypeDefinition
                            | _ -> ()
                        }
                    for parent in bases entity do
                        if parent.Assembly.SimpleName <> entity.Assembly.SimpleName then
                            for methodInfo in parent.MembersFunctionsAndValues do
                                if methodInfo.Accessibility.IsPublic then
                                    for attribute in methodInfo.Attributes |> Seq.filter (fun attribute -> not attribute.IsUnresolved) do
                                        let canonical = canonicalAttribute attribute.AttributeType
                                        let name = canonical |> Option.map (fun entity -> entity.DisplayName) |> Option.defaultValue ""
                                        let attributeAssembly = canonical |> Option.map (fun entity -> entity.Assembly.SimpleName)
                                        let knownAssembly = attributeAssembly |> Option.exists (fun name -> [ "MSTest.TestFramework"; "MSTest.TestFramework.Extensions"; "Microsoft.VisualStudio.TestPlatform.TestFramework"; "Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions" ] |> List.contains name)
                                        if knownAssembly && framework.IsSome && attributeAssembly <> framework
                                           && name <> "TestClassAttribute"
                                           && wouldRun context entity parent framework.Value name attribute methodInfo then
                                            let appliedName = attribute.AttributeType.DisplayName
                                            let appliedName = if appliedName.EndsWith("Attribute", StringComparison.Ordinal) then appliedName.Substring(0, appliedName.Length - 9) else appliedName
                                            context.Report "MSTEST0082" "InheritedMemberFromDifferentMSTestVersionTitle" "InheritedMemberFromDifferentMSTestVersionMessageFormat" entity.DeclarationLocation
                                                [| box methodInfo.DisplayName; box parent.DisplayName; box appliedName; box attributeAssembly.Value |]
