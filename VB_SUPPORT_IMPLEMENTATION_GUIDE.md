# VB.NET Support Implementation Guide

This document outlines the changes made to add VB.NET support to MSTest analyzers and provides guidance for extending this support to remaining code fix providers.

## What Was Implemented

### 1. VB Test Infrastructure
- Created `VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>` infrastructure
- Added `VisualBasicVerifierHelper` for VB-specific compiler settings
- Established pattern for VB test case creation

### 2. VB Code Fix Provider Support
The following code fix providers have been updated to support VB.NET:

- ✅ **TestContextShouldBeValidFixer** - Complete rewrite using language-agnostic APIs
- ✅ **AssemblyInitializeShouldBeValidFixer** - Added VB language support
- ✅ **UseAttributeOnTestMethodFixer** - Added VB language support  
- ✅ **TestClassShouldBeValidFixer** - Added VB language support

### 3. VB Test Coverage Added
- **TestContextShouldBeValidAnalyzer** - 4 comprehensive VB tests
- **TestMethodShouldBeValidAnalyzer** - 3 VB tests  
- **TestClassShouldBeValidAnalyzer** - 3 VB tests

## Remaining Work

### Code Fix Providers Still Needing VB Support
The following 19 code fix providers still only support C# and need to be updated:

1. AddTestClassFixer.cs
2. AssemblyCleanupShouldBeValidFixer.cs
3. AssertionArgsShouldAvoidConditionalAccessFixer.cs
4. AssertionArgsShouldBePassedInCorrectOrderFixer.cs
5. AvoidAssertAreSameWithValueTypesFixer.cs
6. AvoidExpectedExceptionAttributeFixer.cs
7. ClassCleanupShouldBeValidFixer.cs
8. ClassInitializeShouldBeValidFixer.cs
9. PreferAssertFailOverAlwaysFalseConditionsFixer.cs
10. PreferConstructorOverTestInitializeFixer.cs
11. PreferDisposeOverTestCleanupFixer.cs
12. PreferTestCleanupOverDisposeFixer.cs
13. PreferTestInitializeOverConstructorFixer.cs
14. PublicMethodShouldBeTestMethodFixer.cs
15. TestCleanupShouldBeValidFixer.cs
16. TestInitializeShouldBeValidFixer.cs
17. TestMethodShouldBeValidCodeFix.cs
18. UseNewerAssertThrowsFixer.cs
19. UseProperAssertMethodsFixer.cs

### How to Add VB Support to Code Fix Providers

#### Simple Cases (Attribute-based fixes using DocumentEditor)
For code fix providers that already use `DocumentEditor` or simple attribute additions:

1. Update the `ExportCodeFixProvider` attribute:
```csharp
// From:
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MyFixer))]

// To:
[ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = nameof(MyFixer))]
```

#### Complex Cases (Syntax-specific operations)
For code fix providers using C#-specific syntax trees (like most remaining ones):

1. Remove C#-specific using statements:
```csharp
// Remove these:
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
```

2. Replace C#-specific syntax operations with language-agnostic alternatives:
```csharp
// Instead of C#-specific syntax operations, use:
DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken);
SyntaxGenerator generator = editor.Generator;

// Use generator methods for creating syntax nodes:
var propertyDeclaration = generator.PropertyDeclaration(
    name: "PropertyName",
    type: generator.TypeExpression(typeSymbol),
    accessibility: Accessibility.Public,
    getAccessorStatements: null, // Auto-property
    setAccessorStatements: null);
```

3. Follow the pattern established in `TestContextShouldBeValidFixer` for language-agnostic implementation.

### Adding VB Test Coverage

For each analyzer test file:

1. Add VB verifier import:
```csharp
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.YourAnalyzer,
    MSTest.Analyzers.YourFixer>;
```

2. Add VB test methods covering key scenarios:
```csharp
[TestMethod]
public async Task WhenCondition_VB_ExpectedResult()
{
    string code = """
        Imports Microsoft.VisualStudio.TestTools.UnitTesting
        
        <TestClass>
        Public Class MyTestClass
            ' VB.NET test code here
        End Class
        """;
        
    await VerifyVB.VerifyAnalyzerAsync(code, /* expected diagnostics */);
}
```

## VB.NET Syntax Reference

Key VB.NET syntax differences for test cases:

- **Imports** instead of `using`
- **Attributes**: `<TestClass>`, `<TestMethod>` 
- **Classes**: `Public Class MyClass ... End Class`
- **Methods**: `Public Sub MyMethod() ... End Sub`
- **Properties**: `Public Property MyProp As String`
- **Static**: `Shared` instead of `static`
- **Modules**: `Public Module MyModule ... End Module` (equivalent to static class)
- **Comments**: `'` instead of `//`

## Benefits of This Implementation

1. **Complete VB Test Coverage**: Ensures analyzers work correctly with VB.NET code
2. **Working VB Code Fixes**: Key scenarios now have functional code fix support for VB
3. **Extensible Pattern**: Clear pattern established for adding VB support to remaining fixers
4. **Language-Agnostic Architecture**: Uses Roslyn's language-agnostic APIs for better maintainability

## Next Steps

1. Apply the pattern to remaining code fix providers
2. Add comprehensive VB test coverage to remaining analyzer test files
3. Consider adding VB integration tests for end-to-end scenarios