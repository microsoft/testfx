// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class SymbolReferenceabilityHelperTests
{
    [TestMethod]
    public void IsTypeReferenceableFrom_FileLocalType_ReturnsFalseEvenInTheSameAssembly()
    {
        CSharpCompilation compilation = RoslynTestHelpers.CreateCompilation(
            """
            file class Hidden { }
            internal class Visible { }
            """);

        SymbolReferenceabilityHelper.IsTypeReferenceableFrom(compilation.GetSourceType("Visible"), compilation.Assembly).Should().BeTrue();
        SymbolReferenceabilityHelper.IsTypeReferenceableFrom(compilation.GetSourceType("Hidden"), compilation.Assembly).Should().BeFalse();
    }

    [TestMethod]
    public void IsTypeReferenceableFrom_TypeParameterAndDynamicTypeArguments_AreReferenceable()
    {
        CSharpCompilation compilation = RoslynTestHelpers.CreateCompilation(
            """
            public class Box<T> { }
            public class Holder { public Box<dynamic> Field; }
            """);
        INamedTypeSymbol openBox = compilation.GetSourceType("Box");
        INamedTypeSymbol boxOfDynamic = GetFieldType(compilation, "Holder", "Field");

        openBox.TypeArguments.Single().Should().BeAssignableTo<ITypeParameterSymbol>();
        boxOfDynamic.TypeArguments.Single().Should().BeAssignableTo<IDynamicTypeSymbol>();
        SymbolReferenceabilityHelper.IsTypeReferenceableFrom(openBox, compilation.Assembly).Should().BeTrue();
        SymbolReferenceabilityHelper.IsTypeReferenceableFrom(boxOfDynamic, compilation.Assembly).Should().BeTrue();
    }

    [TestMethod]
    public void ContainsTypeParameter_DetectsUnboundGenericsAndTypeParametersAtAnyDepth()
    {
        CSharpCompilation compilation = RoslynTestHelpers.CreateCompilation(
            """
            public class Box<T> { }
            public class Closed
            {
                public Box<int> Flat;
                public Box<Box<int>> Nested;
            }
            public class Open<T>
            {
                public Box<T> Flat;
                public Box<Box<T>> Nested;
            }
            """);
        INamedTypeSymbol unboundBox = compilation.GetSourceType("Box").ConstructUnboundGenericType();

        unboundBox.IsUnboundGenericType.Should().BeTrue();
        SymbolReferenceabilityHelper.ContainsTypeParameter(unboundBox).Should().BeTrue();
        SymbolReferenceabilityHelper.ContainsTypeParameter(GetFieldType(compilation, "Open", "Flat")).Should().BeTrue();
        SymbolReferenceabilityHelper.ContainsTypeParameter(GetFieldType(compilation, "Open", "Nested")).Should().BeTrue();
        SymbolReferenceabilityHelper.ContainsTypeParameter(GetFieldType(compilation, "Closed", "Flat")).Should().BeFalse();
        SymbolReferenceabilityHelper.ContainsTypeParameter(GetFieldType(compilation, "Closed", "Nested")).Should().BeFalse();
    }

    [TestMethod]
    public void IsClosedReferenceableType_RequiresNoOpenTypeParameterAndAReferenceableType()
    {
        CSharpCompilation compilation = RoslynTestHelpers.CreateCompilation(
            """
            public class Box<T> { }
            public class Closed { public Box<int> Flat; }
            public class Open<T> { public Box<T> Flat; }
            file class Hidden { }
            public class UsesHidden { public Box<Hidden> Flat; }
            """);

        SymbolReferenceabilityHelper.IsClosedReferenceableType(GetFieldType(compilation, "Closed", "Flat"), compilation.Assembly).Should().BeTrue();
        SymbolReferenceabilityHelper.IsClosedReferenceableType(GetFieldType(compilation, "Open", "Flat"), compilation.Assembly).Should().BeFalse();
        SymbolReferenceabilityHelper.IsClosedReferenceableType(compilation.GetSourceType("Box").ConstructUnboundGenericType(), compilation.Assembly).Should().BeFalse();
        SymbolReferenceabilityHelper.IsClosedReferenceableType(GetFieldType(compilation, "UsesHidden", "Flat"), compilation.Assembly).Should().BeFalse();
    }

    private static INamedTypeSymbol GetFieldType(CSharpCompilation compilation, string typeName, string fieldName)
        => (INamedTypeSymbol)((IFieldSymbol)compilation.GetSourceType(typeName).GetMembers(fieldName).Single()).Type;
}
