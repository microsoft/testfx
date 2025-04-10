// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Framework.SourceGeneration.ObjectModels;

public sealed class TestMethodParametersInfo
{
    public TestMethodParametersInfo(ImmutableArray<IParameterSymbol> parameters)
    {
        Parameters = parameters
            .Select(p => (p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
            .ToImmutableArray();
        ParametersTuple = BuildParametersTupleString(Parameters);
        ParametersMethodIdentifierFullyQualifiedTypes = parameters.Select(p => p.Type.ToDisplayString(TestMethods.MethodIdentifierFullyQualifiedTypeFormat)).ToImmutableArray();
    }

    public ImmutableArray<(string Name, string FullyQualifiedType)> Parameters { get; }

    public string? ParametersTuple { get; }

    public ImmutableArray<string> ParametersMethodIdentifierFullyQualifiedTypes { get; }

    private static string? BuildParametersTupleString(ImmutableArray<(string Name, string FullyQualifiedType)> parameters)
    {
        if (parameters.IsDefaultOrEmpty)
        {
            return null;
        }

        if (parameters.Length == 1)
        {
            return parameters[0].FullyQualifiedType;
        }

        var tupleTypeStringBuilder = new StringBuilder("(");

        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                tupleTypeStringBuilder.Append(", ");
            }

            tupleTypeStringBuilder.Append(parameters[i].FullyQualifiedType).Append(' ').Append(parameters[i].Name);
        }

        return tupleTypeStringBuilder.Append(')').ToString();
    }
}
