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

module internal ExpressionRules =
    let private inside (outer: range) (inner: range) =
        outer.FileName = inner.FileName
        && (outer.StartLine, outer.StartColumn) <= (inner.StartLine, inner.StartColumn)
        && (inner.EndLine, inner.EndColumn) <= (outer.EndLine, outer.EndColumn)

    let private owner name (m: FSharpMemberOrFunctionOrValue) =
        m.DeclaringEntity |> Option.exists (Symbols.hasTypeName name)

    let private framework name = owner (Symbols.frameworkNamespace + name)
    let private methodName (m: FSharpMemberOrFunctionOrValue) =
        if m.IsPropertyAccessor && (m.CompiledName.StartsWith("get_", StringComparison.Ordinal) || m.CompiledName.StartsWith("set_", StringComparison.Ordinal)) then
            m.CompiledName.Substring(4)
        elif m.IsPropertyAccessor then m.DisplayName
        else m.CompiledName

    let rec private unwrap (expression: FSharpExpr) =
        match expression with
        | Coerce(_, inner) -> unwrap inner
        | NewObject(constructor, _, [ inner ]) when owner "System.Nullable`1" constructor -> unwrap inner
        | _ -> expression

    let private sameType left right =
        (Symbols.normalizeType left).Equals(Symbols.normalizeType right)

    let rec private constant (expression: FSharpExpr) : (obj | null) option =
        match unwrap expression with
        | Const(value, _) -> Some value
        | Value value -> value.LiteralValue
        | FSharpFieldGet(_, _, field) -> field.LiteralValue
        | DefaultValue t when not t.IsGenericParameter && not (t.HasTypeDefinition && t.TypeDefinition.IsValueType) -> Some null
        | _ -> None

    let private nullValue expression = constant expression |> Option.exists isNull
    let private boolValue expression =
        match constant expression with
        | Some (:? bool as value) -> Some value
        | _ -> None

    let private intValue expression =
        match constant expression with
        | Some (:? int as value) -> Some value
        | _ -> None

    let private stringValue expression =
        match constant expression with
        | Some (:? string as value) -> Some value
        | _ -> None

    let rec private sameReference left right =
        match unwrap left, unwrap right with
        | Value a, Value b -> a.Equals(b)
        | FSharpFieldGet(a, _, fa), FSharpFieldGet(b, _, fb) ->
            fa.Equals(fb)
            && match a, b with
               | None, None -> true
               | Some a, Some b -> sameReference a b
               | _ -> false
        | _ -> false

    let private primitiveNames =
        set [ "System.Boolean"; "System.Char"; "System.SByte"; "System.Byte"; "System.Int16"; "System.UInt16"
              "System.Int32"; "System.UInt32"; "System.Int64"; "System.UInt64"; "System.Single"; "System.Double"
              "System.Decimal"; "System.IntPtr"; "System.UIntPtr"; "System.String" ]

    let rec private typesAndConstraints (t: FSharpType) =
        seq {
            let t = Symbols.normalizeType t
            yield t
            if t.IsGenericParameter then
                for constraintInfo in t.GenericParameter.Constraints do
                    if constraintInfo.IsCoercesToConstraint then
                        yield! typesAndConstraints constraintInfo.CoercesToTarget
            else
                yield! t.AllInterfaces
                match t.BaseType with
                | Some parent -> yield! typesAndConstraints parent
                | None -> ()
        }

    let private implements name t =
        typesAndConstraints t |> Seq.exists (fun t -> Symbols.typeName t = Some name)

    let private collectionType t =
        Symbols.typeName t <> Some "System.String"
        && (t.IsArrayType || implements "System.Collections.Generic.IEnumerable`1" t)

    let private hasOwnEquality (t: FSharpType) =
        typesAndConstraints t
        |> Seq.exists (fun candidate ->
            (Symbols.typeName candidate = Some "System.IEquatable`1"
             && candidate.GenericArguments.Count = 1
             && sameType candidate.GenericArguments[0] t)
            || (candidate.HasTypeDefinition
                && (candidate.TypeDefinition.MembersFunctionsAndValues
                    |> Seq.exists (fun m ->
                       m.CompiledName = "Equals"
                       && m.IsOverrideOrExplicitInterfaceImplementation
                       && (Symbols.parameters m |> List.exists (fun p -> Symbols.typeName p.Type = Some "System.Object"))))))

    let private reflexive (t: FSharpType) =
        let t = Symbols.normalizeType t
        t.IsArrayType
        || (Symbols.typeName t |> Option.exists primitiveNames.Contains)
        || (t.HasTypeDefinition
            && (t.TypeDefinition.IsEnum
                || ((t.TypeDefinition.Attributes |> Seq.exists (fun a -> not a.IsUnresolved && Symbols.hasTypeName "Microsoft.FSharp.Core.SealedAttribute" a.AttributeType))
                    && not (hasOwnEquality t))))

    let private valueType (t: FSharpType) =
        let t = Symbols.normalizeType t
        (t.HasTypeDefinition && t.TypeDefinition.IsValueType)
        || (t.IsGenericParameter
            && (t.GenericParameter.Constraints |> Seq.exists (fun c -> c.IsNonNullableValueTypeConstraint || c.IsUnmanagedConstraint)))

    let private nonnullableValue expression =
        let expression = unwrap expression
        let referenced =
            match expression with
            | Value _ | FSharpFieldGet _ -> true
            | Call(_, m, _, _, _) -> m.IsPropertyGetterMethod
            | _ -> false
        referenced && valueType expression.Type && Symbols.typeName expression.Type <> Some "System.Nullable`1"

    let private argsByName (m: FSharpMemberOrFunctionOrValue) arguments =
        let parameters = Symbols.parameters m
        if parameters.Length = List.length arguments then List.zip parameters arguments else []

    let private argument name m arguments =
        argsByName m arguments |> List.tryPick (fun (p, value) -> if p.Name = Some name then Some value else None)

    let private comparisonType (m: FSharpMemberOrFunctionOrValue) typeArguments =
        match typeArguments with
        | [ t ] -> Some t
        | _ -> Symbols.parameters m |> List.tryHead |> Option.map (fun p -> p.Type)

    let private customComparer m typeArguments arguments =
        match argument "comparer" m arguments with
        | None -> false
        | Some value when nullValue value -> false
        | Some value ->
            match unwrap value, comparisonType m typeArguments with
            | Call(_, getter, enclosingTypes, _, []), Some t
                when owner "System.Collections.Generic.EqualityComparer`1" getter && methodName getter = "Default" ->
                enclosingTypes <> [ t ]
            | _ -> true

    let private fixtures =
        [ "TestInitializeAttribute"; "TestCleanupAttribute"; "ClassInitializeAttribute"; "ClassCleanupAttribute"
          "GlobalTestInitializeAttribute"; "GlobalTestCleanupAttribute" ]

    let private testCode includeAssembly (m: FSharpMemberOrFunctionOrValue) =
        Symbols.isTest m
        || List.exists (fun name -> Symbols.hasAttribute name m.Attributes) fixtures
        || (includeAssembly && List.exists (fun name -> Symbols.hasAttribute name m.Attributes) [ "AssemblyInitializeAttribute"; "AssemblyCleanupAttribute" ])

    let rec private containingTypes (entity: FSharpEntity) =
        seq {
            yield entity
            match entity.BaseType with
            | Some parent when parent.HasTypeDefinition -> yield! containingTypes parent.TypeDefinition
            | _ -> ()
        }

    let private inheritedMethodAttributes (m: FSharpMemberOrFunctionOrValue) =
        seq {
            yield! m.Attributes
            if m.IsOverrideOrExplicitInterfaceImplementation then
                for signature in m.ImplementedAbstractSignatures do
                    if signature.DeclaringType.HasTypeDefinition then
                        for parent in containingTypes signature.DeclaringType.TypeDefinition do
                            for candidate in parent.MembersFunctionsAndValues do
                                if candidate.IsDispatchSlot && candidate.LogicalName = signature.Name
                                   && (Symbols.parameters candidate).Length = (Symbols.parameters m).Length
                                   && Seq.forall2 (fun (a: FSharpParameter) (b: FSharpParameter) -> a.Type.Equals(b.Type)) (Symbols.parameters candidate) (Symbols.parameters m) then
                                    yield! candidate.Attributes
        }

    let private coordinationAttributes (m: FSharpMemberOrFunctionOrValue) =
        seq {
            if Symbols.isTest m then yield! inheritedMethodAttributes m
            match m.DeclaringEntity with
            | Some entity ->
                for parent in containingTypes entity do yield! parent.Attributes
            | None -> ()
        }

    let private hasLock key m =
        coordinationAttributes m
        |> Symbols.attributes "ResourceLockAttribute"
        |> List.exists (fun a ->
            match key, Symbols.positional a with
            | None, _ -> true
            | Some key, (:? string as value) :: _ -> value = key
            | _ -> false)

    let private optedOut m = Symbols.hasAttribute "DoNotParallelizeAttribute" (coordinationAttributes m)

    let rec private synUnwrap = function
        | SynExpr.Paren(expr = expression)
        | SynExpr.Typed(expr = expression) -> synUnwrap expression
        | expression -> expression

    let rec private firstExpression expression =
        match synUnwrap expression with
        | SynExpr.Sequential(expr1 = first)
        | SynExpr.SequentialOrImplicitYield(expr1 = first) -> firstExpression first
        | SynExpr.ComputationExpr(expr = body) -> firstExpression body
        | SynExpr.App(argExpr = SynExpr.ComputationExpr(expr = body)) -> firstExpression body
        | expression -> expression

    let rec private statementCount expression =
        match synUnwrap expression with
        | SynExpr.Sequential(expr1 = first; expr2 = rest)
        | SynExpr.SequentialOrImplicitYield(expr1 = first; expr2 = rest) -> statementCount first + statementCount rest
        | SynExpr.LetOrUse binding -> binding.Bindings.Length + statementCount binding.Body
        | SynExpr.ComputationExpr(expr = body) -> statementCount body
        | SynExpr.App(argExpr = SynExpr.ComputationExpr(expr = body)) -> statementCount body
        | SynExpr.ImplicitZero _ -> 0
        | _ -> 1

    let rec private stringLiteralRange expression =
        match synUnwrap expression with
        | SynExpr.Const(constant = SynConst.String _; range = literalRange) -> Some literalRange
        | SynExpr.Tuple(exprs = expressions) -> expressions |> List.tryPick stringLiteralRange
        | SynExpr.App(funcExpr = functionExpression; argExpr = argument) ->
            match stringLiteralRange functionExpression with
            | Some literalRange -> Some literalRange
            | None -> stringLiteralRange argument
        | _ -> None

    let analyze (context: RuleContext) : unit =
        let report id prefix diagnosticRange arguments =
            context.Report id (prefix + "Title") (prefix + "MessageFormat") diagnosticRange arguments

        let syntax = ResizeArray<SynExpr>()
        let bindings = ResizeArray<SynBinding>()
        let attributes = ResizeArray<SynAttribute>()
        let catches = ResizeArray<range * bool>()
        let filters = ResizeArray<range>()
        let lambdas = ResizeArray<range>()
        let collectAttributes (attributeLists: SynAttributeList list) =
            for attributeList in attributeLists do
                attributes.AddRange(attributeList.Attributes)
        let syntaxWalker =
            { new SyntaxCollectorBase() with
                override _.WalkExpr(_, expression) =
                    syntax.Add(expression)
                    match expression with
                    | SynExpr.TryWith(withCases = clauses) ->
                        for SynMatchClause(whenExpr = guard; resultExpr = body) in clauses do
                            catches.Add(body.Range, guard.IsSome)
                            guard |> Option.iter (fun expression -> filters.Add(expression.Range))
                    | SynExpr.Lambda(range = lambdaRange) -> lambdas.Add(lambdaRange)
                    | _ -> ()
                override _.WalkBinding(_, binding) =
                    bindings.Add(binding)
                    let (SynBinding(attributes = attributeLists)) = binding
                    collectAttributes attributeLists
                // The SDK doesn't visit attributes embedded in type and binding headers through WalkAttribute.
                override _.WalkTypeDefn(_, SynTypeDefn(typeInfo = SynComponentInfo(attributes = attributeLists))) =
                    collectAttributes attributeLists
                override _.WalkComponentInfo(_, SynComponentInfo(attributes = attributeLists)) =
                    collectAttributes attributeLists
                override _.WalkSynModuleOrNamespace(_, SynModuleOrNamespace(attribs = attributeLists)) =
                    collectAttributes attributeLists
                override _.WalkAttribute(_, attribute) = attributes.Add(attribute)
            }
        walkAst syntaxWalker context.ParseFileResults.ParseTree
        let symbolUses =
            context.CheckFileResults
            |> Option.map (fun checkedFile -> checkedFile.GetAllUsesOfAllSymbolsInFile() |> Seq.toArray)
            |> Option.defaultValue [||]
        let sourceSymbols diagnosticRange =
            symbolUses
            |> Array.choose (fun useInfo ->
                if inside diagnosticRange useInfo.Range then
                    match useInfo.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as m -> Some m
                    | _ -> None
                else None)
        let explicitMember name diagnosticRange =
            sourceSymbols diagnosticRange |> Array.exists (fun m -> methodName m = name)
        let coreMember name diagnosticRange =
            sourceSymbols diagnosticRange
            |> Array.exists (fun m -> m.CompiledName = name && m.Assembly.SimpleName = "FSharp.Core")
        let syntaxWithin diagnosticRange =
            syntax |> Seq.filter (fun expression -> inside diagnosticRange expression.Range)
        let inCatch diagnosticRange =
            catches
            |> Seq.filter (fun (catchRange, _) -> inside catchRange diagnosticRange)
            |> Seq.sortBy (fun (r, _) -> r.EndLine - r.StartLine, r.EndColumn - r.StartColumn)
            |> Seq.tryHead
            |> Option.map snd
        let parallelEnabled =
            not (Symbols.hasAttribute "DoNotParallelizeAttribute" context.AssemblyAttributes)
            && (Symbols.hasAttribute "ParallelizeAttribute" context.AssemblyAttributes
                || (context.Settings |> Map.tryFind "mstest_parallel_safety_mode"
                    |> Option.exists (fun value -> value.Equals("always", StringComparison.OrdinalIgnoreCase))))
        let referencedEntities =
            context.CheckProjectResults
            |> Option.map (fun project ->
                project.ProjectContext.GetReferencedAssemblies()
                |> Seq.collect (fun assembly -> Symbols.allEntities assembly.Contents.Entities)
                |> Seq.choose (fun entity -> entity.TryGetFullName() |> Option.map (fun name -> name, entity))
                |> Map.ofSeq)
            |> Option.defaultValue Map.empty
        let availableFramework name = referencedEntities.ContainsKey(Symbols.frameworkNamespace + name)
        let rec syntaxCall expression =
            match synUnwrap expression with
            | SynExpr.App(funcExpr = functionExpression; argExpr = arguments) ->
                let rec callTarget = function
                    | SynExpr.TypeApp(expr = expression) -> callTarget expression
                    | expression -> sourceSymbols expression.Range |> Array.tryLast
                callTarget (synUnwrap functionExpression) |> Option.map (fun m -> m, arguments)
            | _ -> None
        let syntaxBinary expression =
            match synUnwrap expression with
            | SynExpr.App(funcExpr = SynExpr.App(isInfix = true; funcExpr = operator; argExpr = left); argExpr = right) ->
                sourceSymbols operator.Range
                |> Array.tryPick (fun m ->
                    if m.Assembly.SimpleName = "FSharp.Core" then Some(m.CompiledName, left, right) else None)
            | _ -> None
        let stripNot expression =
            match syntaxCall expression with
            | Some(m, operand) when m.CompiledName = "Not" && m.Assembly.SimpleName = "FSharp.Core" -> synUnwrap operand
            | _ -> synUnwrap expression
        let astBinary diagnosticRange =
            syntaxWithin diagnosticRange
            |> Seq.tryPick (fun expression ->
                match synUnwrap expression with
                | SynExpr.App(funcExpr = SynExpr.App(isInfix = true; funcExpr = operator; argExpr = left); argExpr = right)
                    when expression.Range = diagnosticRange ->
                    sourceSymbols operator.Range
                    |> Array.tryPick (fun m ->
                        if m.Assembly.SimpleName = "FSharp.Core" then Some(m.CompiledName, left, right) else None)
                | _ -> None)
        let binary expression =
            let expression = unwrap expression
            let operators =
                set [ "op_Equality"; "op_Inequality"; "op_GreaterThan"; "op_GreaterThanOrEqual"; "op_LessThan"; "op_LessThanOrEqual" ]
            match expression with
            | Call(_, m, _, _, [ left; right ]) when m.Assembly.SimpleName = "FSharp.Core" && operators.Contains(m.CompiledName) ->
                Some(m.CompiledName, unwrap left, unwrap right)
            | ILAsm(_, _, [ left; right ]) ->
                astBinary expression.Range
                |> Option.filter (fun (name, _, _) -> operators.Contains(name))
                |> Option.map (fun (name, _, _) -> name, unwrap left, unwrap right)
            | _ -> None
        let negation expression =
            match unwrap expression with
            | Call(_, m, _, _, [ value ]) when m.CompiledName = "Not" && m.Assembly.SimpleName = "FSharp.Core" -> Some value
            | _ when coreMember "Not" expression.Range ->
                // Constant folding erases `not true`, but its resolved syntax still records the unary call.
                syntaxWithin expression.Range
                |> Seq.tryPick (function
                    | SynExpr.App(funcExpr = operator; argExpr = operand) when coreMember "Not" operator.Range -> Some operand.Range
                    | _ -> None)
                |> Option.map (fun _ -> expression)
            | _ -> None
        let rec allExpressions (expression: FSharpExpr) =
            seq {
                yield expression
                for child in expression.ImmediateSubExpressions do yield! allExpressions child
            }
        let sourceCalls body =
            allExpressions body
            |> Seq.choose (fun expression ->
                match expression with
                | Call(receiver, m, enclosingTypes, typeArguments, args) when explicitMember (methodName m) expression.Range ->
                    Some(expression, receiver, m, enclosingTypes, typeArguments, args)
                | _ -> None)
        let bclCount expression =
            match unwrap expression with
            | Call(receiver, getter, _, _, args) ->
                let name = methodName getter
                let declaring = getter.DeclaringEntity |> Option.bind (fun e -> e.TryGetFullName()) |> Option.defaultValue ""
                if owner "System.Linq.Enumerable" getter && name = "Count" && args.Length = 1 then Some false
                elif name = "Length" || name = "Count" then
                    receiver |> Option.bind (fun value ->
                        let span =
                            Symbols.typeName value.Type
                            |> Option.exists (fun name -> (set [ "System.Span`1"; "System.ReadOnlySpan`1"; "System.Memory`1"; "System.ReadOnlyMemory`1" ]).Contains(name))
                        if value.Type.IsArrayType
                           || (declaring.StartsWith("System.", StringComparison.Ordinal) && (collectionType value.Type || span))
                        then Some span else None)
                else None
            | ILAsm(_, _, [ array ]) when array.Type.IsArrayType && explicitMember "Length" expression.Range -> Some false
            | _ -> None
        let linqPredicate name expression =
            match unwrap expression with
            | Call(_, m, _, _, args) when owner "System.Linq.Enumerable" m && m.CompiledName = name ->
                args.Length = 2
                || (match args with
                    | [ source ] ->
                        match unwrap source with
                        | Call(_, where, _, _, [ _; _ ]) -> owner "System.Linq.Enumerable" where && where.CompiledName = "Where"
                        | _ -> false
                    | _ -> false)
            | _ -> false
        let properBoolean isTrue expression =
            let choose positive negative = Some(if isTrue then positive else negative)
            match binary expression with
            | Some((("op_Equality" | "op_Inequality") as op), left, right) when nullValue left || nullValue right ->
                if (op = "op_Equality") = isTrue then Some "IsNull" else Some "IsNotNull"
            | Some(op, left, right) ->
                let countSide =
                    if intValue right = Some 0 && (bclCount left).IsSome then Some(op, (bclCount left).Value)
                    elif intValue left = Some 0 && (bclCount right).IsSome then
                        Some((if op = "op_LessThan" then "op_GreaterThan" else op), (bclCount right).Value)
                    else None
                match countSide with
                | Some("op_Equality", false) -> choose "IsEmpty" "IsNotEmpty"
                | Some(("op_Inequality" | "op_GreaterThan"), false) -> choose "IsNotEmpty" "IsEmpty"
                | _ when op = "op_GreaterThan" && intValue right = Some 0 && linqPredicate "Count" left -> choose "Contains" "DoesNotContain"
                | _ ->
                    let comparable =
                        sameType left.Type right.Type
                        && (typesAndConstraints left.Type
                            |> Seq.exists (fun t ->
                                Symbols.typeName t = Some "System.IComparable`1"
                                && t.GenericArguments.Count = 1 && sameType t.GenericArguments[0] left.Type))
                    match op with
                    | "op_Equality" when not (Symbols.normalizeType left.Type).IsArrayType && not (Symbols.normalizeType right.Type).IsArrayType -> choose "AreEqual" "AreNotEqual"
                    | "op_Inequality" when not (Symbols.normalizeType left.Type).IsArrayType && not (Symbols.normalizeType right.Type).IsArrayType -> choose "AreNotEqual" "AreEqual"
                    | "op_GreaterThan" when comparable -> choose "IsGreaterThan" "IsLessThanOrEqualTo"
                    | "op_GreaterThanOrEqual" when comparable -> choose "IsGreaterThanOrEqualTo" "IsLessThan"
                    | "op_LessThan" when comparable -> choose "IsLessThan" "IsGreaterThanOrEqualTo"
                    | "op_LessThanOrEqual" when comparable -> choose "IsLessThanOrEqualTo" "IsGreaterThan"
                    | _ -> None
            | None ->
                match unwrap expression with
                | Call(_, m, _, _, args) when owner "System.String" m && args.Length = 1 && Symbols.typeName args.Head.Type = Some "System.String" ->
                    match m.CompiledName with
                    | "Contains" -> choose "Contains" "DoesNotContain"
                    | "StartsWith" -> choose "StartsWith" "DoesNotStartWith"
                    | "EndsWith" -> choose "EndsWith" "DoesNotEndWith"
                    | _ -> None
                | Call(_, m, _, _, args) when owner "System.Linq.Enumerable" m ->
                    match m.CompiledName, args.Length with
                    | "Any", 1 when not (linqPredicate "Any" expression) -> choose "IsNotEmpty" "IsEmpty"
                    | "Any", _ | "Contains", _ -> choose "Contains" "DoesNotContain"
                    | _ -> None
                | Call(Some instance, m, _, _, [ item ]) when m.CompiledName = "Contains" ->
                    let declaring = m.DeclaringEntity |> Option.bind (fun e -> e.TryGetFullName()) |> Option.defaultValue ""
                    let enumerable =
                        typesAndConstraints instance.Type
                        |> Seq.tryFind (fun t -> Symbols.typeName t = Some "System.Collections.Generic.IEnumerable`1")
                    if declaring.StartsWith("System.Collections.", StringComparison.Ordinal)
                       && (enumerable |> Option.exists (fun t -> sameType t.GenericArguments[0] item.Type))
                       && not (implements "System.Collections.IDictionary" instance.Type)
                    then choose "Contains" "DoesNotContain" else None
                | _ -> None
        let checkResourceAttributes (values: seq<FSharpAttribute>) =
            for attribute in values do
                if not attribute.IsUnresolved
                   && Symbols.hasTypeName (Symbols.frameworkNamespace + "ResourceLockAttribute") attribute.AttributeType then
                    match Symbols.positional attribute with
                    | (:? string as key) :: _ ->
                        let source = attributes |> Seq.tryFind (fun a -> inside a.Range attribute.Range || inside attribute.Range a.Range)
                        match source with
                        | Some source ->
                            stringLiteralRange source.ArgExpr
                            |> Option.iter (fun literalRange -> report "MSTEST0073" "PreferConstantForResourceLock" literalRange [| box key |])
                        | None -> ()
                    | _ -> ()
        let checkMethod (methodInfo: FSharpMemberOrFunctionOrValue) (body: FSharpExpr) =
            checkResourceAttributes methodInfo.Attributes
            let generatedBindings =
                allExpressions body
                |> Seq.choose (function
                    | Let((value, initializer, _), _) when value.IsCompilerGenerated && not value.IsMutable -> Some(value, initializer)
                    | _ -> None)
                |> Seq.toList
            let rec resolve expression =
                match unwrap expression with
                | Value value when value.IsCompilerGenerated ->
                    generatedBindings
                    |> List.tryPick (fun (candidate, initializer) -> if candidate.Equals(value) then Some initializer else None)
                    |> Option.map resolve
                    |> Option.defaultValue expression
                | other -> other
            let canFlowToken =
                Symbols.parameters methodInfo |> List.exists (fun p -> Symbols.typeName p.Type = Some(Symbols.frameworkNamespace + "TestContext"))
                || (not methodInfo.IsInstanceMemberInCompiledCode && (Symbols.parameters methodInfo).IsEmpty
                    && (Symbols.hasAttribute "ClassCleanupAttribute" methodInfo.Attributes || Symbols.hasAttribute "AssemblyCleanupAttribute" methodInfo.Attributes))
                || Symbols.isTest methodInfo
                || (methodInfo.IsInstanceMemberInCompiledCode
                    && (methodInfo.DeclaringEntity |> Option.exists (fun entity ->
                        (entity.MembersFunctionsAndValues |> Seq.exists (fun m -> m.IsInstanceMember && m.IsProperty && Symbols.typeName m.ReturnParameter.Type = Some(Symbols.frameworkNamespace + "TestContext")))
                        || (entity.FSharpFields |> Seq.exists (fun field -> not field.IsStatic && Symbols.typeName field.FieldType = Some(Symbols.frameworkNamespace + "TestContext"))))))
            let fixture =
                [ "AssemblyInitialize"; "AssemblyCleanup"; "ClassInitialize"; "ClassCleanup" ]
                |> List.tryFind (fun name -> Symbols.hasAttribute (name + "Attribute") methodInfo.Attributes)
            let parallelTest = parallelEnabled && testCode false methodInfo && not (optedOut methodInfo)
            let calls =
                sourceCalls body
                |> Seq.map (fun (expression, receiver, m, enclosingTypes, typeArguments, args) ->
                    expression, receiver |> Option.map resolve, m, enclosingTypes, typeArguments, args |> List.map resolve)
                |> Seq.toArray
            let restrictedProperty name diagnosticRange =
                match fixture with
                | Some fixtureName when
                    (set [ "TestData"; "TestDisplayName"; "DataRow"; "DataConnection"; "TestName" ]).Contains(name)
                    || (name = "FullyQualifiedTestClassName" && fixtureName.StartsWith("Assembly", StringComparison.Ordinal)) ->
                    report "MSTEST0048" "TestContextPropertyUsage" diagnosticRange [| box name; box fixtureName |]
                | _ -> ()
            let tokenType = "System.Threading.CancellationToken"
            for expression, receiver, m, _, typeArguments, args in calls do
                let name = methodName m
                let r = expression.Range
                let parameters = Symbols.parameters m
                let emit id prefix arguments = report id prefix r arguments
                if framework "Assert" m then
                    let condition = argument "condition" m args
                    let expected = argument (if name = "AreNotEqual" || name = "AreNotSame" then "notExpected" else "expected") m args
                    let actual = argument "actual" m args
                    let value = argument "value" m args
                    if (name = "IsTrue" || name = "IsFalse") && (condition |> Option.exists (fun value -> (negation value).IsSome)) then
                        emit "MSTEST0023" "DoNotNegateBooleanAssertion" [||]
                    let same = match expected, actual with Some a, Some b -> sameReference a b | _ -> false
                    let equals =
                        match expected |> Option.bind constant, actual |> Option.bind constant with
                        | Some a, Some b -> Some(Object.Equals(a, b))
                        | _ when same && (comparisonType m typeArguments |> Option.exists reflexive) -> Some true
                        | _ -> None
                    let defaultEquality = not (customComparer m typeArguments args)
                    let always =
                        match name with
                        | "IsTrue" -> condition |> Option.bind boolValue
                        | "IsFalse" -> condition |> Option.bind boolValue |> Option.map not
                        | "AreEqual" when defaultEquality -> equals
                        | "AreNotEqual" when defaultEquality -> equals |> Option.map not
                        | "AreSame" when same -> Some true
                        | "AreNotSame" when same -> Some false
                        | "IsNull" when value |> Option.exists nullValue -> Some true
                        | "IsNotNull" when value |> Option.exists nullValue -> Some false
                        | "IsNull" when value |> Option.exists nonnullableValue -> Some false
                        | "IsNotNull" when value |> Option.exists nonnullableValue -> Some true
                        | _ -> None
                    let integralNames =
                        set [ "System.SByte"; "System.Byte"; "System.Int16"; "System.UInt16"; "System.Int32"; "System.UInt32"; "System.Int64"; "System.UInt64" ]
                    let numericLiteral (value: FSharpExpr) =
                        (Symbols.typeName value.Type |> Option.exists integralNames.Contains)
                        && (syntaxWithin value.Range |> Seq.exists (function
                            | SynExpr.Const(constant = SynConst.SByte _)
                            | SynExpr.Const(constant = SynConst.Byte _)
                            | SynExpr.Const(constant = SynConst.Int16 _)
                            | SynExpr.Const(constant = SynConst.UInt16 _)
                            | SynExpr.Const(constant = SynConst.Int32 _)
                            | SynExpr.Const(constant = SynConst.UInt32 _)
                            | SynExpr.Const(constant = SynConst.Int64 _)
                            | SynExpr.Const(constant = SynConst.UInt64 _) -> true
                            | _ -> false))
                    let enumConversion (value: FSharpExpr) =
                        (Symbols.typeName value.Type |> Option.exists integralNames.Contains)
                        && (symbolUses |> Array.exists (fun u ->
                            inside value.Range u.Range
                            && match u.Symbol with
                               | :? FSharpField as field -> field.DeclaringEntity |> Option.exists (fun entity -> entity.IsEnum)
                               | _ -> false))
                    let enumContract =
                        match expected, actual with
                        | Some a, Some b -> (numericLiteral a && enumConversion b) || (numericLiteral b && enumConversion a)
                        | _ -> false
                    match always with
                    | Some false -> emit "MSTEST0025" "PreferAssertFailOverAlwaysFalseConditions" [| box name |]
                    | Some true when not (name = "AreEqual" && enumContract) -> emit "MSTEST0032" "ReviewAlwaysTrueAssertConditionAnalyzer" [||]
                    | _ -> ()
                    let proper =
                        match name, expected, actual, condition with
                        | ("IsTrue" | "IsFalse"), _, _, Some condition -> properBoolean (name = "IsTrue") condition
                        | "AreEqual", Some expected, Some actual, _ when intValue expected = Some 1 && linqPredicate "Count" actual -> Some "ContainsSingle"
                        | ("AreEqual" | "AreNotEqual"), Some expected, Some actual, _ ->
                            match bclCount actual with
                            | Some false when intValue expected = Some 0 -> Some(if name = "AreEqual" then "IsEmpty" else "IsNotEmpty")
                            | Some _ when name = "AreEqual" && Symbols.typeName expected.Type = Some "System.Int32" -> Some "HasCount"
                            | _ when nullValue expected -> Some(if name = "AreEqual" then "IsNull" else "IsNotNull")
                            | _ when name = "AreEqual" ->
                                boolValue expected |> Option.map (fun value -> if value then "IsTrue" else "IsFalse")
                            | _ -> None
                        | "HasCount", Some expected, _, _ when intValue expected = Some 0 ->
                            let spans = set [ "System.Span`1"; "System.ReadOnlySpan`1"; "System.Memory`1"; "System.ReadOnlyMemory`1" ]
                            if parameters.Length > 1 && not (Symbols.typeName parameters[1].Type |> Option.exists spans.Contains) then Some "IsEmpty" else None
                        | _ -> None
                    proper |> Option.iter (fun proper -> emit "MSTEST0037" "UseProperAssertMethods" [| box proper; box name |])
                    if (name = "AreSame" || name = "AreNotSame") && ([ expected; actual ] |> List.choose id |> List.exists (fun value -> valueType (unwrap value).Type)) then
                        emit "MSTEST0038" "AvoidAssertAreSameWithValueTypes" [| box (if name = "AreSame" then "AreEqual" else "AreNotEqual"); box name |]
                    if (name = "AreEqual" || name = "AreNotEqual") && typeArguments.Length = 1
                       && not ([ expected; actual ] |> List.choose id |> List.exists nullValue)
                       && (not (hasOwnEquality typeArguments.Head) || customComparer m typeArguments args) then
                        let collection =
                            if collectionType typeArguments.Head then Some typeArguments.Head
                            else [ expected; actual ] |> List.choose id |> List.map (fun e -> (unwrap e).Type) |> List.tryFind collectionType
                        collection |> Option.iter (fun t ->
                            emit "MSTEST0065" "AvoidAssertAreEqualOnCollections" [| box ("Assert." + name); box (t.Format(FSharpDisplayContext.Empty)) |])
                    if name.StartsWith("Throws", StringComparison.Ordinal) then
                        match args |> List.tryHead with
                        | Some action ->
                            syntaxWithin action.Range
                            |> Seq.tryPick (function SynExpr.Lambda(body = body) -> Some body | _ -> None)
                            |> Option.iter (fun lambda ->
                                if statementCount lambda > 1 then emit "MSTEST0051" "AssertThrowsShouldContainSingleStatement" [||])
                        | None -> ()
                    if (name = "Throws" || name = "ThrowsExactly") && Symbols.isTest methodInfo
                       && not (methodInfo.IsOverrideOrExplicitInterfaceImplementation && methodInfo.ReturnParameter.Type.IsUnitType) then
                        match argument "action" m args with
                        | Some action ->
                            let sourceLambda =
                                syntaxWithin action.Range |> Seq.tryPick (function SynExpr.Lambda(body = body) -> Some body | _ -> None)
                            let blocked =
                                allExpressions action
                                |> Seq.exists (fun child ->
                                    match child with
                                    | Call(Some _, wait, _, _, []) when owner "System.Threading.Tasks.Task" wait && wait.CompiledName = "Wait" -> true
                                    | Call(Some _, getter, _, _, []) when owner "System.Threading.Tasks.Task`1" getter && methodName getter = "Result" -> true
                                    | Call(Some _, getResult, _, _, []) when getResult.CompiledName = "GetResult" ->
                                        (owner "System.Runtime.CompilerServices.TaskAwaiter" getResult
                                         || owner "System.Runtime.CompilerServices.TaskAwaiter`1" getResult
                                         || owner "System.Runtime.CompilerServices.ConfiguredTaskAwaitable+ConfiguredTaskAwaiter" getResult
                                         || owner "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter" getResult)
                                        && explicitMember "GetResult" child.Range
                                    | _ -> false)
                            let nested =
                                (lambdas |> Seq.exists (fun lambda -> inside lambda r))
                                || (filters |> Seq.exists (fun filter -> inside filter r))
                                || (syntax |> Seq.exists (function
                                    | SynExpr.App(funcExpr = functionExpression; range = lockRange) ->
                                        inside lockRange r && coreMember "Lock" functionExpression.Range
                                    | _ -> false))
                            if blocked && not nested && (sourceLambda |> Option.exists (fun lambda -> statementCount lambda = 1)) then
                                emit "MSTEST0064" "PreferAsyncAssertion" [| box (name + "Async"); box name |]
                        | None -> ()
                let replacement =
                    if framework "StringAssert" m then
                        match name with
                        | "Contains" | "StartsWith" | "EndsWith" -> Some("MSTEST0046", "StringAssertToAssert", name)
                        | "Matches" -> Some("MSTEST0046", "StringAssertToAssert", "MatchesRegex")
                        | "DoesNotMatch" -> Some("MSTEST0046", "StringAssertToAssert", "DoesNotMatchRegex")
                        | _ -> None
                    elif framework "CollectionAssert" m then
                        let comparer =
                            parameters |> List.exists (fun p ->
                                Symbols.typeName p.Type |> Option.exists (fun n ->
                                    (set [ "System.Collections.IComparer"; "System.Collections.Generic.IComparer`1"; "System.Collections.Generic.IEqualityComparer`1" ]).Contains(n)))
                        match name with
                        | "AreEqual" when not comparer -> Some("MSTEST0068", "CollectionAssertToAssert", "AreSequenceEqual")
                        | "AreNotEqual" when not comparer -> Some("MSTEST0068", "CollectionAssertToAssert", "AreNotSequenceEqual")
                        | "AreEquivalent" when not comparer && typeArguments.IsEmpty -> Some("MSTEST0068", "CollectionAssertToAssert", "AreSequenceEqual")
                        | "AreNotEquivalent" when not comparer && typeArguments.IsEmpty -> Some("MSTEST0068", "CollectionAssertToAssert", "AreNotSequenceEqual")
                        | "AllItemsAreNotNull" -> Some("MSTEST0068", "CollectionAssertToAssert", "AreAllNotNull")
                        | "AllItemsAreUnique" -> Some("MSTEST0068", "CollectionAssertToAssert", "AreAllDistinct")
                        | "AllItemsAreInstancesOfType" -> Some("MSTEST0068", "CollectionAssertToAssert", "AreAllOfType")
                        | "Contains" -> Some("MSTEST0068", "CollectionAssertToAssert", "Contains")
                        | "DoesNotContain" -> Some("MSTEST0068", "CollectionAssertToAssert", "DoesNotContain")
                        | _ -> None
                    else None
                replacement |> Option.iter (fun (id, prefix, replacement) -> emit id prefix [| box replacement; box name |])
                if framework "TestContext" m then
                    if name = "CancellationTokenSource" then emit "MSTEST0054" "UseCancellationTokenProperty" [||]
                    if m.IsPropertyGetterMethod then restrictedProperty name r
                if name = "Item" then
                    match receiver, args with
                    | Some properties, [ key ] ->
                        match unwrap properties, stringValue key with
                        | Call(_, getter, _, _, []), Some key when methodName getter = "Properties"
                            && (getter.DeclaringEntity |> Option.exists (Symbols.inherits (Symbols.frameworkNamespace + "TestContext"))) ->
                            restrictedProperty key r
                        | _ -> ()
                    | _ -> ()
                if canFlowToken && not m.IsPropertyAccessor then
                    let token = parameters |> List.tryFindBack (fun p -> Symbols.typeName p.Type = Some tokenType)
                    let omittedOptional =
                        match token with
                        | Some token when token.IsOptionalArg ->
                            argument (token.Name |> Option.defaultValue "") m args
                            |> Option.exists (fun value ->
                                value.Range = r || not (syntax |> Seq.exists (fun source -> source.Range = value.Range)))
                        | _ -> false
                    let overload =
                        token.IsNone
                        && (m.DeclaringEntity |> Option.exists (fun entity ->
                            entity.MembersFunctionsAndValues |> Seq.exists (fun candidate ->
                                let candidateParams = Symbols.parameters candidate
                                candidate.CompiledName = m.CompiledName
                                && candidate.IsInstanceMemberInCompiledCode = m.IsInstanceMemberInCompiledCode
                                && candidateParams.Length = parameters.Length + 1
                                && Symbols.typeName candidateParams[candidateParams.Length - 1].Type = Some tokenType
                                && Seq.forall2 (fun (a: FSharpParameter) (b: FSharpParameter) -> a.Type.Equals(b.Type)) parameters (candidateParams |> List.take parameters.Length))))
                    let quotation =
                        syntax |> Seq.exists (function SynExpr.Quote(range = quoteRange) -> inside quoteRange r | _ -> false)
                    let nestedFunction = lambdas |> Seq.exists (fun lambda -> inside lambda r)
                    if (omittedOptional || overload) && not quotation && not nestedFunction then emit "MSTEST0049" "FlowTestContextCancellationToken" [||]
                match inCatch r with
                | Some filtered when (framework "Assert" m || framework "StringAssert" m || framework "CollectionAssert" m)
                                     && not (filtered && framework "Assert" m && name = "Inconclusive") ->
                    emit "MSTEST0058" "AvoidAssertsInCatchBlocks" [||]
                | _ -> ()
                if testCode true methodInfo then
                    let blocking =
                        if owner "System.Threading.Thread" m && name = "Sleep" then Some "Thread.Sleep"
                        elif owner "System.Threading.Tasks.Task" m && (set [ "Wait"; "WaitAll"; "WaitAny" ]).Contains(name) then Some("Task." + name)
                        elif owner "System.Threading.Tasks.Task`1" m && name = "Result" then Some "Task<TResult>.Result"
                        else None
                    blocking |> Option.iter (fun api -> emit "MSTEST0067" "AvoidThreadSleepAndTaskWaitInTests" [| box api |])
                if parallelTest then
                    if owner "System.Environment" m && name = "SetEnvironmentVariable" && not (hasLock (Some "System.Environment.Variables") methodInfo) then
                        emit "MSTEST0074" "UndeclaredProcessGlobalStateMutation" [| box "Environment.SetEnvironmentVariable"; box "EnvironmentVariables" |]
                    elif owner "System.Console" m && (set [ "SetOut"; "SetError"; "SetIn" ]).Contains(name) && not (hasLock (Some "System.Console") methodInfo) then
                        emit "MSTEST0074" "UndeclaredProcessGlobalStateMutation" [| box ("Console." + name); box "Console" |]
                    if ((owner "System.IO.Directory" m && name = "SetCurrentDirectory")
                        || (owner "System.Environment" m && m.IsPropertySetterMethod && name = "CurrentDirectory"))
                       && not (hasLock (Some "System.Environment.CurrentDirectory") methodInfo) then
                        emit "MSTEST0075" "CurrentDirectoryMutationUnderParallelization" [| box (if owner "System.Environment" m then "Environment.CurrentDirectory" else "Directory.SetCurrentDirectory") |]
                    if owner "System.Globalization.CultureInfo" m && m.IsPropertySetterMethod
                       && (set [ "DefaultThreadCurrentCulture"; "DefaultThreadCurrentUICulture" ]).Contains(name)
                       && not (hasLock None methodInfo) then
                        emit "MSTEST0076" "CultureMutationUnderParallelization" [| box ("CultureInfo." + name) |]
                    let file = owner "System.IO.File" m
                    let directory = owner "System.IO.Directory" m
                    let mutating =
                        if file then
                            (set [ "WriteAllText"; "WriteAllTextAsync"; "WriteAllBytes"; "WriteAllBytesAsync"; "WriteAllLines"; "WriteAllLinesAsync";
                                  "AppendAllText"; "AppendAllTextAsync"; "AppendAllLines"; "AppendAllLinesAsync"; "AppendText"; "Create"; "CreateText";
                                  "CreateSymbolicLink"; "Copy"; "Move"; "Replace"; "Delete"; "OpenWrite"; "Encrypt"; "Decrypt"; "SetAttributes";
                                  "SetUnixFileMode"; "SetCreationTime"; "SetCreationTimeUtc"; "SetLastAccessTime"; "SetLastAccessTimeUtc";
                                  "SetLastWriteTime"; "SetLastWriteTimeUtc" ]).Contains(name)
                        elif directory then
                            (set [ "CreateDirectory"; "CreateSymbolicLink"; "Delete"; "Move"; "SetCreationTime"; "SetCreationTimeUtc"
                                   "SetLastAccessTime"; "SetLastAccessTimeUtc"; "SetLastWriteTime"; "SetLastWriteTimeUtc" ]).Contains(name)
                        else false
                    if mutating then
                        argsByName m args
                        |> List.tryPick (fun (parameter, value) ->
                            let mutated =
                                match file, name, parameter.Name with
                                | true, "Copy", Some "destFileName"
                                | true, "Move", Some ("sourceFileName" | "destFileName")
                                | true, "Replace", Some ("destinationFileName" | "destinationBackupFileName")
                                | false, "Move", Some ("sourceDirName" | "destDirName") -> true
                                | true, ("Copy" | "Move" | "Replace"), _ | false, "Move", _ -> false
                                | _, _, Some "path" -> true
                                | _ -> false
                            if mutated then stringValue value |> Option.filter (fun value -> value.Length > 0) else None)
                        |> Option.iter (fun path -> emit "MSTEST0077" "SharedFileSystemPathInTest" [| box path |])
            for expression in allExpressions body do
                match expression, inCatch expression.Range with
                | Call(_, raiseMethod, _, _, [ exceptionValue ]), Some filtered
                    when raiseMethod.CompiledName = "Raise" && raiseMethod.Assembly.SimpleName = "FSharp.Core" ->
                    match resolve exceptionValue with
                    | NewObject(constructor, _, _) ->
                        let isException name = constructor.DeclaringEntity |> Option.exists (Symbols.inherits (Symbols.frameworkNamespace + name))
                        if isException "AssertFailedException" || (isException "AssertInconclusiveException" && not filtered) then
                            report "MSTEST0058" "AvoidAssertsInCatchBlocks" expression.Range [||]
                    | _ -> ()
                | _ -> ()
            let methodBinding =
                bindings
                |> Seq.tryFind (fun binding -> inside binding.RangeOfBindingWithoutRhs methodInfo.DeclarationLocation)
            if Symbols.isTest methodInfo then
                methodBinding |> Option.iter (fun binding ->
                    let (SynBinding(expr = bindingBody)) = binding
                    let directives =
                        seq { binding.RangeOfBindingWithRhs.StartLine .. binding.RangeOfBindingWithRhs.EndLine }
                        |> Seq.exists (fun line ->
                            let text = context.SourceText.GetLineString(line - 1).TrimStart()
                            text.StartsWith("#if", StringComparison.Ordinal)
                            || text.StartsWith("#else", StringComparison.Ordinal)
                            || text.StartsWith("#endif", StringComparison.Ordinal))
                    let first = firstExpression bindingBody
                    match first with
                    | SynExpr.IfThenElse(ifExpr = condition; thenExpr = thenBranch; elseExpr = elseBranch) ->
                        let noElse = elseBranch.IsNone
                        let thenFirst = firstExpression thenBranch
                        let inconclusive =
                            calls |> Array.exists (fun (expression, _, m, _, _, _) ->
                                framework "Assert" m && m.CompiledName = "Inconclusive" && inside thenFirst.Range expression.Range)
                        let pureReturn =
                            match thenFirst with
                            | SynExpr.YieldOrReturn(flags = (_, true); expr = SynExpr.Const(constant = SynConst.Unit)) -> true
                            | _ -> false
                        if noElse && (inconclusive || pureReturn) && not directives then
                            let conditionCalls = calls |> Array.filter (fun (e, _, _, _, _, _) -> inside condition.Range e.Range)
                            let noConditions = not (Symbols.hasAttribute "ConditionBaseAttribute" methodInfo.Attributes)
                            let os =
                                match syntaxCall (stripNot condition) with
                                | Some(m, _) when owner "System.OperatingSystem" m ->
                                    (set [ "IsWindows"; "IsLinux"; "IsMacOS"; "IsFreeBSD"; "IsAndroid"; "IsIOS"; "IsTvOS"; "IsWatchOS"
                                           "IsBrowser"; "IsWasi"; "IsMacCatalyst" ]).Contains(m.CompiledName)
                                | Some(m, osArgument) when owner "System.Runtime.InteropServices.RuntimeInformation" m && m.CompiledName = "IsOSPlatform" ->
                                    let properties = sourceSymbols osArgument.Range
                                    (properties |> Array.exists (fun getter -> owner "System.Runtime.InteropServices.OSPlatform" getter && getter.IsProperty))
                                    || (conditionCalls |> Array.exists (fun (_, _, create, _, _, args) ->
                                        owner "System.Runtime.InteropServices.OSPlatform" create && create.CompiledName = "Create"
                                        && args.Length = 1 && (stringValue args.Head).IsSome))
                                | _ -> false
                            if os then report "MSTEST0061" "UseOSConditionAttributeInsteadOfRuntimeCheck" first.Range [||]
                            let equality = syntaxBinary condition |> Option.filter (fun (op, _, _) -> op = "op_Equality" || op = "op_Inequality")
                            let architectureProperty (source: SynExpr) =
                                (sourceSymbols source.Range |> Array.exists (fun m ->
                                    owner "System.Runtime.InteropServices.RuntimeInformation" m && methodName m = "ProcessArchitecture"))
                                && match synUnwrap source with
                                   | SynExpr.LongIdent _ | SynExpr.DotGet _ -> true
                                   | _ -> false
                            let architectureMember (source: SynExpr) =
                                symbolUses
                                |> Array.tryPick (fun u ->
                                    if inside source.Range u.Range then
                                        match u.Symbol with
                                        | :? FSharpField as field when field.DeclaringEntity |> Option.exists (Symbols.hasTypeName "System.Runtime.InteropServices.Architecture") ->
                                            Some field.Name
                                        | _ -> None
                                    else None)
                            let architecture =
                                equality |> Option.bind (fun (_, left, right) ->
                                    if architectureProperty left then architectureMember right
                                    elif architectureProperty right then architectureMember left
                                    else None)
                            let supportedArchitecture =
                                architecture |> Option.exists (fun name ->
                                    referencedEntities |> Map.tryFind (Symbols.frameworkNamespace + "TestArchitectures")
                                    |> Option.exists (fun entity -> entity.FSharpFields |> Seq.exists (fun field -> field.Name = name)))
                            if noConditions && availableFramework "ArchitectureConditionAttribute" && supportedArchitecture then
                                report "MSTEST0079" "UseArchitectureConditionAttributeInsteadOfRuntimeCheck" first.Range [||]
                            let ciLookup (source: SynExpr) =
                                match syntaxCall source with
                                | Some(m, _) when owner "System.Environment" m && m.CompiledName = "GetEnvironmentVariable" ->
                                    conditionCalls |> Array.exists (fun (e, _, target, _, _, args) ->
                                        inside source.Range e.Range && owner "System.Environment" target
                                        && target.CompiledName = "GetEnvironmentVariable" && args.Length = 1 && stringValue args.Head = Some "CI")
                                | _ -> false
                            let ci =
                                match equality with
                                | Some(_, left, right) ->
                                    match synUnwrap left, synUnwrap right with
                                    | SynExpr.Null _, _ -> ciLookup right
                                    | _, SynExpr.Null _ -> ciLookup left
                                    | _ -> false
                                | None ->
                                    match syntaxCall (stripNot condition) with
                                    | Some(m, argument) when m.CompiledName = "IsNull" && m.Assembly.SimpleName = "FSharp.Core" -> ciLookup argument
                                    | _ -> false
                            if noConditions && availableFramework "CIConditionAttribute" && ci then
                                report "MSTEST0080" "UseCIConditionAttributeInsteadOfEnvironmentCheck" first.Range [||]
                            let missingExecutable =
                                let fileExists (source: SynExpr) =
                                    match syntaxCall source with
                                    | Some(m, _) when owner "System.IO.File" m && m.CompiledName = "Exists" ->
                                        conditionCalls |> Array.tryPick (fun (e, _, target, _, _, args) ->
                                            if inside source.Range e.Range && owner "System.IO.File" target
                                               && target.CompiledName = "Exists" && args.Length = 1 then
                                                stringValue args.Head |> Option.filter (fun name -> name.Length > 0)
                                            else None)
                                    | _ -> None
                                match syntaxCall condition, equality with
                                | Some(m, operand), _ when m.CompiledName = "Not" && m.Assembly.SimpleName = "FSharp.Core" -> fileExists operand
                                | _, Some(op, left, right) ->
                                    let matches (source: SynExpr) =
                                        match synUnwrap source with
                                        | SynExpr.Const(constant = SynConst.Bool value) -> (op = "op_Equality") <> value
                                        | _ -> false
                                    if matches left then fileExists right
                                    elif matches right then fileExists left
                                    else None
                                | _ -> None
                            if noConditions && availableFramework "ExecutableConditionAttribute" then
                                missingExecutable
                                |> Option.iter (fun executable ->
                                    let matchingStart =
                                        calls |> Array.exists (fun (e, _, m, _, _, args) ->
                                            (e.Range.StartLine, e.Range.StartColumn) >= (first.Range.EndLine, first.Range.EndColumn)
                                            && owner "System.Diagnostics.Process" m && m.CompiledName = "Start"
                                            && not (lambdas |> Seq.exists (fun lambda -> inside lambda e.Range))
                                            && match args with
                                               | argument :: _ ->
                                                   let initializedFileName =
                                                       calls |> Array.tryPick (fun (initializer, _, setter, _, _, values) ->
                                                           if inside argument.Range initializer.Range
                                                              && owner "System.Diagnostics.ProcessStartInfo" setter
                                                              && methodName setter = "FileName" && setter.IsPropertySetterMethod then
                                                               Some(values |> List.tryLast |> Option.bind stringValue)
                                                           else None)
                                                   match initializedFileName with
                                                   | Some fileName -> fileName = Some executable
                                                   | None ->
                                                       stringValue argument = Some executable
                                                       || (match resolve argument with
                                                           | NewObject(constructor, _, fileName :: _) when owner "System.Diagnostics.ProcessStartInfo" constructor ->
                                                               stringValue (resolve fileName) = Some executable
                                                           | _ -> false)
                                               | _ -> false)
                                    if matchingStart then report "MSTEST0083" "UseExecutableConditionAttributeInsteadOfProcessCheck" first.Range [| box executable |])
                    | _ -> ())
        let walker =
            { new TypedTreeCollectorBase() with
                override _.WalkEntity entity _ = checkResourceAttributes entity.Attributes
                override _.WalkMemberOrFunctionOrValue methodInfo _ body = checkMethod methodInfo body
            }
        context.TypedTree |> Option.iter (walkTast walker)
