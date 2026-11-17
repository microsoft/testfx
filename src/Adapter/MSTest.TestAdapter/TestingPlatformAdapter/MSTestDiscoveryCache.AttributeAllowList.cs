// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

internal sealed partial class MSTestDiscoveryCache
{
    private static readonly HashSet<Type> SafeAttributes =
    [
        typeof(TestClassAttribute), typeof(TestMethodAttribute), typeof(TestCategoryAttribute),
        typeof(PriorityAttribute), typeof(OwnerAttribute), typeof(DescriptionAttribute),
        typeof(WorkItemAttribute), typeof(TestPropertyAttribute), typeof(DoNotParallelizeAttribute),
        typeof(ResourceLockAttribute), typeof(DependsOnAttribute), typeof(TimeoutAttribute), typeof(ExpectedDurationAttribute),
        typeof(IgnoreAttribute), typeof(AssemblyInitializeAttribute), typeof(AssemblyCleanupAttribute),
        typeof(ClassInitializeAttribute), typeof(ClassCleanupAttribute),
        typeof(TestInitializeAttribute), typeof(TestCleanupAttribute),
        typeof(GlobalTestInitializeAttribute), typeof(GlobalTestCleanupAttribute),
        typeof(DiscoverInternalsAttribute), typeof(ParallelizeAttribute),
        typeof(TestDataSourceOptionsAttribute), typeof(TestDataSourceDiscoveryAttribute),
    ];

    private static readonly HashSet<string> InertBclAttributeNames =
    [
        "System.CLSCompliantAttribute", "System.ObsoleteAttribute", "System.SerializableAttribute",
        "System.AttributeUsageAttribute",
        "System.Diagnostics.DebuggableAttribute", "System.Diagnostics.DebuggerStepThroughAttribute",
        "System.Diagnostics.DebuggerHiddenAttribute", "System.Diagnostics.DebuggerNonUserCodeAttribute",
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute",
        "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute",
        "System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute",
        "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute",
        "System.Reflection.AssemblyTitleAttribute", "System.Reflection.AssemblyDescriptionAttribute",
        "System.Reflection.AssemblyConfigurationAttribute", "System.Reflection.AssemblyCompanyAttribute",
        "System.Reflection.AssemblyProductAttribute", "System.Reflection.AssemblyCopyrightAttribute",
        "System.Reflection.AssemblyTrademarkAttribute", "System.Reflection.AssemblyCultureAttribute",
        "System.Reflection.AssemblyVersionAttribute", "System.Reflection.AssemblyFileVersionAttribute",
        "System.Reflection.AssemblyInformationalVersionAttribute", "System.Reflection.AssemblyMetadataAttribute",
        "System.Runtime.Versioning.TargetFrameworkAttribute",
        "System.Resources.NeutralResourcesLanguageAttribute",
        "System.Runtime.Versioning.SupportedOSPlatformAttribute",
        "System.Runtime.Versioning.UnsupportedOSPlatformAttribute",
        "System.Runtime.Versioning.NonVersionableAttribute",
        "System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
        "System.Runtime.CompilerServices.RuntimeCompatibilityAttribute",
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
        "System.Runtime.CompilerServices.AsyncStateMachineAttribute",
        "System.Runtime.CompilerServices.IteratorStateMachineAttribute",
        "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute",
        "System.Runtime.CompilerServices.ExtensionAttribute",
        "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
        "System.Runtime.CompilerServices.RefSafetyRulesAttribute",
        "System.Runtime.CompilerServices.RequiredMemberAttribute",
        "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute",
        "System.Runtime.CompilerServices.NullableAttribute",
        "System.Runtime.CompilerServices.NullableContextAttribute",
        "System.Runtime.CompilerServices.NullablePublicOnlyAttribute",
        "System.Runtime.CompilerServices.IsReadOnlyAttribute",
        "System.Runtime.CompilerServices.IsByRefLikeAttribute",
        "System.Runtime.CompilerServices.NativeIntegerAttribute",
        "System.Runtime.CompilerServices.IntrinsicAttribute",
        "System.Runtime.CompilerServices.PreserveBaseOverridesAttribute",
        "System.Runtime.InteropServices.ClassInterfaceAttribute",
        "System.Runtime.InteropServices.ComVisibleAttribute",
        "System.Runtime.CompilerServices.TypeForwardedFromAttribute",
    ];

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070", Justification = "This cache is disabled when dynamic code is unsupported or source-generated reflection is active.")]
    private static bool IsTypeSafe(Type type)
    {
        // New discovery-affecting metadata sources in TypeEnumerator/TestMethodValidator
        // must extend this walk before their descriptors can be reused.
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (!AreAttributesSafe(current.GetCustomAttributesData()))
            {
                return false;
            }

            foreach (MethodInfo method in PlatformServiceProvider.Instance.ReflectionOperations.GetRuntimeMethods(current))
            {
                if (!AreAttributesSafe(method.GetCustomAttributesData()))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsMetadataSafe(Func<bool> inspect)
    {
        try
        {
            return inspect();
        }
        catch (Exception exception) when (exception is TypeLoadException or ReflectionTypeLoadException
            or FileLoadException or FileNotFoundException or BadImageFormatException or CustomAttributeFormatException)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                "MSTest discovery cache: metadata could not be inspected; retaining live discovery. {0}", exception);
            return false;
        }
    }

    private static bool AreAttributesSafe(IEnumerable<CustomAttributeData> attributes)
    {
        foreach (CustomAttributeData attribute in attributes)
        {
            Type attributeType = attribute.AttributeType;
            if (SafeAttributes.Contains(attributeType)
                || (attributeType.FullName is { } name && InertBclAttributeNames.Contains(name)
                    && (attributeType.Assembly == typeof(object).Assembly
                        || attributeType.Assembly == typeof(DebuggerStepThroughAttribute).Assembly)))
            {
                continue;
            }

            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Info(
                    "MSTest discovery cache: attribute {0} from {1} requires live discovery.",
                    attributeType.FullName, attributeType.Assembly.FullName);
            }

            return false;
        }

        return true;
    }
}
