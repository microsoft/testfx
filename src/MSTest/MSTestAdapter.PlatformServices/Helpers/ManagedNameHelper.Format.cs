// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

internal static partial class ManagedNameHelper
{
    private static void AppendTypeString(StringBuilder b, Type? type, bool closedType)
    {
        if (type is null)
        {
            return;
        }

        if (type.IsArray)
        {
            AppendTypeString(b, type.GetElementType(), closedType);
            b.Append('[');
            for (int i = 0; i < type.GetArrayRank() - 1; i++)
            {
                b.Append(',');
            }

            b.Append(']');
        }
        else if (type.IsGenericParameter)
        {
            if (type.DeclaringMethod is not null)
            {
                b.Append('!');
            }

            b.Append('!');
            b.Append(type.GenericParameterPosition);
        }
        else
        {
            if (type.Namespace is not null)
            {
                AppendNamespace(b, type.Namespace);
                b.Append('.');
            }

            AppendNestedTypeName(b, type);
            if (closedType)
            {
                AppendGenericTypeParameters(b, type);
            }
        }
    }

    private static void AppendNamespace(StringBuilder b, string? namespaceString)
    {
        if (namespaceString is null)
        {
            return;
        }

        int start = 0;
        bool shouldEscape = false;

        for (int i = 0; i <= namespaceString.Length; i++)
        {
            if (i == namespaceString.Length || namespaceString[i] == '.')
            {
                if (start != 0)
                {
                    b.Append('.');
                }

                string part = namespaceString.Substring(start, i - start);
                if (shouldEscape)
                {
                    NormalizeAndAppendString(b, part);
                    shouldEscape = false;
                }
                else
                {
                    b.Append(part);
                }

                start = i + 1;
                continue;
            }

            shouldEscape = shouldEscape || NeedsEscaping(namespaceString[i], i - start);
        }
    }

    private static void AppendMethodString(StringBuilder methodBuilder, string name, int methodArity)
    {
        int arityStart = name.LastIndexOf('`');
        int arity = 0;
        if (arityStart > 0)
        {
            arityStart++;
            string arityString = name.Substring(arityStart);
            if (int.TryParse(arityString, out arity))
            {
                if (arity == methodArity)
                {
                    name = name.Substring(0, arityStart - 1);
                }
            }
        }

        if (IsNormalized(name))
        {
            methodBuilder.Append(name);
        }
        else
        {
            NormalizeAndAppendString(methodBuilder, name);
        }

        if (arity > 0 && methodArity == arity)
        {
#if NET6_0_OR_GREATER
            methodBuilder.Append(CultureInfo.InvariantCulture, $"`{arity}");
#else
            methodBuilder.Append($"`{arity}");
#endif
        }
    }

    private static int AppendNestedTypeName(StringBuilder b, Type? type)
    {
        if (type is null)
        {
            return 0;
        }

        int outerArity = 0;
        if (type.IsNested)
        {
            outerArity = AppendNestedTypeName(b, type.DeclaringType);
            b.Append('+');
        }

        string typeName = type.Name;
        int stars = 0;
        if (type.IsPointer)
        {
            for (int i = typeName.Length - 1; i > 0; i--)
            {
                if (typeName[i] != '*')
                {
                    stars = typeName.Length - i - 1;
                    typeName = typeName.Substring(0, i + 1);
                    break;
                }
            }
        }

        TypeInfo info = type.GetTypeInfo();
        int arity = !info.IsGenericType
                  ? 0
                  : info.GenericTypeParameters.Length > 0
                    ? info.GenericTypeParameters.Length
                    : info.GenericTypeArguments.Length;

        AppendMethodString(b, typeName, arity - outerArity);
        b.Append('*', stars);
        return arity;
    }

    private static void AppendGenericTypeParameters(StringBuilder b, Type type)
    {
        Type[] genericArguments = type.GetGenericArguments();
        AppendGenericArguments(b, genericArguments);
    }

    private static void AppendGenericArguments(StringBuilder b, Type[] genericArguments)
    {
        if (genericArguments.Length != 0)
        {
            b.Append('<');
            foreach (Type argType in genericArguments)
            {
                AppendTypeString(b, argType, closedType: true);
                b.Append(',');
            }

            // Replace the last ',' with '>'
            b[b.Length - 1] = '>';
        }
    }

    private static string GetTypeString(Type type, bool closedType)
    {
        var builder = new StringBuilder();
        AppendTypeString(builder, type, closedType);
        return builder.ToString();
    }
}
