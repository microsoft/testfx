# MSTest Analyzer Performance Guide

## Overview

This document provides guidance on the performance characteristics of MSTest analyzers and best practices for maintaining good performance as analyzers are added or modified.

## Current Performance Status

✅ **All MSTest analyzers follow Roslyn performance best practices:**
- Concurrent execution enabled
- Generated code analysis disabled
- Proper symbol caching via `RegisterCompilationStartAction`
- Early exit patterns to minimize unnecessary work

## Analyzer Registration Patterns and Performance Impact

### High-Impact Pattern: `OperationKind.Invocation`

**10 analyzers register for `OperationKind.Invocation`**, which means they are invoked for EVERY method call in the compilation:

1. `UseProperAssertMethodsAnalyzer` (958 lines - largest and most complex)
2. `AssertThrowsShouldContainSingleStatementAnalyzer`
3. `AssertionArgsShouldAvoidConditionalAccessAnalyzer` (3 registrations!)
4. `AssertionArgsShouldBePassedInCorrectOrderAnalyzer`
5. `AvoidAssertAreSameWithValueTypesAnalyzer`
6. `AvoidUsingAssertsInAsyncVoidContextAnalyzer`
7. `DoNotNegateBooleanAssertionAnalyzer`
8. `PreferAssertFailOverAlwaysFalseConditionsAnalyzer`
9. `ReviewAlwaysTrueAssertConditionAnalyzer`
10. `StringAssertToAssertAnalyzer`

**Performance Impact:**
- Called for EVERY method invocation in the compilation
- High frequency, but mitigated by early exit patterns
- Acceptable for test projects (typically smaller than production code)

**Critical Success Factor:** All these analyzers implement early exit checks before expensive analysis:

```csharp
private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol)
{
    var invocationOperation = (IInvocationOperation)context.Operation;
    
    // CRITICAL: Early exit before any expensive work
    if (invocationOperation.TargetMethod.Name is not "IsTrue" and not "IsFalse"
        || !SymbolEqualityComparer.Default.Equals(invocationOperation.TargetMethod.ContainingType, assertSymbol))
    {
        return; // Filter out 99%+ of method calls
    }
    
    // Expensive analysis only runs for Assert.IsTrue/IsFalse calls
    // ...
}
```

### Medium-Impact Pattern: Other `OperationKind` Registrations

These analyzers register for more specific operations, resulting in fewer callbacks:

- `DoNotStoreStaticTestContextAnalyzer`: `OperationKind.SimpleAssignment`
- `IgnoreStringMethodReturnValueAnalyzer`: `OperationKind.ExpressionStatement`
- `TestContextPropertyUsageAnalyzer`: `OperationKind.PropertyReference`
- `TestMethodAttributeShouldSetDisplayNameCorrectlyAnalyzer`: `OperationKind.ObjectCreation`
- `UseCancellationTokenPropertyAnalyzer`: `OperationKind.PropertyReference`

### Low-Impact Pattern: `SymbolKind` Registrations

Most analyzers (~30) register for symbol analysis (`SymbolKind.Method`, `SymbolKind.NamedType`), which is generally more performant than operation analysis.

### Lowest-Impact Pattern: `RegisterCompilationAction`

`UseParallelizeAttributeAnalyzer` uses this pattern - runs once per compilation, checking only assembly-level attributes. Most efficient pattern.

## Performance Best Practices

### Required Patterns (All Analyzers Must Follow)

1. **Enable Concurrent Execution**
   ```csharp
   context.EnableConcurrentExecution();
   ```

2. **Skip Generated Code**
   ```csharp
   context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
   ```

3. **Cache Symbols in CompilationStartAction**
   ```csharp
   context.RegisterCompilationStartAction(context =>
   {
       if (!context.Compilation.TryGetOrCreateTypeByMetadataName(..., out INamedTypeSymbol? symbol))
       {
           return;
       }
       
       // Register symbol/operation actions with cached symbols
       context.RegisterSymbolAction(ctx => Analyze(ctx, symbol), SymbolKind.Method);
   });
   ```

4. **Early Exit Pattern (Critical for Operation Analyzers)**
   ```csharp
   private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol targetSymbol)
   {
       var operation = (IInvocationOperation)context.Operation;
       
       // Check cheapest conditions first
       if (operation.TargetMethod.Name != "ExpectedMethodName")
       {
           return;
       }
       
       // Then check symbol equality
       if (!SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, targetSymbol))
       {
           return;
       }
       
       // Expensive analysis only after all filters
       // ...
   }
   ```

