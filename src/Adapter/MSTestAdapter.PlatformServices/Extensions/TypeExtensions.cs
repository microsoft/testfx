// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

internal static class TypeExtensions
{
    private static readonly ConcurrentDictionary<Type, bool> FSharpModules = new();

    internal static bool IsFSharpModule(this Type type)
        => FSharpModules.GetOrAdd(type, static type =>
        {
            if (!type.IsClass)
            {
                return false;
            }

            // Inspect compiler metadata without referencing or instantiating FSharp.Core attributes.
            foreach (CustomAttributeData attribute in type.GetCustomAttributesData())
            {
                if (attribute.AttributeType.FullName == "Microsoft.FSharp.Core.CompilationMappingAttribute"
                    && attribute.AttributeType.Assembly.GetName().Name == "FSharp.Core"
                    && attribute.ConstructorArguments.Count > 0
                    && attribute.ConstructorArguments[0].Value is int flags
                    // SourceConstructFlags.KindMask = 31; SourceConstructFlags.Module = 7.
                    && (flags & 31) == 7)
                {
                    return true;
                }
            }

            return false;
        });
}
