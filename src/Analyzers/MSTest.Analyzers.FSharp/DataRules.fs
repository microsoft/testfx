// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.FSharp

open System
open System.Collections.Generic
open FSharp.Analyzers.SDK.ASTCollecting
open FSharp.Analyzers.SDK.TASTCollecting
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

module internal DataRules =
    let private isArray (t: FSharpType) =
        let t = Symbols.normalizeType t
        t.HasTypeDefinition && t.TypeDefinition.IsArrayType

    let private values (value: obj | null) : (obj | null) list =
        match value with
        | :? (obj array) as values -> values |> Array.toList
        | :? System.Collections.IEnumerable as values when not (value :? string) -> values |> Seq.cast<obj | null> |> Seq.toList
        | _ -> [ value ]

    let private typeArgument (attribute: FSharpAttribute) =
        attribute.ConstructorArguments |> Seq.tryPick (fun (_, value) -> match value with | null -> None | :? FSharpType as t -> Some t | _ -> None)

    let private integer (value: obj | null) =
        match value with
        | :? int as number -> Some number
        | _ -> None

    let private stringValue (value: obj | null) =
        match value with
        | :? string as text -> Some text
        | _ -> None

    let private location (context: RuleContext) fallback (attribute: FSharpAttribute) =
        if attribute.Range.FileName = context.FileName then attribute.Range else fallback

    let private entityOfType (t: FSharpType) =
        let t = Symbols.normalizeType t
        if t.HasTypeDefinition then Some t.TypeDefinition else None

    let rec private hierarchy (entity: FSharpEntity) =
        seq {
            yield entity
            match entity.BaseType |> Option.bind entityOfType with
            | Some parent -> yield! hierarchy parent
            | None -> ()
        }

    type private Member =
        | Method of FSharpMemberOrFunctionOrValue
        | Field of FSharpField

    let private findMembers flatten (entity: FSharpEntity) name =
        hierarchy entity
        |> Seq.map (fun owner ->
            [
                for m in owner.MembersFunctionsAndValues do
                    if (m.DisplayName = name || m.CompiledName = name)
                       && not m.IsPropertyAccessor
                       && (owner.Equals(entity) || not m.Accessibility.IsPrivate) then yield Method m
                for field in owner.FSharpFields do
                    if field.Name = name && (owner.Equals(entity) || not field.Accessibility.IsPrivate) then yield Field field
            ])
        |> fun members ->
            if flatten then members |> Seq.collect id |> Seq.toList
            else members |> Seq.tryFind (List.isEmpty >> not) |> Option.defaultValue []

    let private memberType = function Method m -> m.ReturnParameter.Type | Field field -> field.FieldType
    let private memberPublic = function Method m -> m.Accessibility.IsPublic | Field field -> field.Accessibility.IsPublic
    let private memberStatic = function Method m -> not m.IsInstanceMemberInCompiledCode | Field field -> field.IsStatic
    let private memberParameters = function Method m -> Symbols.compiledParameters m | Field _ -> []

    let private inheritedInterface name (t: FSharpType) =
        match entityOfType t with
        | Some entity -> Symbols.hasTypeName name entity || (entity.AllInterfaces |> Seq.exists (fun i -> Symbols.typeName i = Some name))
        | None -> false

    let private displayType (t: FSharpType) = t.Format(FSharpDisplayContext.Empty)

    type private ArgumentType =
        | SymbolType of FSharpType
        | RuntimeType of Type
        | ArrayType of ArgumentType * int

    type private Argument =
        {
            Value: obj | null
            Type: ArgumentType option
            Elements: Argument list
        }

    let rec private symbolType (t: FSharpType) =
        let t = Symbols.normalizeType t
        if isArray t then ArrayType(symbolType t.GenericArguments[0], t.TypeDefinition.ArrayRank) else SymbolType t

    let rec private runtimeType (t: Type) =
        if t.IsArray then ArrayType(runtimeType (t.GetElementType() |> Option.ofObj |> Option.get), t.GetArrayRank())
        else RuntimeType t

    let rec private typeName = function
        | SymbolType t -> Symbols.typeName t
        | RuntimeType t -> Option.ofObj t.FullName
        | ArrayType(element, rank) -> typeName element |> Option.map (fun name -> name + "[" + String(',', rank - 1) + "]")

    let private argumentTypeName argument = argument.Type |> Option.bind typeName

    let rec private sameType left right =
        match left, right with
        | SymbolType left, SymbolType right -> left.Equals(right)
        | RuntimeType left, RuntimeType right -> left = right
        | ArrayType(left, leftRank), ArrayType(right, rightRank) -> leftRank = rightRank && sameType left right
        | SymbolType symbol, RuntimeType runtime
        | RuntimeType runtime, SymbolType symbol when runtime.IsGenericType ->
            let arguments = runtime.GetGenericArguments()
            symbol.HasTypeDefinition
            && symbol.TypeDefinition.TryGetFullName() = Option.ofObj (runtime.GetGenericTypeDefinition().FullName)
            && symbol.GenericArguments.Count = arguments.Length
            && Seq.forall2 (fun symbol runtime -> sameType (symbolType symbol) (runtimeType runtime)) symbol.GenericArguments arguments
        | _ -> typeName left = typeName right && (typeName left).IsSome

    let private referenceType = function
        | SymbolType t -> t.HasTypeDefinition && not t.TypeDefinition.IsValueType
        | RuntimeType t -> not t.IsValueType
        | ArrayType _ -> true

    let private numericConversion actual expected =
        let targets =
            match actual with
            | "System.SByte" -> [ "System.Int16"; "System.Int32"; "System.Int64"; "System.Single"; "System.Double"; "System.Decimal" ]
            | "System.Byte" -> [ "System.Int16"; "System.UInt16"; "System.Int32"; "System.UInt32"; "System.Int64"; "System.UInt64"; "System.Single"; "System.Double"; "System.Decimal" ]
            | "System.Int16" -> [ "System.Int32"; "System.Int64"; "System.Single"; "System.Double"; "System.Decimal" ]
            | "System.UInt16"
            | "System.Char" -> [ "System.Int32"; "System.UInt32"; "System.Int64"; "System.UInt64"; "System.Single"; "System.Double"; "System.Decimal"; if actual = "System.Char" then "System.UInt16" ]
            | "System.Int32" -> [ "System.Int64"; "System.Single"; "System.Double"; "System.Decimal" ]
            | "System.UInt32" -> [ "System.Int64"; "System.UInt64"; "System.Single"; "System.Double"; "System.Decimal" ]
            | "System.Int64"
            | "System.UInt64" -> [ "System.Single"; "System.Double"; "System.Decimal" ]
            | "System.Single" -> [ "System.Double" ]
            | _ -> []
        List.contains expected targets

    let rec private assignable allowNumeric allowBoxing actual expected =
        let covariant left right =
            sameType left right
            || (referenceType left && referenceType right && assignable false false left right)
        sameType actual expected
        || (allowNumeric && numericConversion (typeName actual |> Option.defaultValue "") (typeName expected |> Option.defaultValue ""))
        || match actual, expected with
           | _, _ when typeName expected = Some "System.Object" -> allowBoxing || referenceType actual
           | ArrayType(left, leftRank), ArrayType(right, rightRank) -> leftRank = rightRank && covariant left right
           | ArrayType(element, rank), SymbolType target ->
               match Symbols.typeName target with
               | Some name when List.contains name
                   [ "System.Array"; "System.Collections.IEnumerable"; "System.Collections.ICollection"; "System.Collections.IList";
                     "System.Collections.IStructuralComparable"; "System.Collections.IStructuralEquatable" ] -> true
               | Some name when rank = 1 && target.GenericArguments.Count = 1
                                && List.contains name
                                    [ "System.Collections.Generic.IEnumerable`1"; "System.Collections.Generic.ICollection`1"; "System.Collections.Generic.IList`1";
                                      "System.Collections.Generic.IReadOnlyCollection`1"; "System.Collections.Generic.IReadOnlyList`1" ] ->
                   covariant element (symbolType target.GenericArguments[0])
               | _ -> false
           | SymbolType source, _ when source.HasTypeDefinition ->
               let source = source.TypeDefinition
               (allowBoxing || not source.IsValueType || not (referenceType expected))
               && ((source.BaseType |> Option.exists (fun parent -> assignable false allowBoxing (symbolType parent) expected))
                   // FCS already includes transitive interfaces, including an interface itself.
                   || source.AllInterfaces |> Seq.exists (fun parent -> sameType (symbolType parent) expected))
           | RuntimeType source, RuntimeType target ->
               (allowBoxing || not source.IsValueType || target.IsValueType) && target.IsAssignableFrom(source)
           | RuntimeType source, _ ->
               (allowBoxing || not source.IsValueType || not (referenceType expected))
               && ((Option.ofObj source.BaseType |> Option.exists (fun parent -> assignable false allowBoxing (runtimeType parent) expected))
                   || source.GetInterfaces() |> Array.exists (fun parent -> assignable false allowBoxing (runtimeType parent) expected))
           | _ -> false

    let private compatible argument (expected: FSharpType) =
        let expected = Symbols.normalizeType expected
        let scalar =
            if Symbols.typeName expected = Some "System.Nullable`1" && expected.GenericArguments.Count = 1 then expected.GenericArguments[0]
            else expected
        argument.Value = null
        || expected.IsGenericParameter
        || (argument.Type |> Option.exists (fun actual -> assignable true true actual (symbolType scalar)))

    let private rowArguments (context: RuleContext) =
        let syntaxAttributes =
            lazy
                let attributes = ResizeArray<SynAttribute>()
                let collector =
                    { new SyntaxCollectorBase() with
                        override _.WalkAttribute(_, attribute) = attributes.Add(attribute)
                        override _.WalkBinding(_, SynBinding(attributes = lists)) =
                            for list in lists do
                                attributes.AddRange(list.Attributes)
                    }
                walkAst collector context.ParseFileResults.ParseTree
                attributes |> Seq.toList
        let rec unwrap = function
            | SynExpr.Paren(expr = expression)
            | SynExpr.Typed(expr = expression)
            | SynExpr.Upcast(expr = expression) -> unwrap expression
            | expression -> expression
        let rec constructorArguments = function
            | SynExpr.Paren(expr = expression) -> constructorArguments expression
            | SynExpr.Tuple(exprs = expressions) -> expressions
            | SynExpr.Const(constant = SynConst.Unit) -> []
            | expression -> [ expression ]
        let rec arrayElements expression =
            match unwrap expression with
            | SynExpr.ArrayOrList(exprs = expressions) -> expressions
            | SynExpr.ArrayOrListComputed(expr = body) -> arrayElements body
            | SynExpr.Sequential(expr1 = first; expr2 = rest) -> first :: arrayElements rest
            | SynExpr.Const(constant = SynConst.Unit) -> []
            | expression -> [ expression ]
        let symbolAt (at: range) (names: string list) =
            if names.IsEmpty then None
            else
                context.CheckFileResults
                |> Option.bind (fun checkedFile ->
                    checkedFile.GetSymbolUseAtLocation(at.EndLine, at.EndColumn, context.SourceText.GetLineString(at.EndLine - 1), names))
                |> Option.map (fun useInfo -> useInfo.Symbol)
        let rec annotationType = function
            | SynType.Paren(innerType = inner) -> annotationType inner
            | SynType.Array(rank = rank; elementType = element) -> annotationType element |> Option.map (fun element -> ArrayType(element, rank))
            | SynType.LongIdent identifiers ->
                symbolAt identifiers.Range (identifiers.LongIdent |> List.map (fun identifier -> identifier.idText))
                |> Option.bind (function :? FSharpEntity as entity -> Some (symbolType (entity.AsType())) | _ -> None)
            | SynType.App(typeName = name; typeArgs = [ element ]) ->
                annotationType name
                |> Option.bind (function
                    | ArrayType _ -> annotationType element |> Option.map (fun element -> ArrayType(element, 1))
                    | _ -> None)
            | _ -> None
        let rec expressionType declared expression =
            match expression with
            | SynExpr.Paren(expr = expression) -> expressionType declared expression
            | SynExpr.Typed(expr = inner; targetType = annotation)
            | SynExpr.Upcast(expr = inner; targetType = annotation) ->
                let annotated = annotationType annotation
                if not declared && (annotated |> Option.bind typeName) = Some "System.Object" then expressionType false inner
                else annotated |> Option.orElseWith (fun () -> expressionType declared inner)
            | SynExpr.App(funcExpr = functionExpr; argExpr = inner) ->
                let at, names =
                    match unwrap functionExpr with
                    | SynExpr.Ident identifier -> identifier.idRange, [ identifier.idText ]
                    | SynExpr.LongIdent(longDotId = identifiers) -> identifiers.Range, identifiers.LongIdent |> List.map (fun identifier -> identifier.idText)
                    | _ -> functionExpr.Range, []
                match symbolAt at names with
                | Some (:? FSharpMemberOrFunctionOrValue as m) when
                    m.CompiledName = "Box"
                    && (m.DeclaringEntity |> Option.exists (Symbols.hasTypeName "Microsoft.FSharp.Core.Operators")) ->
                    if declared then Some (RuntimeType typeof<obj>) else expressionType false inner
                | _ -> None
            | _ ->
                let names =
                    match expression with
                    | SynExpr.LongIdent(longDotId = identifiers) -> identifiers.LongIdent |> List.map (fun identifier -> identifier.idText)
                    | SynExpr.Ident identifier -> [ identifier.idText ]
                    | _ -> []
                symbolAt expression.Range names
                |> Option.bind (fun symbol ->
                    match symbol with
                    | :? FSharpField as field -> Some (symbolType field.FieldType)
                    | :? FSharpMemberOrFunctionOrValue as value when value.LiteralValue.IsSome -> Some (symbolType value.FullType)
                    | _ -> None)
        let rec argument sourceType syntax (value: obj | null) =
            let actualType =
                syntax |> Option.bind (expressionType false)
                |> Option.orElse (sourceType |> Option.filter (fun t -> typeName t <> Some "System.Object"))
            let expressions = syntax |> Option.map arrayElements |> Option.defaultValue []
            let elements =
                match value with
                | :? Array ->
                    let elementType = actualType |> Option.bind (function ArrayType(element, _) -> Some element | _ -> None)
                    values value |> List.mapi (fun index value -> argument elementType (expressions |> List.tryItem index) value)
                | _ -> []
            let actualType =
                match value with
                | :? Array as array ->
                    match actualType with
                    | Some (ArrayType _) -> actualType
                    | _ ->
                        let elementTypes =
                            elements |> List.mapi (fun index element ->
                                let expression = expressions |> List.tryItem index
                                expression |> Option.bind (expressionType true)
                                |> Option.orElse element.Type)
                            |> List.choose id
                        let elementType =
                            match elementTypes with
                            | first :: rest when rest |> List.forall (sameType first) -> first
                            | _ -> runtimeType (array.GetType().GetElementType() |> Option.ofObj |> Option.get)
                        Some (ArrayType(elementType, array.Rank))
                | null -> actualType
                | :? FSharpType -> Some (RuntimeType typeof<Type>)
                | value -> actualType |> Option.orElseWith (fun () -> Some (runtimeType (value.GetType())))
            { Value = value; Type = actualType; Elements = elements }
        fun (attribute: FSharpAttribute) ->
            let syntax =
                syntaxAttributes.Value
                |> List.tryFind (fun syntax ->
                    syntax.TypeName.Range.Start = attribute.Range.Start
                    && (syntax.Range.EndLine, syntax.Range.EndColumn) >= (attribute.Range.EndLine, attribute.Range.EndColumn))
                |> Option.map (fun syntax -> constructorArguments syntax.ArgExpr)
                |> Option.defaultValue []
            match attribute.ConstructorArguments |> Seq.toList with
            | [ (t, value) ] when isArray t && Symbols.typeName t.GenericArguments[0] = Some "System.Object" ->
                let expressions = if syntax.Length = 1 then arrayElements syntax.Head else syntax
                values value |> List.mapi (fun index value -> argument None (expressions |> List.tryItem index) value)
            | arguments ->
                arguments |> List.mapi (fun index (t, value) -> argument (Some (symbolType t)) (syntax |> List.tryItem index) value)

    let private rows (context: RuleContext) (argumentsFor: FSharpAttribute -> Argument list) (methodInfo: FSharpMemberOrFunctionOrValue) =
        let allAttributes = methodInfo.Attributes |> Seq.toList
        let data =
            allAttributes |> List.filter (fun attribute ->
                not attribute.IsUnresolved && Symbols.hasTypeName (Symbols.frameworkNamespace + "DataRowAttribute") attribute.AttributeType)
        let report attribute suffix arguments =
            context.Report "MSTEST0014" "DataRowShouldBeValidTitle" ("DataRowShouldBeValidMessageFormat_" + suffix)
                (location context methodInfo.DeclarationLocation attribute) arguments
        let seenValues = ResizeArray<FSharpAttribute * Argument list * int>()
        let displayNames = HashSet<string>(StringComparer.Ordinal)
        let rec equalArgument left right =
            let typesMatch =
                match left.Type, right.Type with
                | Some left, Some right -> sameType left right
                | None, None -> true
                | _ -> false
            typesMatch
            && (match left.Value, right.Value with
                | (:? double as l), (:? double as r) -> BitConverter.DoubleToInt64Bits(l) = BitConverter.DoubleToInt64Bits(r)
                | (:? single as l), (:? single as r) -> BitConverter.SingleToInt32Bits(l) = BitConverter.SingleToInt32Bits(r)
                | (:? Array), (:? Array) -> left.Elements.Length = right.Elements.Length && List.forall2 equalArgument left.Elements right.Elements
                | _ -> Object.Equals(left.Value, right.Value))
        let sameRow (leftAttribute: FSharpAttribute) left (rightAttribute: FSharpAttribute) right =
            leftAttribute.ConstructorArguments.Count = rightAttribute.ConstructorArguments.Count
            && Seq.forall2 (fun (left: FSharpType, _) (right: FSharpType, _) -> left.Equals(right)) leftAttribute.ConstructorArguments rightAttribute.ConstructorArguments
            && List.length left = List.length right
            && List.forall2 equalArgument left right
        for attribute in data do
            let arguments = argumentsFor attribute
            let index = allAttributes |> List.findIndex (fun a -> Object.ReferenceEquals(a, attribute))
            match seenValues |> Seq.tryFind (fun (oldAttribute, old, _) -> sameRow oldAttribute old attribute arguments) with
            | Some (_, _, first) ->
                context.Report "MSTEST0042" "DuplicateDataRowTitle" "DuplicateDataRowMessageFormat" (location context methodInfo.DeclarationLocation attribute) [| box first; box index |]
            | None -> seenValues.Add(attribute, arguments, index)
            if Symbols.hasTypeName (Symbols.frameworkNamespace + "DataRowAttribute") attribute.AttributeType
               && (Symbols.named "DisplayName" attribute |> Option.exists Symbols.nonemptyString)
               && (Symbols.named "ArgumentsDisplayName" attribute |> Option.exists Symbols.nonemptyString) then
                context.Report "MSTEST0089" "ConflictingDataRowDisplayNamesTitle" "ConflictingDataRowDisplayNamesMessageFormat"
                    (location context methodInfo.DeclarationLocation attribute) [||]
            if Symbols.isTest methodInfo then
                match Symbols.named "DisplayName" attribute |> Option.bind stringValue with
                | Some name when not (String.IsNullOrWhiteSpace(name)) && not (displayNames.Add(name)) ->
                    context.Report "MSTEST0087" "DuplicateDataRowDisplayNameTitle" "DuplicateDataRowDisplayNameMessageFormat" (location context methodInfo.DeclarationLocation attribute) [| box name |]
                | _ -> ()
                let declaredParameters = Symbols.compiledParameters methodInfo
                let explicitNullToken =
                    arguments.Length = declaredParameters.Length
                    && (arguments |> List.tryLast |> Option.exists (fun argument -> argument.Value = null))
                    && (match declaredParameters, arguments with
                        | [ data; _ ], first :: _ when isArray data.Type && Symbols.typeName data.Type.GenericArguments[0] = Some "System.Object" ->
                            first.Value = null || compatible first data.Type
                        | _ -> true)
                let parameters =
                    match declaredParameters |> List.tryLast with
                    | Some last when Symbols.typeName last.Type = Some "System.Threading.CancellationToken" && not explicitNullToken ->
                        declaredParameters |> List.take (declaredParameters.Length - 1)
                    | _ -> declaredParameters
                let tokenOmitted = parameters.Length < declaredParameters.Length
                let arrayOnly =
                    match parameters with
                    | [ p ] -> isArray p.Type && (not tokenOmitted || Symbols.typeName p.Type.GenericArguments[0] = Some "System.Object")
                    | _ -> false
                let paramArray = parameters |> List.tryLast |> Option.exists (fun p -> p.IsParamArrayArg)
                let required = parameters |> List.filter (fun p -> not p.IsOptionalArg) |> List.length
                let countValid =
                    arrayOnly
                    || (arguments.Length >= required - (if paramArray then 1 else 0)
                        && (paramArray || arguments.Length <= parameters.Length))
                if not countValid then report attribute "ArgumentCountMismatch" [| box arguments.Length; box parameters.Length |]
                else
                    let inferred = Dictionary<FSharpGenericParameter, ArgumentType>()
                    let mismatches = ResizeArray<string>()
                    for i, argument in List.indexed arguments do
                        if not parameters.IsEmpty then
                            let parameter = parameters[min i (parameters.Length - 1)]
                            let expected =
                                if (paramArray && i >= parameters.Length - 1) || (arrayOnly && (tokenOmitted || arguments.Length > 1)) then
                                    parameter.Type.GenericArguments[0]
                                else parameter.Type
                            let genericType =
                                if expected.IsGenericParameter then Some expected.GenericParameter
                                elif isArray expected && expected.GenericArguments[0].IsGenericParameter then Some expected.GenericArguments[0].GenericParameter
                                else None
                            match genericType with
                            | Some generic ->
                                let item =
                                    if argument.Value = null then None
                                    elif isArray expected then
                                        argument.Type |> Option.bind (function ArrayType(element, _) -> Some element | _ -> None)
                                    else argument.Type
                                match item with
                                | Some item ->
                                    match inferred.TryGetValue(generic) with
                                    | true, previous when assignable false true item previous -> ()
                                    | true, previous when not (assignable false true previous item) ->
                                        let name argument = (typeName argument |> Option.defaultValue "").Split('.') |> Array.last
                                        report attribute "GenericTypeArgumentConflictingTypes" [| box generic.Name; box (name previous); box (name item) |]
                                    | _ -> inferred[generic] <- item
                                | None -> ()
                            | None when not (compatible argument expected) ->
                                let actualType = argumentTypeName argument |> Option.defaultValue "null"
                                mismatches.Add(String.Format(Rules.resource "DataRowShouldBeValidMessageFormat_ParameterMismatch", parameter.Name |> Option.defaultValue "", displayType expected, actualType))
                            | _ -> ()
                    if mismatches.Count > 0 then report attribute "ArgumentTypeMismatch" [| box (String.Join("; ", mismatches)) |]
                    for generic in methodInfo.GenericParameters do
                        if not (inferred.ContainsKey(generic)) then report attribute "GenericTypeArgumentNotResolved" [| box generic.Name |]
            else
                report attribute "OnTestMethod" [||]

    let private dynamicData (context: RuleContext) entity (methodInfo: FSharpMemberOrFunctionOrValue) =
        for attribute in Symbols.attributes "DynamicDataAttribute" methodInfo.Attributes do
            let at = location context methodInfo.DeclarationLocation attribute
            let report suffix arguments = context.Report "MSTEST0018" "DynamicDataShouldBeValidTitle" ("DynamicDataShouldBeValidMessageFormat_" + suffix) at arguments
            if not (Symbols.isTest methodInfo) then report "OnTestMethod" [||]
            let declaring = typeArgument attribute |> Option.bind entityOfType |> Option.defaultValue entity
            let name = Symbols.positional attribute |> List.tryPick stringValue
            let kind =
                attribute.ConstructorArguments |> Seq.tryPick (fun (t, value) ->
                    if Symbols.typeName t = Some (Symbols.frameworkNamespace + "DynamicDataSourceType") then integer value else None)
            if kind.IsSome then
                context.Report "MSTEST0052" "AvoidExplicitDynamicDataSourceTypeTitle" "AvoidExplicitDynamicDataSourceTypeMessageFormat" at [||]
            match name with
            | Some name ->
                let candidates = findMembers false declaring name
                let selected = candidates |> List.tryFind (function Method m -> m.IsProperty | _ -> false) |> Option.orElse (candidates |> List.tryHead)
                let args = [| box declaring.DisplayName; box name |]
                if selected.IsNone then report "MemberNotFound" args
                elif candidates |> List.filter (function Method m -> not m.IsProperty | _ -> false) |> List.length > 1
                     && not (candidates |> List.exists (function Method m -> m.IsProperty | _ -> false)) then report "TooManyMembers" args
                else
                    let memberInfo = selected.Value
                    let actualKind = match memberInfo with | Method m when m.IsProperty -> 0 | Method _ -> 1 | Field _ -> 3
                    if kind.IsSome && kind.Value <> 2 && kind.Value <> actualKind then
                        report (match actualKind with | 0 -> "SourceTypeProperty" | 1 -> "SourceTypeMethod" | _ -> "SourceTypeField") args
                    let argumentCount =
                        attribute.ConstructorArguments |> Seq.tryPick (fun (t, value) -> if isArray t then Some (values value |> List.length) else None) |> Option.defaultValue 0
                    let generic = match memberInfo with | Method m -> m.GenericParameters.Count > 0 | _ -> false
                    if not (memberStatic memberInfo) || generic || (actualKind = 1 && (memberParameters memberInfo).Length <> argumentCount) then
                        report "DataMemberSignature" args
                    let result = memberType memberInfo
                    if Symbols.typeName result = Some "System.String"
                       || (not (isArray result) && not (inheritedInterface "System.Collections.IEnumerable" result)) then report "MemberType" args
            | None -> ()
            match Symbols.named "DynamicDataDisplayName" attribute |> Option.bind stringValue with
            | Some name ->
                let owner = Symbols.named "DynamicDataDisplayNameDeclaringType" attribute |> Option.bind (function :? FSharpType as t -> entityOfType t | _ -> None) |> Option.defaultValue entity
                let args = [| box owner.DisplayName; box name |]
                match findMembers false owner name with
                | [] -> report "MemberNotFound" args
                | [ Method m ] when not m.IsProperty ->
                    let parameters = Symbols.parameters m
                    let valid =
                        not m.IsInstanceMemberInCompiledCode && m.Accessibility.IsPublic
                        && Symbols.typeName m.ReturnParameter.Type = Some "System.String"
                        && (match parameters with | [ first; second ] -> Symbols.typeName first.Type = Some "System.Reflection.MethodInfo" && isArray second.Type && Symbols.typeName second.Type.GenericArguments[0] = Some "System.Object" | _ -> false)
                    if not valid then report "DisplayMethodSignature" args
                | [ _ ] -> report "MemberMethod" args
                | _ -> report "TooManyMembers" args
            | None -> ()

    let private condition (context: RuleContext) fallback attributes =
        for attribute in Symbols.attributes "MemberConditionAttribute" attributes do
            let report suffix args = context.Report "MSTEST0070" "MemberConditionShouldBeValidTitle" ("MemberConditionShouldBeValidMessageFormat_" + suffix) (location context fallback attribute) args
            match typeArgument attribute with
            | Some t ->
                let names = Symbols.positional attribute |> List.collect values |> List.choose stringValue |> List.filter (String.IsNullOrWhiteSpace >> not)
                for name in names do
                    let owner = entityOfType t
                    let typeName = owner |> Option.map (fun e -> e.DisplayName) |> Option.defaultValue (displayType t)
                    let args = [| box typeName; box name |]
                    let candidates = owner |> Option.map (fun entity -> findMembers true entity name) |> Option.defaultValue []
                    let runtimeCandidate = candidates |> List.tryFind (fun memberInfo ->
                        memberPublic memberInfo && memberStatic memberInfo && (memberParameters memberInfo).IsEmpty)
                    match runtimeCandidate |> Option.orElse (candidates |> List.tryHead) with
                    | None -> report "MemberNotFound" args
                    | Some memberInfo ->
                        if not (memberPublic memberInfo) then report "MemberNotPublic" args
                        elif not (memberStatic memberInfo) then report "MemberNotStatic" args
                        elif not (memberParameters memberInfo).IsEmpty then report "MethodHasParameters" args
                        elif (match memberInfo with Method m -> m.IsProperty && not m.HasGetterMethod | _ -> false) then report "PropertyNotReadable" args
                        elif Symbols.typeName (memberType memberInfo) <> Some "System.Boolean" then report "MemberWrongReturnType" args
            | None -> ()

    let private testFilters (context: RuleContext) =
        let providers = context.AssemblyAttributes |> List.filter (fun a -> a.AttributeType.TryGetFullName() |> Option.exists (fun n -> n = Symbols.frameworkNamespace + "TestFilterProviderAttribute" || n = Symbols.frameworkNamespace + "TestFilterProviderAttribute`1"))
        for attribute in providers do
            if attribute.Range.FileName = context.FileName then
                let report suffix args = context.Report "MSTEST0081" "TestFilterProviderShouldBeValidTitle" ("TestFilterProviderShouldBeValidMessageFormat_" + suffix) attribute.Range args
                if providers.Length > 1 then report "Multiple" [||]
                let filter = typeArgument attribute |> Option.orElse (attribute.AttributeType.GenericArguments |> Seq.tryHead)
                match filter with
                | None -> report "Null" [||]
                | Some t ->
                    let args = [| box (displayType t) |]
                    match entityOfType t with
                    | None -> report "NotATestFilter" args
                    | Some entity ->
                        if entity.GenericParameters.Count > 0 then report "Generic" args
                        elif entity.IsAbstractClass || entity.IsInterface || entity.IsFSharpModule then report "NotInstantiable" args
                        elif not (inheritedInterface (Symbols.frameworkNamespace + "ITestFilter") t) then report "NotATestFilter" args
                        elif not entity.IsValueType
                             && not (entity.MembersFunctionsAndValues |> Seq.exists (fun m -> m.IsConstructor && m.Accessibility.IsPublic && (Symbols.compiledParameters m).IsEmpty)) then report "NoParameterlessConstructor" args

    let private platform (context: RuleContext) entity fallback name attributes isMethod =
        let platformAttributes attrs =
            attrs |> Seq.filter (fun (a: FSharpAttribute) -> a.AttributeType.TryGetFullName() = Some "System.Runtime.Versioning.SupportedOSPlatformAttribute" || a.AttributeType.TryGetFullName() = Some "System.Runtime.Versioning.UnsupportedOSPlatformAttribute") |> Seq.toList
        let local = platformAttributes attributes
        let rec owners (entity: FSharpEntity) = seq { yield entity.Attributes; match entity.DeclaringEntity with | Some parent -> yield! owners parent | _ -> () }
        let scopes = [ yield local; yield! owners entity |> Seq.map platformAttributes; yield platformAttributes context.AssemblyAttributes ]
        let versionedUnsupported =
            scopes |> List.collect id |> List.exists (fun a ->
                a.AttributeType.TryGetFullName() = Some "System.Runtime.Versioning.UnsupportedOSPlatformAttribute"
                && (Symbols.positional a |> List.tryPick stringValue |> Option.exists (Seq.exists Char.IsDigit)))
        if not versionedUnsupported && (not local.IsEmpty || (not isMethod && scopes |> List.exists (not << List.isEmpty))) then
            let mask scope =
                if List.isEmpty scope then Some (15, true)
                else
                    let supported (a: FSharpAttribute) = a.AttributeType.TryGetFullName() = Some "System.Runtime.Versioning.SupportedOSPlatformAttribute"
                    let includeMode = supported scope.Head
                    let bits =
                        scope |> List.map (fun a ->
                            if supported a <> includeMode then None
                            else Symbols.positional a |> List.tryPick stringValue |> Option.bind (fun text ->
                                let platformName = text |> Seq.takeWhile (Char.IsDigit >> not) |> Seq.toArray |> String
                                match platformName.ToLowerInvariant() with | "linux" -> Some 1 | "macos" | "osx" -> Some 2 | "windows" -> Some 4 | "freebsd" -> Some 8 | _ -> None))
                    if bits |> List.exists Option.isNone then None
                    else
                        let bits = bits |> List.choose id |> List.fold (|||) 0
                        Some ((if includeMode then bits else 15 &&& (~~~bits)), not includeMode)
            let desired = scopes |> List.map mask
            let conditionMask (attribute: FSharpAttribute option) =
                match attribute with
                | None -> 15, true
                | Some attribute ->
                    let integers = attribute.ConstructorArguments |> Seq.choose (fun (t, value) -> integer value |> Option.map (fun v -> Symbols.typeName t, v)) |> Seq.toList
                    let mode = integers |> List.tryPick (fun (t, v) -> if t = Some (Symbols.frameworkNamespace + "ConditionMode") then Some v else None) |> Option.defaultValue 0
                    let bits = integers |> List.tryPick (fun (t, v) -> if t = Some (Symbols.frameworkNamespace + "OperatingSystems") then Some v else None) |> Option.defaultValue 0
                    (if mode = 0 then bits else 15 &&& (~~~bits)), mode <> 0
            let actual =
                [ Symbols.attribute "OSConditionAttribute" attributes; if isMethod then Symbols.attribute "OSConditionAttribute" entity.Attributes else None ]
                |> List.map conditionMask |> List.fold (fun (mask, unknown) (m, u) -> mask &&& m, unknown && u) (15, true)
            let expected = desired |> List.choose id |> List.fold (fun (mask, unknown) (m, u) -> mask &&& m, unknown && u) (15, true)
            if desired |> List.exists Option.isNone || actual <> expected then
                context.Report "MSTEST0084" "OSPlatformAttributesShouldBeConsistentTitle" "OSPlatformAttributesShouldBeConsistentMessageFormat" fallback [| box name |]

    let private aot (context: RuleContext) =
        if context.Settings.TryFind("PublishAot") = Some "true" || context.Settings.TryFind("RunAOTCompilation") = Some "true" then
            for attribute in Symbols.attributes "AssemblyFixtureProviderAttribute" context.AssemblyAttributes do
                if attribute.Range.FileName = context.FileName then
                    context.Report "MSTEST0072" "AssemblyFixtureProviderNotSupportedWithNativeAotTitle" "AssemblyFixtureProviderNotSupportedWithNativeAotMessageFormat" attribute.Range [||]
            let firstSource =
                context.ProjectOptions.SourceFiles
                |> List.tryFind (fun file ->
                    let path = IO.Path.GetFullPath(file)
                    file.EndsWith(".fs", StringComparison.OrdinalIgnoreCase)
                    && not (path.Contains(".AssemblyInfo.", StringComparison.Ordinal))
                    && not (path.Contains("obj" + string IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            if firstSource |> Option.exists (fun file ->
                String.Equals(IO.Path.GetFullPath(file), IO.Path.GetFullPath(context.FileName), StringComparison.OrdinalIgnoreCase)) then
                match context.CheckFileResults with
                | Some results when results.ProjectContext.GetReferencedAssemblies()
                                    |> Seq.exists (fun assembly -> Symbols.hasAttribute "AssemblyFixtureProviderAttribute" assembly.Contents.Attributes) ->
                    let at = Range.mkRange context.FileName (Position.mkPos 1 0) (Position.mkPos 1 1)
                    context.Report "MSTEST0072" "AssemblyFixtureProviderNotSupportedWithNativeAotTitle" "AssemblyFixtureProviderNotSupportedWithNativeAotMessageFormat" at [||]
                | _ -> ()

    let private conflictingNames (context: RuleContext) =
        let rec walk (expression: FSharpExpr) =
            match expression with
            | Let((binding, (NewObject(constructor, _, _) as creation), _), body) when
                constructor.DeclaringEntity |> Option.exists (fun entity -> Symbols.hasTypeName (Symbols.frameworkNamespace + "DataRowAttribute") entity || entity.TryGetFullName() = Some (Symbols.frameworkNamespace + "TestDataRow`1")) ->
                let names = Dictionary<string, string>()
                let rec setters (e: FSharpExpr) =
                    match e with
                    | Call(Some (Value target), m, _, _, [ Const((:? string as value), _) ]) when target.Equals(binding) && (m.CompiledName = "set_DisplayName" || m.CompiledName = "set_ArgumentsDisplayName") -> names[m.CompiledName] <- value
                    | _ -> ()
                    e.ImmediateSubExpressions |> Seq.iter setters
                setters body
                if names.ContainsKey("set_DisplayName") && names.ContainsKey("set_ArgumentsDisplayName") && not (String.IsNullOrWhiteSpace(names["set_ArgumentsDisplayName"]))
                   && (constructor.DeclaringEntity |> Option.exists (fun entity -> entity.TryGetFullName() = Some (Symbols.frameworkNamespace + "TestDataRow`1") || not (String.IsNullOrWhiteSpace(names["set_DisplayName"])))) then
                    context.Report "MSTEST0089" "ConflictingDataRowDisplayNamesTitle" "ConflictingDataRowDisplayNamesMessageFormat" creation.Range [||]
            | _ -> ()
            expression.ImmediateSubExpressions |> Seq.iter walk
        let walker = { new TypedTreeCollectorBase() with override _.WalkMemberOrFunctionOrValue _ _ body = walk body }
        context.TypedTree |> Option.iter (walkTast walker)

    let analyze (context: RuleContext) =
        let argumentsFor = rowArguments context
        let walker =
            { new TypedTreeCollectorBase() with
                override _.WalkEntity entity _ =
                    condition context entity.DeclarationLocation entity.Attributes
                    if Symbols.isTestClass entity then platform context entity entity.DeclarationLocation entity.DisplayName entity.Attributes false
                    for methodInfo in entity.MembersFunctionsAndValues do
                        if not methodInfo.IsPropertyAccessor then
                            rows context argumentsFor methodInfo
                            dynamicData context entity methodInfo
                            condition context methodInfo.DeclarationLocation methodInfo.Attributes
                            if Symbols.isTest methodInfo then platform context entity methodInfo.DeclarationLocation methodInfo.DisplayName methodInfo.Attributes true
            }
        context.TypedTree |> Option.iter (walkTast walker)
        testFilters context
        aot context
        conflictingNames context
