// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Hosting.Resources;

namespace Microsoft.Extensions.DependencyInjection;

internal sealed class MicrosoftExtensionsTestClassInstanceFactory(IServiceScopeFactory serviceScopeFactory) : ITestClassInstanceFactory
{
    public ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext)
    {
#if NET
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new NotSupportedException(
                HostingResources.DynamicCodeNotSupported);
        }
#endif

        IServiceScope scope = serviceScopeFactory.CreateScope();
        bool shouldSupplyTestContext;
        try
        {
            shouldSupplyTestContext = ShouldSupplyTestContext(scope.ServiceProvider, testClassType);
        }
        catch (Exception selectionException)
        {
            try
            {
                DisposeScope(scope);
            }
            catch (Exception disposeException)
            {
                throw new AggregateException(selectionException, disposeException);
            }

            throw;
        }

        try
        {
            object instance = shouldSupplyTestContext
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
            string.Format(CultureInfo.CurrentCulture, HostingResources.ActivationFailed, testClassType.FullName),
            innerException);

    private static bool ShouldSupplyTestContext(IServiceProvider serviceProvider, Type testClassType)
    {
        ConstructorInfo[] constructors = testClassType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        foreach (ConstructorInfo constructor in constructors)
        {
            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                if (parameter.ParameterType != typeof(TestContext)
                    && parameter.ParameterType.IsSubclassOf(typeof(TestContext)))
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            HostingResources.DerivedTestContextParameter,
                            testClassType.FullName,
                            parameter.ParameterType.FullName));
                }
            }
        }

        ConstructorInfo[] preferredConstructors =
            [.. constructors.Where(static constructor =>
                constructor.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute), inherit: false))];
        if (preferredConstructors.Length > 1)
        {
            throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, HostingResources.MultiplePreferredConstructors, testClassType.FullName));
        }

        if (preferredConstructors is [ConstructorInfo preferredConstructor])
        {
            return HasTestContextParameter(preferredConstructor);
        }

        IServiceProviderIsService? serviceChecker = serviceProvider.GetService<IServiceProviderIsService>();
        IServiceProviderIsKeyedService? keyedServiceChecker = serviceProvider.GetService<IServiceProviderIsKeyedService>();
        ConstructorCandidate[] candidates =
        [
            .. constructors
                .Select(constructor => new ConstructorCandidate(
                    constructor,
                    HasTestContextParameter(constructor),
                    IsSatisfiable(constructor, serviceChecker, keyedServiceChecker)))
                .Where(static candidate => candidate.IsSatisfiable),
        ];
        if (candidates.Length == 0)
        {
            return false;
        }

        int maximumParameterCount = candidates.Max(static candidate => candidate.Constructor.GetParameters().Length);
        ConstructorCandidate[] longestCandidates =
            [.. candidates.Where(candidate => candidate.Constructor.GetParameters().Length == maximumParameterCount)];
        if (longestCandidates is [ConstructorCandidate selectedCandidate])
        {
            return selectedCandidate.HasTestContext;
        }

        ConstructorCandidate[] testContextCandidates =
            [.. longestCandidates.Where(static candidate => candidate.HasTestContext)];
        return testContextCandidates is [ConstructorCandidate testContextCandidate]
            ? testContextCandidate.HasTestContext
            : throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, HostingResources.AmbiguousConstructors, testClassType.FullName));
    }

    private static bool HasTestContextParameter(ConstructorInfo constructor)
        => constructor.GetParameters().Any(static parameter => parameter.ParameterType == typeof(TestContext));

    private static bool IsSatisfiable(
        ConstructorInfo constructor,
        IServiceProviderIsService? serviceChecker,
        IServiceProviderIsKeyedService? keyedServiceChecker)
    {
        foreach (ParameterInfo parameter in constructor.GetParameters())
        {
            FromKeyedServicesAttribute? keyedServicesAttribute = parameter.GetCustomAttribute<FromKeyedServicesAttribute>();
            if (parameter.ParameterType == typeof(TestContext)
                || parameter.HasDefaultValue
                || (keyedServicesAttribute is null
                    ? serviceChecker?.IsService(parameter.ParameterType) is not false
                    : keyedServiceChecker?.IsKeyedService(parameter.ParameterType, keyedServicesAttribute.Key) is not false))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static void DisposeScope(IServiceScope scope)
    {
        if (scope is IAsyncDisposable asyncDisposable)
        {
            Task.Run(async () => await asyncDisposable.DisposeAsync().ConfigureAwait(false)).GetAwaiter().GetResult();
            return;
        }

        scope.Dispose();
    }

    private readonly record struct ConstructorCandidate(
        ConstructorInfo Constructor,
        bool HasTestContext,
        bool IsSatisfiable);
}
