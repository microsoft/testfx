// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Extensions.DependencyInjection;

internal sealed class MicrosoftExtensionsTestClassInstanceFactory(IServiceScopeFactory serviceScopeFactory) : ITestClassInstanceFactory
{
    public ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext)
    {
#if NET
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException(
                "MSTest test-class injection from Microsoft.Extensions.Hosting requires runtime dynamic code and is not supported by NativeAOT.");
        }
#endif

        IServiceScope scope = serviceScopeFactory.CreateScope();
        try
        {
            object instance = ShouldSupplyTestContext(testClassType)
                ? ActivatorUtilities.CreateInstance(scope.ServiceProvider, testClassType, testContext)
                : ActivatorUtilities.CreateInstance(scope.ServiceProvider, testClassType);
            return new MicrosoftExtensionsTestClassInstanceLease(instance, scope);
        }
        catch (Exception activationException)
        {
            try
            {
                DisposeScope(scope);
            }
            catch (Exception disposeException)
            {
                throw new AggregateException(
                    CreateActivationException(testClassType, activationException),
                    disposeException);
            }

            throw CreateActivationException(testClassType, activationException);
        }
    }

    private static InvalidOperationException CreateActivationException(Type testClassType, Exception innerException)
        => new(
            $"MSTest could not create test class '{testClassType.FullName}' from the application host service provider. "
            + "Ensure that exactly one public constructor can be satisfied by registered services and TestContext.",
            innerException);

    private static bool ShouldSupplyTestContext(Type testClassType)
    {
        ConstructorInfo[] constructors = testClassType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        ConstructorInfo? preferredConstructor = constructors.SingleOrDefault(
            static constructor => constructor.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute), inherit: false));

        ConstructorInfo[] candidates = preferredConstructor is null ? constructors : [preferredConstructor];
        return candidates.Any(static constructor =>
            constructor.GetParameters().Any(static parameter => parameter.ParameterType == typeof(TestContext)));
    }

    private static void DisposeScope(IServiceScope scope)
    {
        if (scope is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        else
        {
            scope.Dispose();
        }
    }
}
