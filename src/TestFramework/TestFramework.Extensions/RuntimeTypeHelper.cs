// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides method signature discovery for generic methods.
/// </summary>
internal sealed class RuntimeTypeHelper
{
    /// <summary>
    /// Compares the method signatures of these two methods.
    /// </summary>
    /// <param name="m1">Method1.</param>
    /// <param name="m2">Method2.</param>
    /// <returns>True if they are similar.</returns>
    internal static bool CompareMethodSigAndName(MethodBase m1, MethodBase m2)
    {
        ParameterInfo[] params1 = m1.GetParameters();
        ParameterInfo[] params2 = m2.GetParameters();

        if (params1.Length != params2.Length)
        {
            return false;
        }

        int numParams = params1.Length;
        for (int i = 0; i < numParams; i++)
        {
            if (params1[i].ParameterType != params2[i].ParameterType)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the hierarchy depth from the base type of the provided type.
    /// </summary>
    /// <param name="t">The type.</param>
    /// <returns>The depth.</returns>
    internal static int GetHierarchyDepth(Type t)
    {
        int depth = 0;

        Type currentType = t;
        do
        {
            depth++;
            currentType = currentType.BaseType;
        }
        while (currentType != null);

        return depth;
    }

    /// <summary>
    /// Finds most derived type with the provided information.
    /// </summary>
    /// <param name="match">Candidate matches.</param>
    /// <param name="cMatches">Number of matches.</param>
    /// <returns>The most derived method.</returns>
    internal static MethodBase? FindMostDerivedNewSlotMeth(MethodBase[] match, int cMatches)
    {
        int deepestHierarchy = 0;
        MethodBase? methWithDeepestHierarchy = null;

        for (int i = 0; i < cMatches; i++)
        {
            // Calculate the depth of the hierarchy of the declaring type of the
            // current method.
            int currentHierarchyDepth = GetHierarchyDepth(match[i].DeclaringType);

            // Two methods with the same hierarchy depth are not allowed. This would
            // mean that there are 2 methods with the same name and sig on a given type
            // which is not allowed, unless one of them is vararg...
            if (currentHierarchyDepth == deepestHierarchy)
            {
                if (methWithDeepestHierarchy != null)
                {
                    DebugEx.Assert(
                        ((match[i].CallingConvention & CallingConventions.VarArgs)
                        | (methWithDeepestHierarchy.CallingConvention & CallingConventions.VarArgs)) != 0,
                        $"Calling conventions: {match[i].CallingConvention} - {methWithDeepestHierarchy.CallingConvention}");
                }

                throw new AmbiguousMatchException();
            }

            // Check to see if this method is on the most derived class.
            if (currentHierarchyDepth > deepestHierarchy)
            {
                deepestHierarchy = currentHierarchyDepth;
                methWithDeepestHierarchy = match[i];
            }
        }

        return methWithDeepestHierarchy;
    }

    /// <summary>
    /// Given a set of methods that match the base criteria, select a method based
    /// upon an array of types. This method should return null if no method matches
    /// the criteria.
    /// </summary>
    /// <param name="match">Candidate matches.</param>
    /// <param name="types">Types.</param>
    /// <returns>Matching method. Null if none matches.</returns>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
    internal static MethodBase? SelectMethod(MethodBase[] match, Type[] types)
    {
        Guard.NotNull(match);

        int i;
        int j;

        var realTypes = new Type[types.Length];
        for (i = 0; i < types.Length; i++)
        {
            realTypes[i] = types[i].UnderlyingSystemType;
        }

        types = realTypes;

        // If there are no methods to match to, then return null, indicating that no method
        // matches the criteria
        if (match.Length == 0)
        {
            return null;
        }

        // Find all the methods that can be described by the types parameter.
        // Remove all of them that cannot.
        int curIdx = 0;
        for (i = 0; i < match.Length; i++)
        {
            ParameterInfo[] par = match[i].GetParameters();
            if (par.Length != types.Length)
            {
                continue;
            }

            for (j = 0; j < types.Length; j++)
            {
                Type pCls = par[j].ParameterType;

                if (pCls.ContainsGenericParameters)
                {
                    if (pCls.IsArray != types[j].IsArray)
                    {
                        break;
                    }
                }
                else
                {
                    if (pCls == types[j])
                    {
                        continue;
                    }

                    if (pCls == typeof(object))
                    {
                        continue;
                    }
                    else
                    {
                        if (!pCls.IsAssignableFrom(types[j]))
                        {
                            break;
                        }
                    }
                }
            }

            if (j == types.Length)
            {
                match[curIdx++] = match[i];
            }
        }

        if (curIdx == 0)
        {
            return null;
        }

        if (curIdx == 1)
        {
            return match[0];
        }

        // Walk all of the methods looking the most specific method to invoke
        int currentMin = 0;
        bool ambig = false;
        int[] paramOrder = new int[types.Length];
        for (i = 0; i < types.Length; i++)
        {
            paramOrder[i] = i;
        }

        for (i = 1; i < curIdx; i++)
        {
            int newMin = FindMostSpecificMethod(match[currentMin], paramOrder, null, match[i], paramOrder, null, types, null);
            if (newMin == 0)
            {
                ambig = true;
            }
            else
            {
                if (newMin == 2)
                {
                    ambig = false;
                    currentMin = i;
                }
            }
        }

        return ambig ? throw new AmbiguousMatchException() : match[currentMin];
    }

    /// <summary>
    /// Finds the most specific method in the two methods provided.
    /// </summary>
    /// <param name="m1">Method 1.</param>
    /// <param name="paramOrder1">Parameter order for Method 1.</param>
    /// <param name="paramArrayType1">Parameter array type.</param>
    /// <param name="m2">Method 2.</param>
    /// <param name="paramOrder2">Parameter order for Method 2.</param>
    /// <param name="paramArrayType2">>Parameter array type.</param>
    /// <param name="types">Types to search in.</param>
    /// <param name="args">Args.</param>
    /// <returns>An int representing the match.</returns>
    internal static int FindMostSpecificMethod(
        MethodBase m1,
        int[] paramOrder1,
        Type? paramArrayType1,
        MethodBase m2,
        int[] paramOrder2,
        Type? paramArrayType2,
        Type[] types,
        object?[]? args)
    {
        // Find the most specific method based on the parameters.
        int res = FindMostSpecific(
            m1.GetParameters(),
            paramOrder1,
            paramArrayType1,
            m2.GetParameters(),
            paramOrder2,
            paramArrayType2,
            types,
            args);

        // If the match was not ambiguous then return the result.
        if (res != 0)
        {
            return res;
        }

        // Check to see if the methods have the exact same name and signature.
        if (CompareMethodSigAndName(m1, m2))
        {
            // Determine the depth of the declaring types for both methods.
            int hierarchyDepth1 = GetHierarchyDepth(m1.DeclaringType);
            int hierarchyDepth2 = GetHierarchyDepth(m2.DeclaringType);

            // The most derived method is the most specific one.
            return hierarchyDepth1 == hierarchyDepth2
                ? 0
                : hierarchyDepth1 < hierarchyDepth2
                    ? 2
                    : 1;
        }

        // The match is ambiguous.
        return 0;
    }

    /// <summary>
    /// Finds the most specific method in the two methods provided.
    /// </summary>
    /// <param name="p1">Method 1.</param>
    /// <param name="paramOrder1">Parameter order for Method 1.</param>
    /// <param name="paramArrayType1">Parameter array type.</param>
    /// <param name="p2">Method 2.</param>
    /// <param name="paramOrder2">Parameter order for Method 2.</param>
    /// <param name="paramArrayType2">>Parameter array type.</param>
    /// <param name="types">Types to search in.</param>
    /// <param name="args">Args.</param>
    /// <returns>An int representing the match.</returns>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
    internal static int FindMostSpecific(
        ParameterInfo[] p1,
        int[] paramOrder1,
        Type? paramArrayType1,
        ParameterInfo[] p2,
        int[] paramOrder2,
        Type? paramArrayType2,
        Type[] types,
        object?[]? args)
    {
        // A method using params is always less specific than one not using params
        if (paramArrayType1 != null && paramArrayType2 == null)
        {
            return 2;
        }

        if (paramArrayType2 != null && paramArrayType1 == null)
        {
            return 1;
        }

        bool p1Less = false;
        bool p2Less = false;

        for (int i = 0; i < types.Length; i++)
        {
            if (args != null && args[i] == Type.Missing)
            {
                continue;
            }

            Type c1, c2;

            // If a param array is present, then either
            //      the user re-ordered the parameters in which case
            //          the argument to the param array is either an array
            //              in which case the params is conceptually ignored and so paramArrayType1 == null
            //          or the argument to the param array is a single element
            //              in which case paramOrder[i] == p1.Length - 1 for that element
            //      or the user did not re-order the parameters in which case
            //          the paramOrder array could contain indexes larger than p.Length - 1
            //          so any index >= p.Length - 1 is being put in the param array
            c1 = paramArrayType1 != null && paramOrder1[i] >= p1.Length - 1 ? paramArrayType1 : p1[paramOrder1[i]].ParameterType;

            c2 = paramArrayType2 != null && paramOrder2[i] >= p2.Length - 1 ? paramArrayType2 : p2[paramOrder2[i]].ParameterType;

            if (c1 == c2)
            {
                continue;
            }

            if (c1.ContainsGenericParameters || c2.ContainsGenericParameters)
            {
                continue;
            }

            switch (FindMostSpecificType(c1, c2, types[i]))
            {
                case 0:
                    return 0;
                case 1:
                    p1Less = true;
                    break;
                case 2:
                    p2Less = true;
                    break;
            }
        }

        // Two way p1Less and p2Less can be equal.  All the arguments are the
        //  same they both equal false, otherwise there were things that both
        //  were the most specific type on....
        if (p1Less == p2Less)
        {
            // it's possible that the 2 methods have same sig and  default param in which case we match the one
            // with the same number of args but only if they were exactly the same (that is p1Less and p2Lees are both false)
            if (!p1Less && p1.Length != p2.Length && args != null)
            {
                if (p1.Length == args.Length)
                {
                    return 1;
                }
                else if (p2.Length == args.Length)
                {
                    return 2;
                }
            }

            return 0;
        }
        else
        {
            return p1Less ? 1 : 2;
        }
    }

    /// <summary>
    /// Finds the most specific type in the two provided.
    /// </summary>
    /// <param name="c1">Type 1.</param>
    /// <param name="c2">Type 2.</param>
    /// <param name="t">The defining type.</param>
    /// <returns>An int representing the match.</returns>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
    internal static int FindMostSpecificType(Type c1, Type c2, Type t)
    {
        // If the two types are exact move on...
        if (c1 == c2)
        {
            return 0;
        }

        if (c1 == t)
        {
            return 1;
        }

        if (c2 == t)
        {
            return 2;
        }

        bool c1FromC2;
        bool c2FromC1;

        if (c1.IsByRef || c2.IsByRef)
        {
            if (c1.IsByRef && c2.IsByRef)
            {
                c1 = c1.GetElementType();
                c2 = c2.GetElementType();
            }
            else if (c1.IsByRef)
            {
                if (c1.GetElementType() == c2)
                {
                    return 2;
                }

                c1 = c1.GetElementType();
            }
            else
            {
                if (c2.GetElementType() == c1)
                {
                    return 1;
                }

                c2 = c2.GetElementType();
            }
        }

        if (c1.IsPrimitive && c2.IsPrimitive)
        {
            c1FromC2 = true;
            c2FromC1 = true;
        }
        else
        {
            c1FromC2 = c1.IsAssignableFrom(c2);
            c2FromC1 = c2.IsAssignableFrom(c1);
        }

        return c1FromC2 == c2FromC1
            ? 0
            : c1FromC2
                ? 2
                : 1;
    }
}
#endif
