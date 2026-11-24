// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class RuntimeContextMutationTests : TestContainer
{
    private const string DotNetWatchVariable = "DOTNET_WATCH";
    private const string HotReloadVariable = "TESTINGPLATFORM_HOTRELOAD_ENABLED";

    public void HotReloadDetection_IsEnabledWhenEitherVariableIsExactlyOne()
    {
        // RuntimeContext.IsHotReloadEnabled keeps the first answer for the whole process, so it can only ever
        // reflect the environment of whoever asked first. Pin that cached answer to the real environment, then
        // evaluate the detection logic that feeds it against controlled environments.
        _ = RuntimeContext.IsHotReloadEnabled;
        Func<bool> detectHotReload = GetHotReloadDetection();

        (string? DotNetWatch, string? HotReload, bool Expected, string Because)[] cases =
        [
            (null, null, false, "neither variable is set"),
            ("1", null, true, "DOTNET_WATCH alone is enough, even when the other variable is not set at all"),
            (null, "1", true, "TESTINGPLATFORM_HOTRELOAD_ENABLED alone is enough, even when DOTNET_WATCH is not set at all"),
            ("1", "1", true, "both variables are set"),
            ("0", "1", true, "TESTINGPLATFORM_HOTRELOAD_ENABLED=1 wins over a DOTNET_WATCH that is switched off"),
            ("1", "0", true, "DOTNET_WATCH=1 wins over a TESTINGPLATFORM_HOTRELOAD_ENABLED that is switched off"),
            ("0", null, false, "DOTNET_WATCH only counts when it is 1"),
            (null, "0", false, "TESTINGPLATFORM_HOTRELOAD_ENABLED only counts when it is 1"),
            ("true", "true", false, "only the value 1 turns hot reload on, not other truthy spellings"),
        ];

        string? originalDotNetWatch = Environment.GetEnvironmentVariable(DotNetWatchVariable);
        string? originalHotReload = Environment.GetEnvironmentVariable(HotReloadVariable);
        try
        {
            foreach ((string? dotNetWatch, string? hotReload, bool expected, string because) in cases)
            {
                Environment.SetEnvironmentVariable(DotNetWatchVariable, dotNetWatch);
                Environment.SetEnvironmentVariable(HotReloadVariable, hotReload);

                detectHotReload().Should().Be(expected, because: because);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(DotNetWatchVariable, originalDotNetWatch);
            Environment.SetEnvironmentVariable(HotReloadVariable, originalHotReload);
        }
    }

    // The detection is a non-capturing lambda handed to the Lazy of RuntimeContext: the compiler hosts it in a
    // nested closure class as the only parameterless method returning bool.
    private static Func<bool> GetHotReloadDetection()
    {
        foreach (Type nestedType in typeof(RuntimeContext).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (MethodInfo method in nestedType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.ReturnType == typeof(bool) && method.GetParameters().Length == 0)
                {
                    object? target = method.IsStatic ? null : Activator.CreateInstance(nestedType, nonPublic: true);
                    return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), target, method);
                }
            }
        }

        throw new InvalidOperationException("The lambda computing RuntimeContext.IsHotReloadEnabled was not found.");
    }
}
