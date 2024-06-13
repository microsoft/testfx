// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal static class DiaSessionOperations
{
    private static MethodInfo? s_methodGetNavigationData;
    private static PropertyInfo? s_propertyFileName;
    private static PropertyInfo? s_propertyMinLineNumber;
    private static Type? s_typeDiaSession;
    private static Type? s_typeDiaNavigationData;

    /// <summary>
    /// Initializes static members of the <see cref="DiaSessionOperations"/> class.
    /// </summary>
    /// <remarks>Initializes DiaSession.</remarks>
    static DiaSessionOperations()
    {
#if NET8_0_OR_GREATER
        if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException("Dia is not allowed when dynamic code is not supported");
        }
#endif

        const string diaSessionTypeName = "Microsoft.VisualStudio.TestPlatform.ObjectModel.DiaSession, Microsoft.VisualStudio.TestPlatform.ObjectModel";
        const string diaNavigationDataTypeName = "Microsoft.VisualStudio.TestPlatform.ObjectModel.DiaNavigationData,  Microsoft.VisualStudio.TestPlatform.ObjectModel";

        Initialize(diaSessionTypeName, diaNavigationDataTypeName);
    }

    /// <summary>
    /// Creates a Navigation session for the source file.
    /// This is used to get file path and line number information for its components.
    /// </summary>
    /// <param name="source"> The source file. </param>
    /// <returns> A Navigation session instance for the current platform. </returns>
    internal static object? CreateNavigationSession(string source)
    {
        // Create instance only when DiaSession is found in Object Model.
        if (s_typeDiaSession != null && s_typeDiaNavigationData != null)
        {
            string messageFormatOnException = string.Join("MSTestDiscoverer:DiaSession: Could not create diaSession for source:", source, ". Reason:{0}");
#pragma warning disable IL2077 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
            return SafeInvoke(() => Activator.CreateInstance(s_typeDiaSession, source));
#pragma warning restore IL2077 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
        }

        return null;
    }

    /// <summary>
    /// Gets the navigation data for a navigation session.
    /// </summary>
    /// <param name="navigationSession"> The navigation session. </param>
    /// <param name="className"> The class name. </param>
    /// <param name="methodName"> The method name. </param>
    /// <param name="minLineNumber"> The min line number. </param>
    /// <param name="fileName"> The file name. </param>
    internal static void GetNavigationData(object? navigationSession, string className, string methodName, out int minLineNumber, out string? fileName)
    {
        // Set default values.
        fileName = null;
        minLineNumber = -1;

        // Get navigation data only when DiaSession is found in Object Model.
        if (s_typeDiaSession != null && s_typeDiaNavigationData != null)
        {
            string messageFormatOnException = string.Join("MSTestDiscoverer:DiaSession: Could not get navigation data for class:", className, ". Reason:{0}");
            object? data = SafeInvoke(() => s_methodGetNavigationData!.Invoke(navigationSession, [className, methodName]));

            if (data != null)
            {
                fileName = (string?)s_propertyFileName?.GetValue(data);
                minLineNumber = (int)(s_propertyMinLineNumber?.GetValue(data) ?? -1);
            }
        }
    }

    /// <summary>
    /// Disposes the navigation session instance.
    /// </summary>
    /// <param name="navigationSession"> The navigation session. </param>
    internal static void DisposeNavigationSession(object? navigationSession)
    {
        var diaSession = navigationSession as IDisposable;
        diaSession?.Dispose();
    }

    /// <summary>
    /// 1. Initializes DiaSession.
    /// 2. Assists in Unit Testing.
    /// </summary>
    /// <param name="diaSession">Type name of  DiaSession class.</param>
    /// <param name="diaNavigationData">Type name of DiaNavigationData class.</param>
    internal static void Initialize(string diaSession, string diaNavigationData)
    {
        // We won't reach this code, in we will generate this info in source generator,
        // and we won't use navigation session to get method locations from PDBs.
#pragma warning disable IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'
        s_typeDiaSession = Type.GetType(diaSession, false);
        s_typeDiaNavigationData = Type.GetType(diaNavigationData, false);
#pragma warning restore IL2057 // Unrecognized value passed to the typeName parameter of 'System.Type.GetType(String)'

#pragma warning disable IL2077 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
        if (s_typeDiaSession != null && s_typeDiaNavigationData != null)
        {
            s_methodGetNavigationData = s_typeDiaSession.GetRuntimeMethod("GetNavigationData", [typeof(string), typeof(string)]);
            s_propertyFileName = s_typeDiaNavigationData.GetRuntimeProperty("FileName");
            s_propertyMinLineNumber = s_typeDiaNavigationData.GetRuntimeProperty("MinLineNumber");
        }
#pragma warning restore IL2077 // 'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to 'target method'.
    }

    private static object? SafeInvoke<T>(Func<T> action)
    {
        try
        {
            return action.Invoke();
        }
        catch (Exception)
        {
            // TODO : Add EqtTrace
        }

        return null;
    }
}
