// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// The test deployment used by application models that do not support <c>DeploymentItemAttribute</c>
/// (see <see cref="AppModelCapabilities.SupportsDeployment"/>). Nothing is deployed and tests run from their source location.
/// </summary>
internal sealed class UnsupportedTestDeployment : ITestDeployment
{
    /// <inheritdoc/>
    public bool Deploy(IEnumerable<UnitTestElement> testElements, DeploymentContext deploymentContext, IAdapterMessageLogger messageLogger)
        => false;

    /// <inheritdoc/>
    public KeyValuePair<string, string>[]? GetDeploymentItems(MethodInfo method, Type type, ICollection<string> warnings)
        => null;

    /// <inheritdoc/>
    public void Cleanup()
    {
    }

    /// <inheritdoc/>
    public string? GetDeploymentDirectory()
        => null;

    /// <inheritdoc/>
    public string? GetUnsupportedDeploymentMessage(MethodInfo method, Type type)
    {
#if WINDOWS_UWP
        // Classic UWP consumes the netstandard2.0 test framework, which does not define DeploymentItemAttribute.
        return null;
#else
        IReflectionOperations reflection = PlatformServiceProvider.Instance.ReflectionOperations;
        return reflection.IsAttributeDefined<DeploymentItemAttribute>(method)
            || reflection.IsAttributeDefined<DeploymentItemAttribute>(type)
            ? Resource.DeploymentItemNotSupportedForUi
            : null;
#endif
    }
}
