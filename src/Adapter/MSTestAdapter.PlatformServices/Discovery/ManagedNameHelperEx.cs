// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

internal static class ManagedNameHelperEx
{
    internal static void GetManagedName(MethodBase method, out string managedTypeName, out string managedMethodName)
    {
        Type semanticType = method.ReflectedType ?? throw ApplicationStateGuard.Unreachable();

        if (semanticType.IsGenericType)
        {
            // The type might have some of its generic parameters specified, so make
            // sure we are working with the open form of the generic type.
            semanticType = semanticType.GetGenericTypeDefinition();

            // The method might have some of its parameters specified by the original closed type
            // declaration. Here we use the method handle (basically metadata token) to create
            // a new method reference using the open form of the reflected type. The intent is
            // to strip all generic type parameters.
            RuntimeMethodHandle methodHandle = method.MethodHandle;
            method = MethodBase.GetMethodFromHandle(methodHandle, semanticType.TypeHandle)!;
        }

        if (method.IsGenericMethod)
        {
            // If this method is generic, then convert to the generic method definition
            // so that we get the open generic type definitions for parameters.
            method = ((MethodInfo)method).GetGenericMethodDefinition();
        }

        var typeBuilder = new StringBuilder();
        var methodBuilder = new StringBuilder();

        AppendTypeString(typeBuilder, semanticType, closedType: false);

        // Method Name with method arity
        int arity = method.GetGenericArguments().Length;
        AppendMethodString(methodBuilder, method.Name, arity);
        if (arity > 0)
        {
            methodBuilder.Append('`');
            methodBuilder.Append(arity);
        }

        // Type Parameters
        ParameterInfo[] paramList = method.GetParameters();
        if (paramList.Length != 0)
        {
            methodBuilder.Append('(');
            foreach (ParameterInfo p in paramList)
            {
                // closedType is always true here by RFC
                AppendTypeString(methodBuilder, p.ParameterType, closedType: true);
                methodBuilder.Append(',');
            }

            // Replace the last ',' with ')'
            methodBuilder[methodBuilder.Length - 1] = ')';
        }

        managedTypeName = typeBuilder.ToString();
        managedMethodName = methodBuilder.ToString();
    }

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
            if (type.DeclaringMethod != null)
            {
                b.Append('!');
            }

            b.Append('!');
            b.Append(type.GenericParameterPosition);
        }
        else
        {
            if (type.Namespace != null)
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

    private static void AppendGenericTypeParameters(StringBuilder b, Type type)
    {
        Type[] genericArguments = type.GetGenericArguments();
        AppendGenericArguments(b, genericArguments);
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

#if NET6_0_OR_GREATER
                ReadOnlySpan<char> part = namespaceString.AsSpan().Slice(start, i - start);
#else
                string part = namespaceString.Substring(start, i - start);
#endif
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

    private static bool IsNormalized(string s)
    {
        int brackets = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (NeedsEscaping(c, i) && c != '.')
            {
                if (i != 0)
                {
                    if (c == '<')
                    {
                        brackets++;
                        continue;
                    }

                    if (c == '>' && s[i - 1] != '<' && brackets > 0)
                    {
                        brackets--;
                        continue;
                    }
                }

                return false;
            }
        }

        return brackets == 0;
    }

    private static void NormalizeAndAppendString(
        StringBuilder b,
#if NET6_0_OR_GREATER
        ReadOnlySpan<char> name)
#else
        string name)
#endif
    {
        b.Append('\'');
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (NeedsEscaping(c, i))
            {
                if (c is '\\' or '\'')
                {
                    // var encoded = Convert.ToString(((uint)c), 16);
                    // b.Append("\\u");
                    // b.Append('0', 4 - encoded.Length);
                    // b.Append(encoded);
                    b.Append('\\');
                    b.Append(c);
                    continue;
                }
            }

            b.Append(c);
        }

        b.Append('\'');
    }

    private static bool NeedsEscaping(char c, int pos)
    {
        if (pos == 0 && char.IsDigit(c))
        {
            return true;
        }

        if (c == '_'
            // 'Digit' does not include letter numbers, which are valid identifiers as per docs https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names'.
            // Lu, Ll, Lt, Lm, Lo, or Nd
            || char.IsLetterOrDigit(c))
        {
            return false;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category
            is not UnicodeCategory.LetterNumber // Nl
            and not UnicodeCategory.NonSpacingMark // Mn
            and not UnicodeCategory.SpacingCombiningMark // Mc
            and not UnicodeCategory.ConnectorPunctuation // Pc
            and not UnicodeCategory.Format; // Cf
    }
}
