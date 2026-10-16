// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.FSharp

open System
open System.Collections.Generic
open System.Collections.Immutable
open System.IO
open System.Reflection.Metadata
open System.Reflection.PortableExecutable
open FSharp.Analyzers.SDK.ASTCollecting
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

module internal DeclarationRules =
    let rec private metadataTypeDefinitionName (reader: MetadataReader) handle =
        let definition = reader.GetTypeDefinition(handle)
        let name = reader.GetString(definition.Name)
        let parent = definition.GetDeclaringType()
        if parent.IsNil then
            let namespaceName = reader.GetString(definition.Namespace)
            if namespaceName = "" then name else namespaceName + "." + name
        else metadataTypeDefinitionName reader parent + "+" + name

    let rec private metadataTypeReferenceName (reader: MetadataReader) handle =
        let reference = reader.GetTypeReference(handle)
        let name = reader.GetString(reference.Name)
        if reference.ResolutionScope.Kind = HandleKind.TypeReference then
            metadataTypeReferenceName reader (TypeReferenceHandle.op_Explicit reference.ResolutionScope) + "+" + name
        else
            let namespaceName = reader.GetString(reference.Namespace)
            if namespaceName = "" then name else namespaceName + "." + name

    type private SignatureNames() =
        interface ISignatureTypeProvider<string, unit> with
            member _.GetArrayType(element, shape) = element + "[" + String(',', shape.Rank - 1) + "]"
            member _.GetByReferenceType(element) = element + "&"
            member _.GetFunctionPointerType(_) = ""
            member _.GetGenericInstantiation(name, arguments) = name + "<" + String.Join(",", arguments) + ">"
            member _.GetGenericMethodParameter(_, index) = "!!" + string index
            member _.GetGenericTypeParameter(_, index) = "!" + string index
            member _.GetModifiedType(_, element, _) = element
            member _.GetPinnedType(element) = element
            member _.GetPointerType(element) = element + "*"
            member _.GetPrimitiveType(code) =
                match code with
                | PrimitiveTypeCode.Void -> "System.Void"
                | PrimitiveTypeCode.Boolean -> "System.Boolean"
                | PrimitiveTypeCode.Char -> "System.Char"
                | PrimitiveTypeCode.SByte -> "System.SByte"
                | PrimitiveTypeCode.Byte -> "System.Byte"
                | PrimitiveTypeCode.Int16 -> "System.Int16"
                | PrimitiveTypeCode.UInt16 -> "System.UInt16"
                | PrimitiveTypeCode.Int32 -> "System.Int32"
                | PrimitiveTypeCode.UInt32 -> "System.UInt32"
                | PrimitiveTypeCode.Int64 -> "System.Int64"
                | PrimitiveTypeCode.UInt64 -> "System.UInt64"
                | PrimitiveTypeCode.Single -> "System.Single"
                | PrimitiveTypeCode.Double -> "System.Double"
                | PrimitiveTypeCode.String -> "System.String"
                | PrimitiveTypeCode.Object -> "System.Object"
                | PrimitiveTypeCode.IntPtr -> "System.IntPtr"
                | PrimitiveTypeCode.UIntPtr -> "System.UIntPtr"
                | _ -> ""
            member _.GetSZArrayType(element) = element + "[]"
            member _.GetTypeFromDefinition(reader, handle, _) = metadataTypeDefinitionName reader handle
            member _.GetTypeFromReference(reader, handle, _) = metadataTypeReferenceName reader handle
            member this.GetTypeFromSpecification(reader, context, handle, _) =
                reader.GetTypeSpecification(handle).DecodeSignature(this, context)

    let private metadataAttributeName (reader: MetadataReader) handle =
        let attribute = reader.GetCustomAttribute(handle)
        match attribute.Constructor.Kind with
        | HandleKind.MemberReference ->
            let constructor = reader.GetMemberReference(MemberReferenceHandle.op_Explicit attribute.Constructor)
            match constructor.Parent.Kind with
            | HandleKind.TypeReference -> metadataTypeReferenceName reader (TypeReferenceHandle.op_Explicit constructor.Parent)
            | HandleKind.TypeDefinition -> metadataTypeDefinitionName reader (TypeDefinitionHandle.op_Explicit constructor.Parent)
            | _ -> ""
        | HandleKind.MethodDefinition ->
            let constructor = reader.GetMethodDefinition(MethodDefinitionHandle.op_Explicit attribute.Constructor)
            metadataTypeDefinitionName reader (constructor.GetDeclaringType())
        | _ -> ""

    let rec private signatureTypeName typeInfo =
        let typeInfo = Symbols.normalizeType typeInfo
        if not typeInfo.HasTypeDefinition then ""
        elif typeInfo.TypeDefinition.IsArrayType then
            signatureTypeName typeInfo.GenericArguments[0] + "[" + String(',', typeInfo.TypeDefinition.ArrayRank - 1) + "]"
        else
            let name = Symbols.typeName typeInfo |> Option.defaultValue ""
            if typeInfo.GenericArguments.Count = 0 then name
            else name + "<" + String.Join(",", typeInfo.GenericArguments |> Seq.map signatureTypeName) + ">"

    let private metadataParameterAttributes (constructor: FSharpMemberOrFunctionOrValue) =
        // FCS removes caller-info attributes from both imported and source parameter symbols.
        let parameters = Symbols.parameters constructor
        let expectedTypes = parameters |> List.map (fun parameter -> signatureTypeName parameter.Type)
        constructor.DeclaringEntity
        |> Option.bind (fun entity ->
            entity.Assembly.FileName
            |> Option.bind (fun file ->
                if not (File.Exists(file)) then None
                else
                    use stream = File.OpenRead(file)
                    use peReader = new PEReader(stream)
                    if not peReader.HasMetadata then None
                    else
                        let reader = peReader.GetMetadataReader()
                        reader.TypeDefinitions
                        |> Seq.tryFind (fun handle -> entity.TryGetFullName() = Some (metadataTypeDefinitionName reader handle))
                        |> Option.bind (fun handle ->
                            reader.GetTypeDefinition(handle).GetMethods()
                            |> Seq.tryPick (fun methodHandle ->
                                let definition = reader.GetMethodDefinition(methodHandle)
                                if reader.GetString(definition.Name) <> ".ctor" then None
                                else
                                    let signature = definition.DecodeSignature(SignatureNames(), ())
                                    if signature.ParameterTypes |> Seq.toList <> expectedTypes then None
                                    else
                                        definition.GetParameters()
                                        |> Seq.map (fun parameterHandle ->
                                            let parameter = reader.GetParameter(parameterHandle)
                                            int parameter.SequenceNumber - 1,
                                            parameter.GetCustomAttributes()
                                            |> Seq.map (metadataAttributeName reader)
                                            |> Set.ofSeq)
                                        |> Map.ofSeq
                                        |> Some))))
        |> Option.defaultValue Map.empty

    let private exactAttribute name (values: seq<FSharpAttribute>) =
        values
        |> Seq.tryFind (fun value ->
            not value.IsUnresolved
            && Symbols.hasTypeName (Symbols.frameworkNamespace + name) value.AttributeType)

    let private externalAttribute name (values: seq<FSharpAttribute>) =
        values
        |> Seq.exists (fun value ->
            not value.IsUnresolved
            && Symbols.hasTypeName name value.AttributeType)

    let private sealedType (entity: FSharpEntity) =
        externalAttribute "Microsoft.FSharp.Core.SealedAttribute" entity.Attributes
        || entity.IsValueType
        || entity.IsFSharpModule

    let private staticType (entity: FSharpEntity) =
        entity.IsFSharpModule
        || (entity.IsAbstractClass && sealedType entity)

    let rec private hierarchy (entity: FSharpEntity) =
        seq {
            yield entity
            match entity.BaseType with
            | Some parent when parent.HasTypeDefinition -> yield! hierarchy parent.TypeDefinition
            | _ -> ()
        }

    let private visible discoverInternals (accessibility: FSharpAccessibility) =
        accessibility.IsPublic || (discoverInternals <> Some false && accessibility.IsInternal)

    let rec private visibleType discoverInternals (entity: FSharpEntity) =
        visible discoverInternals entity.Accessibility
        && (entity.DeclaringEntity |> Option.forall (visibleType discoverInternals))

    let private ordinary (memberInfo: FSharpMemberOrFunctionOrValue) =
        not memberInfo.IsConstructor
        && not memberInfo.IsProperty
        && not memberInfo.IsPropertyAccessor
        && not memberInfo.IsCompilerGenerated

    let private methodParameters (memberInfo: FSharpMemberOrFunctionOrValue) =
        // FCS represents a unit argument even though the compiled CLI method is parameterless.
        match Symbols.parameters memberInfo with
        | [ parameter ] when parameter.Name.IsNone && Symbols.typeName parameter.Type = Some "Microsoft.FSharp.Core.Unit" -> []
        | parameters -> parameters

    let private methodGenericParameters (entity: FSharpEntity) (memberInfo: FSharpMemberOrFunctionOrValue) =
        memberInfo.GenericParameters
        |> Seq.filter (fun parameter -> not (entity.GenericParameters |> Seq.exists (fun parent -> parent.Equals(parameter))))
        |> Seq.toList

    let private unitReturn (memberInfo: FSharpMemberOrFunctionOrValue) =
        List.contains (Symbols.typeName memberInfo.ReturnParameter.Type) [ Some "Microsoft.FSharp.Core.Unit"; Some "System.Void" ]

    let private validReturn (memberInfo: FSharpMemberOrFunctionOrValue) =
        unitReturn memberInfo
        || List.contains (Symbols.typeName memberInfo.ReturnParameter.Type) [ Some "System.Threading.Tasks.Task"; Some "System.Threading.Tasks.ValueTask" ]

    let private testContextType typeInfo =
        Symbols.typeName typeInfo = Some (Symbols.frameworkNamespace + "TestContext")

    let private hasDefaultImplementation (entity: FSharpEntity) (memberInfo: FSharpMemberOrFunctionOrValue) =
        let sameSignature (signature: FSharpAbstractSignature) =
            let parameters = methodParameters memberInfo
            let arguments =
                signature.AbstractArguments
                |> Seq.collect id
                |> Seq.filter (fun parameter -> Symbols.typeName parameter.Type <> Some "Microsoft.FSharp.Core.Unit")
                |> Seq.toList
            signature.DeclaringType.HasTypeDefinition
            && signature.DeclaringType.TypeDefinition.Equals(entity)
            && (signature.Name = memberInfo.LogicalName || signature.Name = memberInfo.CompiledName)
            && parameters.Length = arguments.Length
            && List.forall2 (fun (left: FSharpParameter) (right: FSharpAbstractParameter) -> left.Type.Equals(right.Type)) parameters arguments
        not memberInfo.IsDispatchSlot
        || (Symbols.members entity
            |> List.exists (fun implementation ->
                implementation.IsOverrideOrExplicitInterfaceImplementation
                && (implementation.ImplementedAbstractSignatures |> Seq.exists sameSignature)))

    let private validFixture discoverInternals (entity: FSharpEntity) (memberInfo: FSharpMemberOrFunctionOrValue) isStatic allowGeneric requireClass parameterMode =
        let parameters = methodParameters memberInfo
        let correctParameters =
            match parameterMode, parameters with
            | 0, [] -> true
            | 1, [ parameter ]
            | 2, [ parameter ] -> testContextType parameter.Type
            | 2, [] -> true
            | _ -> false
        entity.IsClass
        && ordinary memberInfo
        && (allowGeneric || entity.GenericParameters.Count = 0)
        && ((not requireClass && not (sealedType entity)) || Symbols.hasAttribute "TestClassAttribute" entity.Attributes)
        && (methodGenericParameters entity memberInfo).IsEmpty
        && memberInfo.IsInstanceMemberInCompiledCode <> isStatic
        && hasDefaultImplementation entity memberInfo
        && memberInfo.Accessibility.IsPublic
        && visibleType discoverInternals entity
        && correctParameters
        && validReturn memberInfo

    let rec private unwrap expression =
        match expression with
        | Coerce(_, inner) -> unwrap inner
        | _ -> expression

    let private number (value: obj | null) =
        match value with
        | :? int as value -> Some value
        | :? int64 as value -> Some (int value)
        | _ -> None

    let private namedNumber name attribute =
        Symbols.named name attribute |> Option.bind number |> Option.defaultValue 0

    let private namedBoolean name attribute =
        match Symbols.named name attribute with
        | Some (:? bool as value) -> value
        | _ -> false

    let rec private equivalentValue (left: obj | null) (right: obj | null) =
        match left, right with
        | (:? FSharpType as left), (:? FSharpType as right) -> left.Equals(right)
        | (:? System.Collections.IEnumerable as left), (:? System.Collections.IEnumerable as right) when not (left :? string) && not (right :? string) ->
            let left = left |> Seq.cast<obj | null> |> Seq.toList
            let right = right |> Seq.cast<obj | null> |> Seq.toList
            left.Length = right.Length && List.forall2 equivalentValue left right
        | _ -> Object.Equals(left, right)

    let private equivalentArguments (left: FSharpAttribute) (right: FSharpAttribute) =
        let leftArguments = Symbols.positional left
        let rightArguments = Symbols.positional right
        leftArguments.Length = rightArguments.Length
        && List.forall2 equivalentValue leftArguments rightArguments
        && left.NamedArguments.Count = right.NamedArguments.Count
        && (left.NamedArguments
            |> Seq.forall (fun (_, name, _, value) ->
                Symbols.named name right |> Option.exists (equivalentValue value)))

    let private conditionMessage attribute =
        match Symbols.named "IgnoreMessage" attribute with
        | None -> true
        | Some (:? string as text) -> text.Length > 0
        | _ -> false

    let private redundantConditionMessage methodAttribute classAttribute =
        conditionMessage classAttribute || not (conditionMessage methodAttribute)

    let private allowedFlags allValues unknown (attribute: FSharpAttribute) =
        match Symbols.positional attribute |> List.map number with
        | [ Some flags ] -> Some (flags &&& allValues, false)
        | [ Some mode; Some flags ] ->
            Some ((if mode = 0 then flags &&& allValues else allValues &&& (~~~flags)), mode <> 0 && unknown)
        | _ -> None

    let private ignoreMessage attribute =
        match Symbols.named "IgnoreMessage" attribute with
        | Some value -> value
        | None -> Symbols.positional attribute |> List.tryHead |> Option.defaultValue (box "")

    let private deploymentItem attribute =
        match Symbols.positional attribute with
        | [ (:? string as path) ] when not (String.IsNullOrWhiteSpace(path)) -> Some (path.TrimEnd('/', '\\'), "")
        | [ (:? string as path); (:? string as output) ] when not (String.IsNullOrWhiteSpace(path)) -> Some (path.TrimEnd('/', '\\'), output)
        | _ -> None

    let private dependency attribute =
        let proceed = namedBoolean "ProceedOnFailure" attribute
        match Symbols.positional attribute with
        | [ (:? string as methodName) ] -> Some (None, Some methodName, proceed)
        | [ (:? FSharpType as typeInfo) ] -> Some (Some typeInfo, None, proceed)
        | [ (:? FSharpType as typeInfo); (:? string as methodName) ] -> Some (Some typeInfo, Some methodName, proceed)
        | _ -> None

    let private redundantAttribute (entity: FSharpEntity) (memberInfo: FSharpMemberOrFunctionOrValue) (attribute: FSharpAttribute) =
        let direct name = exactAttribute name entity.Attributes
        let inherited name =
            hierarchy entity
            |> Seq.collect (fun parent -> parent.Attributes)
            |> Seq.filter (fun value ->
                not value.IsUnresolved
                && Symbols.hasTypeName (Symbols.frameworkNamespace + name) value.AttributeType)
        let kind =
            if attribute.IsUnresolved then ""
            else attribute.AttributeType.TryGetFullName() |> Option.defaultValue ""
        match kind with
        | "Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelizeAttribute" ->
            inherited "DoNotParallelizeAttribute" |> Seq.isEmpty |> not
        | "Microsoft.VisualStudio.TestTools.UnitTesting.TestCategoryAttribute"
        | "Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute" ->
            inherited attribute.AttributeType.DisplayName |> Seq.exists (equivalentArguments attribute)
        | "Microsoft.VisualStudio.TestTools.UnitTesting.DeploymentItemAttribute" ->
            match deploymentItem attribute with
            | Some (path, output) ->
                inherited "DeploymentItemAttribute"
                |> Seq.exists (fun other ->
                    deploymentItem other
                    |> Option.exists (fun (otherPath, otherOutput) ->
                        String.Equals(path, otherPath, StringComparison.OrdinalIgnoreCase)
                        && String.Equals(output, otherOutput, StringComparison.OrdinalIgnoreCase)))
            | _ -> false
        | "Microsoft.VisualStudio.TestTools.UnitTesting.ResourceLockAttribute" ->
            match Symbols.positional attribute with
            | [ (:? string as resource) ] when List.contains (namedNumber "Mode" attribute) [ 0; 1 ] ->
                inherited "ResourceLockAttribute"
                |> Seq.exists (fun other ->
                    Symbols.positional other = [ box resource ]
                    && (namedNumber "Mode" other = 0 || namedNumber "Mode" other = namedNumber "Mode" attribute))
            | _ -> false
        | "Microsoft.VisualStudio.TestTools.UnitTesting.OSConditionAttribute"
        | "Microsoft.VisualStudio.TestTools.UnitTesting.ArchitectureConditionAttribute" when sealedType entity ->
            let isOS = kind.EndsWith(".OSConditionAttribute", StringComparison.Ordinal)
            let allValues = if isOS then 15 else 1023
            match allowedFlags allValues isOS attribute, direct attribute.AttributeType.DisplayName with
            | Some (allowed, unknown), Some other ->
                allowedFlags allValues isOS other
                |> Option.exists (fun (classAllowed, classUnknown) ->
                    (classAllowed &&& (~~~allowed)) = 0
                    && (not classUnknown || unknown)
                    && redundantConditionMessage attribute other)
            | _ -> false
        | "Microsoft.VisualStudio.TestTools.UnitTesting.CIConditionAttribute" when sealedType entity ->
            direct "CIConditionAttribute"
            |> Option.exists (fun other ->
                match Symbols.positional attribute |> List.map number, Symbols.positional other |> List.map number with
                | [ Some methodMode ], [ Some classMode ] ->
                    (methodMode = 0) = (classMode = 0) && redundantConditionMessage attribute other
                | _ -> false)
        | "Microsoft.VisualStudio.TestTools.UnitTesting.RetryAttribute" when sealedType entity ->
            direct "RetryAttribute"
            |> Option.exists (fun other ->
                Symbols.positional attribute = Symbols.positional other
                && namedNumber "MillisecondsDelayBetweenRetries" attribute = namedNumber "MillisecondsDelayBetweenRetries" other
                && namedNumber "BackoffType" attribute = namedNumber "BackoffType" other)
        | "Microsoft.VisualStudio.TestTools.UnitTesting.IgnoreAttribute" when sealedType entity ->
            direct "IgnoreAttribute"
            |> Option.exists (fun other ->
                match ignoreMessage other with
                | :? string as message when message.Length > 0 -> true
                | classMessage -> equivalentValue (ignoreMessage attribute) classMessage)
        | "Microsoft.VisualStudio.TestTools.UnitTesting.DependsOnAttribute" when sealedType entity ->
            match dependency attribute with
            | Some (targetType, targetMethod, proceed) ->
                entity.Attributes
                |> Seq.filter (fun other -> not other.IsUnresolved && Symbols.hasTypeName kind other.AttributeType)
                |> Seq.exists (fun other ->
                    dependency other
                    |> Option.exists (fun (classType, classMethod, classProceed) ->
                        let self =
                            classMethod = Some memberInfo.CompiledName
                            && (classType |> Option.forall (fun typeInfo -> typeInfo.HasTypeDefinition && typeInfo.TypeDefinition.Equals(entity)))
                        not self
                        && targetType = classType
                        && targetMethod = classMethod
                        && (not classProceed || proceed)))
            | _ -> false
        | _ -> false

    let analyze (context: RuleContext) : unit =
        let report id prefix diagnosticRange arguments =
            context.Report id (prefix + "Title") (prefix + "MessageFormat") diagnosticRange arguments
        let sameFile (name: string) =
            not (String.IsNullOrWhiteSpace(name))
            && String.Equals(Path.GetFullPath(name), Path.GetFullPath(context.FileName), StringComparison.OrdinalIgnoreCase)
        let sourceRange (attribute: FSharpAttribute) fallback =
            if sameFile attribute.Range.FileName then attribute.Range else fallback
        let isSourceFile (fileName: string) =
            fileName.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)
            && not (fileName.Contains(string Path.DirectorySeparatorChar + "obj" + string Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            && not (fileName.Contains(".AssemblyInfo.", StringComparison.OrdinalIgnoreCase))
        let firstSourceFile =
            context.ProjectOptions.SourceFiles |> Seq.tryFind isSourceFile
        let assemblyFile = firstSourceFile |> Option.exists sameFile
        let assemblyRange = Range.mkRange context.FileName (Position.mkPos 1 0) (Position.mkPos 1 1)
        let settingTrue name =
            context.Settings.TryFind name
            |> Option.exists (fun text -> String.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
        let metadata key (values: seq<FSharpAttribute>) =
            values
            |> Seq.tryPick (fun value ->
                if not value.IsUnresolved && Symbols.hasTypeName "System.Reflection.AssemblyMetadataAttribute" value.AttributeType then
                    match Symbols.positional value with
                    | [ (:? string as name); (:? string as text) ] when name = key -> Some text
                    | _ -> None
                else None)

        let parallelizeAttribute = exactAttribute "ParallelizeAttribute" context.AssemblyAttributes
        let notParallel = exactAttribute "DoNotParallelizeAttribute" context.AssemblyAttributes
        match parallelizeAttribute, notParallel with
        | Some parallelizeAttribute, Some notParallel ->
            for attribute in [ parallelizeAttribute; notParallel ] do
                if sameFile attribute.Range.FileName then
                    report "MSTEST0059" "DoNotUseParallelizeAndDoNotParallelizeTogether" attribute.Range [||]
        | None, None when assemblyFile && settingTrue "IsMSTestTestAdapterReferenced" ->
            report "MSTEST0001" "UseParallelizeAttributeAnalyzer" assemblyRange [||]
        | _ -> ()

        let hostInjectionLocations = ResizeArray<range>()
        let hostCollector =
            { new TypedTreeCollectorBase() with
                override _.WalkCall _ methodInfo _ _ _ callRange =
                    if methodInfo.CompiledName = "AddMSTestTestClassInjection"
                       && (methodInfo.DeclaringEntity
                           |> Option.exists (fun entity ->
                               Symbols.hasTypeName "Microsoft.Extensions.DependencyInjection.MSTestHostingServiceCollectionExtensions" entity
                               && entity.Assembly.SimpleName = "MSTest.Extensions.Hosting"))
                       && (methodParameters methodInfo |> List.map (fun parameter -> Symbols.typeName parameter.Type)) =
                           [ Some "Microsoft.Extensions.DependencyInjection.IServiceCollection" ]
                       && Symbols.typeName methodInfo.ReturnParameter.Type = Some "Microsoft.Extensions.DependencyInjection.IServiceCollection" then
                        hostInjectionLocations.Add(callRange)
            }
        match context.CheckProjectResults with
        | Some project when not project.HasCriticalErrors ->
            for file in project.AssemblyContents.ImplementationFiles do
                walkTast hostCollector file
        | _ -> context.TypedTree |> Option.iter (walkTast hostCollector)
        let referencedInjection =
            context.CheckFileResults
            |> Option.exists (fun checkedFile ->
                checkedFile.ProjectContext.GetReferencedAssemblies()
                |> Seq.exists (fun assembly ->
                    metadata "MSTestHostTestClassInjectionEnabled" assembly.Contents.Attributes = Some "true"))
        let hostInjection =
            hostInjectionLocations.Count > 0
            || referencedInjection
            || settingTrue "MSTestHostTestClassInjectionEnabled"
        let unsupportedMode =
            metadata "MSTestHostTestClassInjectionUnsupportedMode" context.AssemblyAttributes
            |> Option.orElseWith (fun () -> context.Settings.TryFind "MSTestHostTestClassInjectionUnsupportedMode")
            |> Option.orElseWith (fun () ->
                [ "PublishAot"; "RunAOTCompilation"; "EnableMSTestSourceGeneration" ] |> List.tryFind settingTrue)
            |> Option.orElseWith (fun () ->
                if context.Settings.TryFind "TargetPlatformIdentifier" = Some "browser"
                   || (context.Settings.TryFind "RuntimeIdentifier" |> Option.exists (fun value -> value.Contains("browser-wasm", StringComparison.Ordinal))) then
                    Some "browser-wasm"
                else None)
        if hostInjection then
            unsupportedMode
            |> Option.iter (fun mode ->
                let currentLocations = hostInjectionLocations |> Seq.filter (fun location -> sameFile location.FileName) |> Seq.toList
                if not currentLocations.IsEmpty then
                    for location in currentLocations do
                        report "MSTEST0088" "MSTestHostTestClassInjectionNotSupported" location [| box mode |]
                elif hostInjectionLocations.Count = 0 && assemblyFile then
                    report "MSTEST0088" "MSTestHostTestClassInjectionNotSupported" assemblyRange [| box mode |])

        let syntaxAttributes = ResizeArray<SynAttribute>()
        let parameterSyntaxAttributes = ResizeArray<range * SynAttribute list>()
        let collectParameterAttributes diagnosticRange attributes =
            parameterSyntaxAttributes.Add(diagnosticRange, attributes |> List.collect (fun (attributes: SynAttributeList) -> attributes.Attributes))
        let syntaxCollector =
            { new SyntaxCollectorBase() with
                override _.WalkAttribute(_, attribute) = syntaxAttributes.Add(attribute)
                override _.WalkBinding(_, SynBinding(attributes = attributes)) =
                    for attributeList in attributes do
                        for attribute in attributeList.Attributes do
                            syntaxAttributes.Add(attribute)
                override _.WalkPat(_, pattern) =
                    match pattern with
                    | SynPat.Attrib(attributes = attributes; range = diagnosticRange) ->
                        collectParameterAttributes diagnosticRange attributes
                    | _ -> ()
                override _.WalkSimplePat(_, pattern) =
                    match pattern with
                    | SynSimplePat.Attrib(attributes = attributes; range = diagnosticRange) ->
                        collectParameterAttributes diagnosticRange attributes
                    | _ -> ()
            }
        walkAst syntaxCollector context.ParseFileResults.ParseTree

        let containsRange (outer: range) (inner: range) =
            sameFile inner.FileName
            && (outer.StartLine, outer.StartColumn) <= (inner.StartLine, inner.StartColumn)
            && (outer.EndLine, outer.EndColumn) >= (inner.EndLine, inner.EndColumn)
        let syntaxAttributeTypeName (attribute: SynAttribute) =
            let names = attribute.TypeName.LongIdent |> List.map (fun identifier -> identifier.idText)
            let location = attribute.TypeName.Range
            context.CheckFileResults
            |> Option.bind (fun checkedFile ->
                checkedFile.GetSymbolUseAtLocation(location.EndLine, location.EndColumn, context.SourceText.GetLineString(location.EndLine - 1), names))
            |> Option.bind (fun symbolUse ->
                match symbolUse.Symbol with
                | :? FSharpEntity as entity -> entity.TryGetFullName()
                | :? FSharpMemberOrFunctionOrValue as constructor -> constructor.DeclaringEntity |> Option.bind (fun entity -> entity.TryGetFullName())
                | _ -> None)
        let metadataParameters = Dictionary<FSharpMemberOrFunctionOrValue, Map<int, Set<string>>>()
        let parameterHasAttribute constructor index (parameter: FSharpParameter) name =
            let importedParameterAttribute () =
                if sameFile parameter.DeclarationLocation.FileName then false
                else
                    let attributes =
                        match metadataParameters.TryGetValue(constructor) with
                        | true, attributes -> attributes
                        | _ ->
                            let attributes = metadataParameterAttributes constructor
                            metadataParameters[constructor] <- attributes
                            attributes
                    attributes |> Map.tryFind index |> Option.exists (Set.contains name)
            externalAttribute name parameter.Attributes
            || (sameFile parameter.DeclarationLocation.FileName
                && (parameterSyntaxAttributes
                    |> Seq.exists (fun (location, attributes) ->
                        containsRange location parameter.DeclarationLocation
                        && (attributes |> List.exists (fun attribute -> syntaxAttributeTypeName attribute = Some name)))))
            || importedParameterAttribute ()

        let rec syntaxArguments expression =
            match expression with
            | SynExpr.Paren(expr = inner) -> syntaxArguments inner
            | SynExpr.Tuple(exprs = arguments) -> arguments
            | SynExpr.Const(constant = SynConst.Unit) -> []
            | _ -> [ expression ]
        let textAt diagnosticRange = context.SourceText.GetSubTextFromRange(diagnosticRange)
        let explicitAttributeArgument (attribute: FSharpAttribute) index parameterName =
            syntaxAttributes
            |> Seq.tryFind (fun syntax ->
                syntax.TypeName.Range.StartLine = attribute.Range.StartLine
                && syntax.Range.StartColumn <= attribute.Range.StartColumn
                && (syntax.Range.EndLine > attribute.Range.EndLine || syntax.Range.EndColumn >= attribute.Range.EndColumn))
            |> Option.bind (fun syntax ->
                let arguments = syntaxArguments syntax.ArgExpr
                let named =
                    arguments
                    |> List.tryFind (fun argument ->
                        let text = textAt argument.Range |> fun text -> text.TrimStart()
                        text.StartsWith(parameterName, StringComparison.Ordinal)
                        && text.Substring(parameterName.Length).TrimStart().StartsWith("=", StringComparison.Ordinal))
                named
                |> Option.orElseWith (fun () ->
                    arguments
                    |> List.filter (fun argument ->
                        match argument with
                        | SynExpr.App _ -> false
                        | _ -> true)
                    |> List.tryItem index))

        let memberKey (memberInfo: FSharpMemberOrFunctionOrValue) =
            let declaringType =
                memberInfo.DeclaringEntity
                |> Option.bind (fun entity -> entity.TryGetFullName())
                |> Option.defaultValue ""
            declaringType, memberInfo.CompiledName, memberInfo.DeclarationLocation
        // Signature members and implementation declarations have different FCS symbol identities.
        let bodies = Dictionary<string * string * range, FSharpMemberOrFunctionOrValue list list * FSharpExpr>()
        let parameterValues = HashSet<FSharpMemberOrFunctionOrValue>()
        let bodyCollector =
            { new TypedTreeCollectorBase() with
                override _.WalkMemberOrFunctionOrValue memberInfo arguments body =
                    bodies[memberKey memberInfo] <- arguments, body
                    for argument in arguments |> List.collect id do
                        parameterValues.Add(argument) |> ignore
            }
        context.TypedTree |> Option.iter (walkTast bodyCollector)

        let fromContextParameter expression =
            match unwrap expression with
            | Value value -> parameterValues.Contains(value) && testContextType value.FullType
            | _ -> false
        let rec expressions (expression: FSharpExpr) =
            seq {
                yield expression
                for child in expression.ImmediateSubExpressions do
                    yield! expressions child
            }
        let returnedField body =
            let rec returned expression =
                match unwrap expression with
                | FSharpFieldGet(_, _, field) -> Some field
                | Sequential(_, last) -> returned last
                | Let((_, _, _), last) -> returned last
                | _ -> None
            returned body
        let contextAssigned (entity: FSharpEntity) (property: FSharpMemberOrFunctionOrValue) =
            let field =
                if property.HasGetterMethod then
                    match bodies.TryGetValue(memberKey property.GetterMethod) with
                    | true, (_, body) -> returnedField body
                    | _ -> None
                else None
            Symbols.members entity
            |> List.filter (fun constructor ->
                constructor.IsConstructor
                && (match methodParameters constructor with | [ parameter ] -> testContextType parameter.Type | _ -> false))
            |> List.exists (fun constructor ->
                match bodies.TryGetValue(memberKey constructor) with
                | true, (_, body) ->
                    expressions body
                    |> Seq.exists (fun expression ->
                        match expression with
                        | FSharpFieldSet(_, _, target, value) -> field = Some target && fromContextParameter value
                        | Call(_, setter, _, _, [ value ]) ->
                            property.HasSetterMethod && setter.Equals(property.SetterMethod) && fromContextParameter value
                        | _ -> false)
                | _ -> false)

        let checkAttributes isTestClass isAbstract diagnosticRange (values: seq<FSharpAttribute>) =
            if not isTestClass then
                Symbols.attribute "ConditionBaseAttribute" values
                |> Option.iter (fun attribute ->
                    report "MSTEST0041" "UseConditionBaseWithTestClass" diagnosticRange [| box attribute.AttributeType.DisplayName |])
            if not isTestClass && not isAbstract && (exactAttribute "DeploymentItemAttribute" values).IsSome then
                report "MSTEST0035" "UseDeploymentItemWithTestMethodOrTestClass" diagnosticRange [||]
            if not isTestClass && Symbols.hasAttribute "RetryBaseAttribute" values then
                report "MSTEST0043" "UseRetryWithTestMethod" diagnosticRange [||]

        let checkMethod (entity: FSharpEntity) (memberInfo: FSharpMemberOrFunctionOrValue) =
            if ordinary memberInfo then
                let location = memberInfo.DeclarationLocation
                let attributes = memberInfo.Attributes |> Seq.filter (fun attribute -> not attribute.IsUnresolved) |> Seq.toList
                let test = Symbols.isTest memberInfo
                if not test then
                    for attribute in attributes do
                        let matching =
                            [ "OwnerAttribute"; "PriorityAttribute"; "DescriptionAttribute"; "TestPropertyAttribute"; "WorkItemAttribute"; "ConditionBaseAttribute" ]
                            |> List.tryFind (fun name -> Symbols.inherits (Symbols.frameworkNamespace + name) attribute.AttributeType)
                        matching
                        |> Option.iter (fun name ->
                            let name =
                                if name = "ConditionBaseAttribute" then attribute.AttributeType.DisplayName
                                else name.Substring(0, name.Length - "Attribute".Length)
                            report "MSTEST0007" "UseAttributeOnTestMethodAnalyzer" (sourceRange attribute location) [| box name |])
                if test && (exactAttribute "IgnoreAttribute" attributes).IsSome then
                    context.Report "MSTEST0015" "TestMethodShouldNotBeIgnoredAnalyzerTitle" "TestMethodShouldNotBeIgnoredAnalyzerFormat" location [| box memberInfo.DisplayName |]
                if test && externalAttribute "System.ComponentModel.DescriptionAttribute" attributes then
                    report "MSTEST0031" "DoNotUseSystemDescriptionAttribute" location [| box memberInfo.DisplayName |]
                if not test && (exactAttribute "DeploymentItemAttribute" attributes).IsSome then
                    report "MSTEST0035" "UseDeploymentItemWithTestMethodOrTestClass" location [||]
                if not test && Symbols.hasAttribute "RetryBaseAttribute" attributes then
                    report "MSTEST0043" "UseRetryWithTestMethod" location [||]
                exactAttribute "DataTestMethodAttribute" attributes
                |> Option.iter (fun attribute -> report "MSTEST0044" "PreferTestMethodOverDataTestMethodAnalyzer" (sourceRange attribute location) [||])
                exactAttribute "TimeoutAttribute" attributes
                |> Option.iter (fun attribute ->
                    if not (namedBoolean "CooperativeCancellation" attribute) then
                        report "MSTEST0045" "UseCooperativeCancellationForTimeout" (sourceRange attribute location) [||])

                let fixtureRules =
                    [ "TestInitializeAttribute", "MSTEST0008", "TestInitializeShouldBeValid", false, true, false, 0
                      "TestCleanupAttribute", "MSTEST0009", "TestCleanupShouldBeValid", false, true, false, 0
                      "ClassInitializeAttribute", "MSTEST0010", "ClassInitializeShouldBeValid", true, false, false, 1
                      "ClassCleanupAttribute", "MSTEST0011", "ClassCleanupShouldBeValid", true, false, false, 2
                      "AssemblyInitializeAttribute", "MSTEST0012", "AssemblyInitializeShouldBeValid", true, false, true, 1
                      "AssemblyCleanupAttribute", "MSTEST0013", "AssemblyCleanupShouldBeValid", true, false, true, 2
                      "GlobalTestInitializeAttribute", "MSTEST0050", "GlobalTestFixtureShouldBeValid", true, false, true, 1
                      "GlobalTestCleanupAttribute", "MSTEST0050", "GlobalTestFixtureShouldBeValid", true, false, true, 1 ]
                for name, id, prefix, isStatic, allowGeneric, requireClass, mode in fixtureRules do
                    exactAttribute name attributes
                    |> Option.iter (fun attribute ->
                        let classFixture = id = "MSTEST0010" || id = "MSTEST0011"
                        let inheritance =
                            classFixture
                            && (attribute.ConstructorArguments
                                |> Seq.exists (fun (typeInfo, value) ->
                                    Symbols.typeName typeInfo = Some (Symbols.frameworkNamespace + "InheritanceBehavior")
                                    && (number value |> Option.exists ((<>) 0))))
                        let discoverInternals = if id = "MSTEST0050" then Some false else context.DiscoverInternals
                        if not (validFixture discoverInternals entity memberInfo isStatic (allowGeneric || inheritance) requireClass mode)
                           || (classFixture && ((entity.IsAbstractClass && not inheritance) || (sealedType entity && inheritance))) then
                            report id prefix location [| box memberInfo.DisplayName |])
                if (exactAttribute "TestInitializeAttribute" attributes).IsSome && unitReturn memberInfo then
                    report "MSTEST0020" "PreferConstructorOverTestInitialize" location [||]
                if (exactAttribute "TestCleanupAttribute" attributes).IsSome then
                    report "MSTEST0021" "PreferDisposeOverTestCleanup" location [||]
                if Symbols.isTestClass entity
                   && (memberInfo.ImplementedAbstractSignatures
                       |> Seq.exists (fun signature ->
                           signature.DeclaringType.HasTypeDefinition
                           && ((Symbols.hasTypeName "System.IDisposable" signature.DeclaringType.TypeDefinition && signature.Name = "Dispose")
                               || (Symbols.hasTypeName "System.IAsyncDisposable" signature.DeclaringType.TypeDefinition && signature.Name = "DisposeAsync")))) then
                    report "MSTEST0022" "PreferTestCleanupOverDispose" location [||]

                if Symbols.isTestClass entity
                   && memberInfo.Accessibility.IsPublic
                   && visibleType (Some false) entity
                   && memberInfo.IsInstanceMemberInCompiledCode
                   && not memberInfo.IsDispatchSlot
                   && not memberInfo.IsOverrideOrExplicitInterfaceImplementation
                   && (methodGenericParameters entity memberInfo).IsEmpty
                   && validReturn memberInfo
                   && not test
                   && not (Symbols.hasAttribute "TestInitializeAttribute" attributes)
                   && not (Symbols.hasAttribute "TestCleanupAttribute" attributes) then
                    context.Report "MSTEST0029" "PublicMethodShouldBeTestMethodAnalyzerTitle" "PublicMethodShouldBeTestMethodAnalyzerFormat" location [| box memberInfo.DisplayName |]

                let testAttributes = Symbols.attributes "TestMethodAttribute" attributes
                if testAttributes.Length > 1 then
                    report "MSTEST0060" "DuplicateTestMethodAttribute" location [| box memberInfo.DisplayName |]
                if test then
                    if methodParameters memberInfo
                       |> List.exists (fun parameter ->
                           let typeInfo = Symbols.normalizeType parameter.Type
                           parameter.IsOutArg || (typeInfo.HasTypeDefinition && typeInfo.TypeDefinition.IsByRef && not parameter.IsInArg)) then
                        report "MSTEST0062" "AvoidOutRefTestMethodParameters" location [| box memberInfo.DisplayName |]
                    for attribute in testAttributes do
                        if Symbols.named "DisplayName" attribute = Some (box memberInfo.CompiledName) then
                            report "MSTEST0071" "RedundantTestMethodDisplayName" (sourceRange attribute location) [| box memberInfo.CompiledName |]
                    if Symbols.hasAttribute "TestClassAttribute" entity.Attributes then
                        for attribute in attributes do
                            if redundantAttribute entity memberInfo attribute then
                                let name = attribute.AttributeType.DisplayName
                                let name = if name.EndsWith("Attribute", StringComparison.Ordinal) then name.Substring(0, name.Length - 9) else name
                                report "MSTEST0086" "RedundantTestMethodAttribute" (sourceRange attribute location) [| box ("[" + name + "]"); box memberInfo.DisplayName |]

                for attribute in testAttributes do
                    let constructors = Symbols.members attribute.AttributeType |> List.filter (fun value -> value.IsConstructor)
                    for constructor in constructors do
                        let parameters = methodParameters constructor
                        if parameters.Length = attribute.ConstructorArguments.Count then
                            for index, parameter in List.indexed parameters do
                                if parameter.IsOptionalArg
                                   && Symbols.typeName parameter.Type = Some "System.String"
                                   && parameterHasAttribute constructor index parameter "System.Runtime.CompilerServices.CallerFilePathAttribute" then
                                    explicitAttributeArgument attribute index (parameter.Name |> Option.defaultValue "callerFilePath")
                                    |> Option.iter (fun argument ->
                                        report "MSTEST0056" "TestMethodAttributeShouldSetDisplayNameCorrectly" argument.Range [||])

        let checkEntity (entity: FSharpEntity) =
            if not entity.IsNamespace then
                let location = entity.DeclarationLocation
                let directClass = Symbols.hasAttribute "TestClassAttribute" entity.Attributes
                let isClass = Symbols.isTestClass entity
                let members = Symbols.members entity
                let inheritedMembers = hierarchy entity |> Seq.collect Symbols.members |> Seq.toList
                let hasInstanceFixtures =
                    members
                    |> List.exists (fun memberInfo ->
                        Symbols.isTest memberInfo
                        || ([ "TestInitializeAttribute"; "TestCleanupAttribute"; "ClassInitializeAttribute"; "ClassCleanupAttribute" ]
                            |> List.exists (fun name -> Symbols.hasAttribute name memberInfo.Attributes)))
                if directClass && (entity.IsClass || entity.IsFSharpModule)
                   && (not (visibleType context.DiscoverInternals entity)
                       || (staticType entity && hasInstanceFixtures)) then
                    report "MSTEST0002" "TestClassShouldBeValid" location [| box entity.DisplayName |]
                if entity.IsClass && not (staticType entity) && not entity.IsAbstractClass
                   && visibleType (Some false) entity && not directClass then
                    report "MSTEST0004" "PublicTypeShouldBeTestClass" location [| box entity.DisplayName |]
                if isClass then
                    for property in members |> List.filter (fun value -> value.IsProperty) do
                        if testContextType property.ReturnParameter.Type
                           && String.Equals(property.DisplayName, "TestContext", StringComparison.OrdinalIgnoreCase)
                           && (not property.IsInstanceMemberInCompiledCode
                               || not ((property.DisplayName = "TestContext" && property.Accessibility.IsPublic && property.HasSetterMethod)
                                       || contextAssigned entity property)) then
                            report "MSTEST0005" "TestContextShouldBeValid" property.DeclarationLocation [||]
                    if not (inheritedMembers |> List.exists Symbols.isTest)
                       && (not (staticType entity)
                           || not (inheritedMembers |> List.exists (fun memberInfo ->
                               [ "AssemblyInitializeAttribute"; "AssemblyCleanupAttribute"; "GlobalTestInitializeAttribute"; "GlobalTestCleanupAttribute" ]
                               |> List.exists (fun name -> Symbols.hasAttribute name memberInfo.Attributes)))) then
                        report "MSTEST0016" "TestClassShouldHaveTestMethod" location [| box entity.DisplayName |]
                    for constructor in members |> List.filter (fun value -> value.IsConstructor && methodParameters value = [] && not value.IsCompilerGenerated) do
                        report "MSTEST0019" "PreferTestInitializeOverConstructor" constructor.DeclarationLocation [||]
                if (entity.IsClass || entity.IsValueType || entity.IsFSharpModule)
                   && not entity.IsAbstractClass && not isClass
                   && (inheritedMembers |> List.exists Symbols.isTest) then
                    report "MSTEST0030" "TypeContainingTestMethodShouldBeATestClass" location [| box entity.DisplayName |]
                checkAttributes directClass entity.IsAbstractClass location entity.Attributes
                if directClass then
                    let baseMembers = hierarchy entity |> Seq.skip 1 |> Seq.collect Symbols.members |> Seq.toList
                    for memberInfo in members |> List.filter (fun value -> not value.IsPropertyAccessor && not value.IsConstructor && not value.IsOverrideOrExplicitInterfaceImplementation && not value.IsCompilerGenerated) do
                        let hidden =
                            baseMembers |> List.exists (fun parent ->
                                not parent.IsOverrideOrExplicitInterfaceImplementation
                                && not parent.Accessibility.IsPrivate
                                && parent.CompiledName = memberInfo.CompiledName
                                && parent.IsProperty = memberInfo.IsProperty
                                && (if memberInfo.IsProperty then parent.ReturnParameter.Type.Equals(memberInfo.ReturnParameter.Type)
                                    else
                                        ordinary parent
                                        && parent.GenericParameters.Count = memberInfo.GenericParameters.Count
                                        && (methodParameters parent |> List.map (fun parameter -> parameter.Type)) = (methodParameters memberInfo |> List.map (fun parameter -> parameter.Type))))
                        if hidden then
                            report "MSTEST0036" "DoNotUseShadowing" memberInfo.DeclarationLocation [| box memberInfo.DisplayName |]
                if entity.BaseType |> Option.exists (fun parent -> Symbols.typeName parent = Some (Symbols.frameworkNamespace + "DataTestMethodAttribute")) then
                    report "MSTEST0044" "PreferTestMethodOverDataTestMethodAnalyzer" location [||]
                if Symbols.inherits (Symbols.frameworkNamespace + "TestMethodAttribute") entity
                   && not (Symbols.hasTypeName (Symbols.frameworkNamespace + "TestMethodAttribute") entity) then
                    for constructor in members |> List.filter (fun value -> value.IsConstructor) do
                        let parameters = methodParameters constructor
                        let hasCallerInfo name =
                            parameters
                            |> List.indexed
                            |> List.exists (fun (index, parameter) -> parameterHasAttribute constructor index parameter name)
                        if not (hasCallerInfo "System.Runtime.CompilerServices.CallerFilePathAttribute")
                           || not (hasCallerInfo "System.Runtime.CompilerServices.CallerLineNumberAttribute") then
                            report "MSTEST0057" "TestMethodAttributeShouldPropagateSourceInformation" constructor.DeclarationLocation [| box entity.DisplayName |]
                if entity.IsClass && isClass && not entity.IsAbstractClass && not (staticType entity) then
                    let publicConstructors =
                        members |> List.filter (fun value -> value.IsConstructor && value.Accessibility.IsPublic)
                    let validConstructor =
                        publicConstructors
                        |> List.exists (fun constructor ->
                            hostInjection
                            || (match methodParameters constructor with
                                | [] -> true
                                | [ parameter ] -> testContextType parameter.Type
                                | _ -> false))
                    if not validConstructor then
                        report "MSTEST0063" "TestClassConstructorShouldBeValid" location [| box entity.DisplayName |]
                    elif hostInjection then
                        let preferred =
                            publicConstructors
                            |> List.filter (fun constructor -> externalAttribute "Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructorAttribute" constructor.Attributes)
                        if preferred.Length > 1 then
                            context.Report "MSTEST0063" "TestClassConstructorShouldBeValidTitle" "TestClassConstructorHasMultipleActivatorUtilitiesConstructorsMessageFormat" location [| box entity.DisplayName |]
                        else
                            publicConstructors |> List.collect methodParameters
                            |> List.tryFind (fun parameter ->
                                let typeInfo = Symbols.normalizeType parameter.Type
                                typeInfo.HasTypeDefinition && not (testContextType typeInfo)
                                && Symbols.inherits (Symbols.frameworkNamespace + "TestContext") typeInfo.TypeDefinition)
                            |> Option.iter (fun parameter ->
                                context.Report "MSTEST0063" "TestClassConstructorShouldBeValidTitle" "TestClassConstructorHasDerivedTestContextMessageFormat" location [| box entity.DisplayName; box (Symbols.typeName parameter.Type |> Option.defaultValue parameter.Type.TypeDefinition.DisplayName) |])
                if entity.IsClass && entity.IsAbstractClass && not (staticType entity) && directClass then
                    report "MSTEST0085" "TestClassAttributeShouldNotBeAppliedToAbstractClass" location [| box entity.DisplayName |]
                for memberInfo in members do
                    checkMethod entity memberInfo

        let walker =
            { new TypedTreeCollectorBase() with
                override _.WalkEntity entity _ = checkEntity entity
                override _.WalkFSharpFieldSet _ _ field value =
                    if field.IsStatic && fromContextParameter value then
                        report "MSTEST0024" "DoNotStoreStaticTestContextAnalyzer" value.Range [||]
                override _.WalkValueSet target value =
                    if target.IsModuleValueOrMember && not target.IsInstanceMemberInCompiledCode && fromContextParameter value then
                        report "MSTEST0024" "DoNotStoreStaticTestContextAnalyzer" value.Range [||]
                override _.WalkCall instance methodInfo _ _ arguments callRange =
                    if instance.IsNone && methodInfo.IsPropertySetterMethod
                       && (arguments |> List.tryLast |> Option.exists fromContextParameter) then
                        report "MSTEST0024" "DoNotStoreStaticTestContextAnalyzer" callRange [||]
                override _.WalkMemberOrFunctionOrValue _ _ body =
                    for expression in expressions body do
                        match expression with
                        | NewObject(constructor, _, arguments) when
                            constructor.DeclaringEntity |> Option.exists (Symbols.inherits (Symbols.frameworkNamespace + "TestMethodAttribute")) ->
                            let parameters = methodParameters constructor
                            if parameters.Length = arguments.Length then
                                for index, (parameter, argument) in List.indexed (List.zip parameters arguments) do
                                    if parameter.IsOptionalArg
                                       && Symbols.typeName parameter.Type = Some "System.String"
                                       && parameterHasAttribute constructor index parameter "System.Runtime.CompilerServices.CallerFilePathAttribute" then
                                        match unwrap argument with
                                        | Call(_, property, _, _, _) when
                                            property.IsPropertyGetterMethod
                                            && (List.contains property.DisplayName [ "DeclaringFilePath"; "DeclaringLineNumber" ]
                                                || List.contains property.CompiledName [ "get_DeclaringFilePath"; "get_DeclaringLineNumber" ])
                                            && (property.DeclaringEntity |> Option.exists (Symbols.inherits (Symbols.frameworkNamespace + "TestMethodAttribute"))) -> ()
                                        | _ ->
                                            // Compiler-inserted caller information has the enclosing expression's range.
                                            if argument.Range <> expression.Range then
                                                report "MSTEST0056" "TestMethodAttributeShouldSetDisplayNameCorrectly" argument.Range [||]
                        | _ -> ()
            }
        context.TypedTree |> Option.iter (walkTast walker)
