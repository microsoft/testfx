// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed class TestAssemblySettingsProvider : MarshalByRefObject
{
    /// <summary>
    /// Returns object to be used for controlling lifetime, null means infinite lifetime.
    /// </summary>
    /// <returns>
    /// The <see cref="object"/>.
    /// </returns>
    [SecurityCritical]
#if NET5_0_OR_GREATER
    [Obsolete]
#endif
    public override object InitializeLifetimeService() => null!;

    internal static TestAssemblySettings GetSettings(string source)
    {
        var testAssemblySettings = new TestAssemblySettings();

        // Load the source.
        Assembly testAssembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(source, isReflectionOnly: false);

        ParallelizeAttribute? parallelizeAttribute = ReflectHelper.GetParallelizeAttribute(testAssembly);

        if (parallelizeAttribute != null)
        {
            testAssemblySettings.Workers = parallelizeAttribute.Workers;
            testAssemblySettings.Scope = parallelizeAttribute.Scope;

            if (testAssemblySettings.Workers == 0)
            {
                testAssemblySettings.Workers = Environment.ProcessorCount;
            }
        }

        testAssemblySettings.CanParallelizeAssembly = !ReflectHelper.IsDoNotParallelizeSet(testAssembly);

        ClassCleanupExecutionAttribute? classCleanupSequencingAttribute = ReflectHelper.GetClassCleanupAttribute(testAssembly);
        if (classCleanupSequencingAttribute != null)
        {
            testAssemblySettings.ClassCleanupLifecycle = classCleanupSequencingAttribute.CleanupBehavior;
        }

        return testAssemblySettings;
    }
}
