// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.RoslynAnalyzerHelpers;

internal static class TypeAssignabilityChecker
{
    public static bool IsAssignableTo(
        [NotNullWhen(returnValue: true)] this ITypeSymbol? fromSymbol,
        [NotNullWhen(returnValue: true)] ITypeSymbol? toSymbol,
        Compilation compilation)
        => fromSymbol != null && toSymbol != null && compilation.ClassifyCommonConversion(fromSymbol, toSymbol).IsImplicit;
}