### Recommended Patterns

5. **Prefer Symbol Analysis Over Operation Analysis**
   - Use `RegisterSymbolAction` when possible
   - Only use `RegisterOperationAction` when you need control flow or expression analysis

6. **Prefer Specific OperationKinds**
   - Register for specific operation kinds (e.g., `OperationKind.PropertyReference`)
   - Avoid general operation registration if possible

7. **Order Early Exit Checks by Cost**
   ```csharp
   // Cheap: String comparison
   if (method.Name != "ExpectedName") return;
   
   // Medium: Symbol comparison
   if (!SymbolEqualityComparer.Default.Equals(...)) return;
   
   // Expensive: Walking operations, type checks
   // ... only after filters
   ```

### Anti-Patterns to Avoid

❌ **Don't: Repeat symbol lookups**
```csharp
// BAD: Looking up symbol for every callback
private static void AnalyzeSymbol(SymbolAnalysisContext context)
{
    var assertSymbol = context.Compilation.GetTypeByMetadataName(...); // Expensive!
}
```

✅ **Do: Cache symbols in CompilationStartAction**
```csharp
// GOOD: Look up once, reuse in callbacks
context.RegisterCompilationStartAction(context =>
{
    INamedTypeSymbol? assertSymbol = context.Compilation.GetTypeByMetadataName(...);
    context.RegisterSymbolAction(ctx => AnalyzeSymbol(ctx, assertSymbol), ...);
});
```

❌ **Don't: Register for `OperationKind.Invocation` without early exits**
```csharp
// BAD: Expensive work before checking if this is the right method
private static void AnalyzeInvocation(OperationAnalysisContext context)
{
    var op = (IInvocationOperation)context.Operation;
    // Expensive analysis before checking method name
    var analysis = ExpensiveAnalysis(op);
    if (op.TargetMethod.Name == "Assert") // Too late!
    {
        // ...
    }
}
```

❌ **Don't: Allocate unnecessarily in hot paths**
```csharp
// BAD: Creating collections before filtering
private static void AnalyzeSymbol(SymbolAnalysisContext context)
{
    var results = new List<Diagnostic>(); // Allocation before early exit!
    if (context.Symbol.Name != "Expected") return; // Should be first!
}
```

## Performance Monitoring

### Recommended Metrics

1. **Analyzer Execution Time**
   - Track in CI/CD: Total analyzer execution time per build
   - Set thresholds (e.g., warn if > 5 seconds, fail if > 10 seconds)

2. **Per-Analyzer Metrics**
   - Use Visual Studio's "Analyze Analyzer Performance" feature
   - Profile with realistic test projects (1000+ test methods)

3. **Build Time Impact**
   - Monitor total build time trends
   - Investigate spikes after analyzer changes

### Profiling Tools

- **Visual Studio**: Tools → Options → Text Editor → C# → Advanced → Enable "Analyze Analyzer Performance"
- **Command Line**: `dotnet build /p:ReportAnalyzer=true`
- **BenchmarkDotNet**: For micro-benchmarking specific analyzer logic

## When to Optimize

### ✅ Optimize When:
- Profiling shows a specific analyzer taking > 10% of analysis time
- Users report slow builds
- CI builds exceed time budgets
- Adding new invocation-based analyzers

### ❌ Don't Optimize When:
- No performance issues reported
- Profiling shows good performance
- Optimization would significantly reduce code clarity

### Trade-offs

**Consolidating Multiple Analyzers:**
- **Pros**: Fewer callbacks, potentially faster
- **Cons**: Harder to maintain, test, and understand
- **Recommendation**: Only consider if profiling shows actual issues

**Adding More Invocation-Based Analyzers:**
- **Consider**: Can this be a symbol-based analyzer instead?
- **If necessary**: Ensure early exit is the first thing checked
- **Document**: Add performance comments explaining the trade-off

## Conclusion

The current MSTest analyzers are well-optimized for their use case. The key to maintaining good performance is:

1. **Follow the required patterns** (concurrent execution, symbol caching, early exits)
2. **Be mindful of invocation-based analyzers** - they have the highest impact
3. **Document performance-sensitive code** - help future maintainers
4. **Monitor and profile** - measure before optimizing

When in doubt, prioritize code clarity and maintainability over micro-optimizations. The patterns currently used are sufficient for test projects.
