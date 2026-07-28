// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This class represents the live NON public INTERNAL object in the system.
/// </summary>
public partial class PrivateObject
{
    #region Private Helpers

    /// <summary>
    /// Validate access string.
    /// </summary>
    /// <param name="access"> access string.</param>
    private static void ValidateAccessString(string access)
    {
        _ = access ?? throw new ArgumentNullException(nameof(access));
        if (access.Length == 0)
        {
            throw new ArgumentException(FrameworkMessages.AccessStringInvalidSyntax);
        }

        string[] arr = access.Split('.');
        foreach (string str in arr)
        {
            if ((str.Length == 0) || (str.IndexOfAny([' ', '\t', '\n']) != -1))
            {
                throw new ArgumentException(FrameworkMessages.AccessStringInvalidSyntax);
            }
        }
    }

    private void BuildGenericMethodCacheForType(Type t)
    {
        DebugEx.Assert(t != null, "type should not be null.");
        GenericMethodCache = [];

        MethodInfo[] members = t.GetMethods(BindToEveryThing);

        foreach (MethodInfo member in members)
        {
            if (member is { IsGenericMethod: false, IsGenericMethodDefinition: false })
            {
                continue;
            }

            // automatically initialized to null
            if (!GenericMethodCache.TryGetValue(member.Name, out LinkedList<MethodInfo> listByName))
            {
                listByName = new LinkedList<MethodInfo>();
                GenericMethodCache.Add(member.Name, listByName);
            }

            DebugEx.Assert(listByName != null, "list should not be null.");
            listByName.AddLast(member);
        }
    }

    /// <summary>
    /// Extracts the most appropriate generic method signature from the current private type.
    /// </summary>
    /// <param name="methodName">The name of the method in which to search the signature cache.</param>
    /// <param name="parameterTypes">An array of types corresponding to the types of the parameters in which to search.</param>
    /// <param name="typeArguments">An array of types corresponding to the types of the generic arguments.</param>
    /// <param name="bindingFlags"><see cref="BindingFlags"/> to further filter the method signatures.</param>
    /// <returns>A method info instance.</returns>
    private MethodInfo? GetGenericMethodFromCache(string methodName, Type[] parameterTypes, Type[] typeArguments, BindingFlags bindingFlags)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(methodName), "Invalid method name.");
        DebugEx.Assert(parameterTypes != null, "Invalid parameter type array.");
        DebugEx.Assert(typeArguments != null, "Invalid type arguments array.");

        // Build a preliminary list of method candidates that contain roughly the same signature.
        LinkedList<MethodInfo> methodCandidates = GetMethodCandidates(methodName, parameterTypes, typeArguments, bindingFlags);

        // Search of ambiguous methods (methods with the same signature).
        var finalCandidates = new MethodInfo[methodCandidates.Count];
        methodCandidates.CopyTo(finalCandidates, 0);

        // parameterTypes is only asserted non-null in debug builds (see above), so the null check is kept as a defensive guard for release builds.
        if (parameterTypes == null || parameterTypes.Length != 0)
        {
            // Now that we have a preliminary list of candidates, select the most appropriate one.
            return RuntimeTypeHelper.SelectMethod(finalCandidates, parameterTypes!) as MethodInfo;
        }

        for (int i = 0; i < finalCandidates.Length; i++)
        {
            MethodInfo methodInfo = finalCandidates[i];

            if (!RuntimeTypeHelper.CompareMethodSigAndName(methodInfo, finalCandidates[0]))
            {
                throw new AmbiguousMatchException();
            }
        }

        // All the methods have the exact same name and sig so return the most derived one.
        return RuntimeTypeHelper.FindMostDerivedNewSlotMeth(finalCandidates, finalCandidates.Length) as MethodInfo;
    }

    private LinkedList<MethodInfo> GetMethodCandidates(string methodName, Type[] parameterTypes, Type[] typeArguments, BindingFlags bindingFlags)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(methodName), "methodName should not be null.");
        DebugEx.Assert(parameterTypes != null, "parameterTypes should not be null.");
        DebugEx.Assert(typeArguments != null, "typeArguments should not be null.");

        LinkedList<MethodInfo> methodCandidates = new();

        if (!GenericMethodCache.TryGetValue(methodName, out LinkedList<MethodInfo>? methods))
        {
            return methodCandidates;
        }

        DebugEx.Assert(methods != null, "methods should not be null.");

        foreach (MethodInfo candidate in methods)
        {
            bool paramMatch = true;
            Type[] genericArgs = candidate.GetGenericArguments();
            if (genericArgs.Length != typeArguments.Length)
            {
                continue;
            }

            // Since we can't just get the correct MethodInfo from Reflection,
            // we will just match the number of parameters, their order, and their type
            MethodInfo methodCandidate = candidate;
            ParameterInfo[] candidateParams = methodCandidate.GetParameters();

            if (candidateParams.Length != parameterTypes.Length)
            {
                continue;
            }

            if ((bindingFlags & BindingFlags.ExactBinding) == 0)
            {
                methodCandidates.AddLast(methodCandidate);
                continue;
            }

            // Exact binding
            int i = 0;

            foreach (ParameterInfo candidateParam in candidateParams)
            {
                Type sourceParameterType = parameterTypes[i++];
                if (candidateParam.ParameterType.ContainsGenericParameters)
                {
                    // Since we have a generic parameter here, just make sure the IsArray matches.
                    if (candidateParam.ParameterType.IsArray != sourceParameterType.IsArray)
                    {
                        paramMatch = false;
                        break;
                    }
                }
                else
                {
                    if (candidateParam.ParameterType != sourceParameterType)
                    {
                        paramMatch = false;
                        break;
                    }
                }
            }

            if (paramMatch)
            {
                methodCandidates.AddLast(methodCandidate);
                continue;
            }
        }

        return methodCandidates;
    }

    #endregion
}
#endif
