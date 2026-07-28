// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Parses <c>[DataRow(...)]</c> applications on a test method into flat <see cref="DataRowModel"/> rows the
/// emitter can consume.
/// </summary>
internal static class DataRowBuilder
{
    // Walks the attribute list and reifies each [DataRow(...)] application into a flat
    // object?[] row. Mirrors DataRowAttribute's runtime behavior: when the constructor uses
    // the variadic overload (object? data1, params object?[] moreData), Roslyn surfaces the
    // tail as a single Array TypedConstant, which we flatten back so the consumer sees the
    // same shape as DataRowAttribute.Data.
    internal static EquatableArray<DataRowModel> BuildDataRows(ImmutableArray<AttributeData> attributes)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return EquatableArray<DataRowModel>.Empty;
        }

        ImmutableArray<DataRowModel>.Builder builder = ImmutableArray.CreateBuilder<DataRowModel>();
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            if (attributeClass.ToDisplayString(SymbolDisplayFormats.FullyQualified) != "global::" + MSTestAttributeNames.DataRow)
            {
                continue;
            }

            ImmutableArray<TypedConstant> ctorArgs = attribute.ConstructorArguments;
            ImmutableArray<TypedConstantModel>.Builder rowBuilder = ImmutableArray.CreateBuilder<TypedConstantModel>();

            bool lastIsParamsArray =
                attribute.AttributeConstructor is { Parameters: { IsDefaultOrEmpty: false } parameters }
                && parameters[parameters.Length - 1].IsParams
                && !ctorArgs.IsDefaultOrEmpty
                && ctorArgs[ctorArgs.Length - 1].Kind == TypedConstantKind.Array;

            for (int i = 0; i < ctorArgs.Length; i++)
            {
                if (i == ctorArgs.Length - 1 && lastIsParamsArray)
                {
                    foreach (TypedConstant element in ctorArgs[i].Values)
                    {
                        rowBuilder.Add(AttributeMaterializationHelper.ToModel(element));
                    }
                }
                else
                {
                    rowBuilder.Add(AttributeMaterializationHelper.ToModel(ctorArgs[i]));
                }
            }

            builder.Add(new DataRowModel(new EquatableArray<TypedConstantModel>(rowBuilder.ToImmutable())));
        }

        return new EquatableArray<DataRowModel>(builder.ToImmutable());
    }
}
