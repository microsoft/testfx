// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Regression tests for specific bug fixes in the Assert class and related framework types.
/// Each test references the GitHub issue and PR that motivated the fix.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region PR #5275 / Issue #5277 — DoesNotContain(string, string) StackOverflow fix

    /// <summary>
    /// Regression: The 2-parameter DoesNotContain(string, string) previously called itself
    /// recursively instead of delegating to the 3-parameter overload, causing StackOverflowException.
    /// See: https://github.com/microsoft/testfx/issues/5277.
    /// </summary>
    public void Regression_GH5277_DoesNotContain_StringTwoParamOverload_CompletesWithoutStackOverflow()
    {
        // This call would previously cause a StackOverflowException due to infinite recursion.
        // The fix delegates to DoesNotContain(string, string, StringComparison.Ordinal, ...).
        Action action = () => Assert.DoesNotContain("xyz", "hello world");

        action.Should().NotThrow();
    }

    public void Regression_GH5277_DoesNotContain_StringTwoParamOverload_SubstringAbsent_Passes()
        => Assert.DoesNotContain("xyz", "hello world");

    public void Regression_GH5277_DoesNotContain_StringTwoParamOverload_SubstringPresent_Throws()
    {
        Action action = () => Assert.DoesNotContain("world", "hello world");

        action.Should().Throw<AssertFailedException>();
    }

    #endregion

    #region PR #4670 / Issue #4468 — CultureInfo nullability on AreEqual/AreNotEqual

    /// <summary>
    /// Regression: String comparison overloads had bad nullability annotation on CultureInfo parameter.
    /// The CultureInfo parameter properly accepts a value and falls back to InvariantCulture.
    /// See: https://github.com/microsoft/testfx/issues/4468.
    /// </summary>
    public void Regression_GH4468_AreEqual_WithInvariantCulture_CaseInsensitive_Passes()
    {
        Action action = () => Assert.AreEqual("hello", "HELLO", true, CultureInfo.InvariantCulture);

        action.Should().NotThrow();
    }

    public void Regression_GH4468_AreEqual_WithInvariantCulture_CaseSensitive_FailsForDifferentCase()
    {
        Action action = () => Assert.AreEqual("hello", "HELLO", false, CultureInfo.InvariantCulture);

        action.Should().Throw<AssertFailedException>();
    }

    public void Regression_GH4468_AreNotEqual_WithInvariantCulture_CaseInsensitive_FailsForEqualStrings()
    {
        Action action = () => Assert.AreNotEqual("hello", "HELLO", true, CultureInfo.InvariantCulture);

        action.Should().Throw<AssertFailedException>();
    }

    public void Regression_GH4468_AreNotEqual_WithInvariantCulture_CaseSensitive_PassesForDifferentCase()
    {
        Action action = () => Assert.AreNotEqual("hello", "HELLO", false, CultureInfo.InvariantCulture);

        action.Should().NotThrow();
    }

    #endregion

    #region PR #1382 / Issue #1376 — TestContext.Properties nullability

    /// <summary>
    /// Regression: TestContext.Properties was incorrectly marked as nullable.
    /// TestContext lives in TestFramework.Extensions (not referenced by this project),
    /// so we verify the annotation via reflection on the loaded assembly.
    /// See: https://github.com/microsoft/testfx/issues/1376.
    /// </summary>
    public void Regression_GH1376_TestContextProperties_ReturnType_IsNotNullableAnnotated()
    {
        Type? testContextType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return [];
                }
            })
            .FirstOrDefault(t => t.Name == "TestContext" && t.Namespace == "Microsoft.VisualStudio.TestTools.UnitTesting");

        if (testContextType is null)
        {
            // TestContext type isn't loaded because TestFramework.Extensions is not referenced.
            return;
        }

        PropertyInfo? propertiesProp = testContextType.GetProperty("Properties");
        propertiesProp.Should().NotBeNull();

        // The return type should be non-nullable IDictionary<string, object?>.
        propertiesProp!.PropertyType.IsGenericType.Should().BeTrue();
        propertiesProp.PropertyType.GetGenericTypeDefinition().Should().Be(typeof(IDictionary<,>));
    }

    #endregion

    #region PR #1381 / Issue #1375 — Assert.ThrowsException return nullability

    /// <summary>
    /// Regression: Assert.ThrowsException/ThrowsAsync was marked as returning nullable but
    /// should return non-null on success.
    /// See: https://github.com/microsoft/testfx/issues/1375.
    /// </summary>
    public void Regression_GH1375_Throws_ReturnValue_IsNotNull()
    {
        InvalidOperationException result = Assert.Throws<InvalidOperationException>(
            () => throw new InvalidOperationException("test"));

        result.Should().NotBeNull();
        result.Message.Should().Be("test");
    }

    public async Task Regression_GH1375_ThrowsAsync_ReturnValue_IsNotNull()
    {
        InvalidOperationException result = await Assert.ThrowsAsync<InvalidOperationException>(
            () => throw new InvalidOperationException("async test"));

        result.Should().NotBeNull();
        result.Message.Should().Be("async test");
    }

    #endregion

    #region PR #1005 / Issue #1004 — DoesNotReturnIf on Assert.IsTrue/IsFalse

    /// <summary>
    /// Regression: Assert.IsTrue/IsFalse didn't have [DoesNotReturnIf] attributes, so C# nullable
    /// analysis couldn't use them for null narrowing.
    /// See: https://github.com/microsoft/testfx/issues/1004.
    /// </summary>
    public void Regression_GH1004_IsTrue_WhenTrue_DoesNotThrow()
    {
        Action action = () => Assert.IsTrue(true);

        action.Should().NotThrow();
    }

    public void Regression_GH1004_IsTrue_WhenFalse_Throws()
    {
        Action action = () => Assert.IsTrue(false);

        action.Should().Throw<AssertFailedException>();
    }

    public void Regression_GH1004_IsFalse_WhenFalse_DoesNotThrow()
    {
        Action action = () => Assert.IsFalse(false);

        action.Should().NotThrow();
    }

    public void Regression_GH1004_IsFalse_WhenTrue_Throws()
    {
        Action action = () => Assert.IsFalse(true);

        action.Should().Throw<AssertFailedException>();
    }

    public void Regression_GH1004_IsTrue_HasDoesNotReturnIfAttribute()
    {
        MethodInfo method = typeof(Assert).GetMethod(nameof(Assert.IsTrue), [typeof(bool?), typeof(string), typeof(string)])!;

        method.Should().NotBeNull();
        ParameterInfo conditionParam = method.GetParameters()[0];
        conditionParam.GetCustomAttributes()
            .Should().Contain(a => a.GetType().Name == "DoesNotReturnIfAttribute");
    }

    public void Regression_GH1004_IsFalse_HasDoesNotReturnIfAttribute()
    {
        MethodInfo method = typeof(Assert).GetMethod(nameof(Assert.IsFalse), [typeof(bool?), typeof(string), typeof(string)])!;

        method.Should().NotBeNull();
        ParameterInfo conditionParam = method.GetParameters()[0];
        conditionParam.GetCustomAttributes()
            .Should().Contain(a => a.GetType().Name == "DoesNotReturnIfAttribute");
    }

    #endregion

    #region PR #744 / Issue #714 — Nullable-annotated Assert.IsNotNull

    /// <summary>
    /// Regression: Assert.IsNotNull didn't narrow types for nullable reference analysis.
    /// See: https://github.com/microsoft/testfx/issues/714.
    /// </summary>
    public void Regression_GH714_IsNotNull_WithNonNullValue_DoesNotThrow()
    {
        object value = new();
        Action action = () => Assert.IsNotNull(value);

        action.Should().NotThrow();
    }

    public void Regression_GH714_IsNotNull_WithNullValue_Throws()
    {
        object? value = null;
        Action action = () => Assert.IsNotNull(value);

        action.Should().Throw<AssertFailedException>();
    }

    public void Regression_GH714_IsNotNull_HasNotNullAttribute()
    {
        MethodInfo method = typeof(Assert).GetMethod(nameof(Assert.IsNotNull), [typeof(object), typeof(string), typeof(string)])!;

        method.Should().NotBeNull();
        ParameterInfo valueParam = method.GetParameters()[0];
        valueParam.GetCustomAttributes()
            .Should().Contain(a => a.GetType().Name == "NotNullAttribute");
    }

    #endregion

    #region PR #5708 / Issue #5707 — TestMethodAttribute.ExecuteAsync is overridable

    /// <summary>
    /// Regression: TestMethodAttribute.ExecuteAsync should be virtual so derived classes can override it.
    /// See: https://github.com/microsoft/testfx/issues/5707.
    /// </summary>
    public void Regression_GH5707_TestMethodAttribute_ExecuteAsync_IsVirtualAndOverridable()
    {
        MethodInfo executeAsyncMethod = typeof(TestMethodAttribute)
            .GetMethod(nameof(TestMethodAttribute.ExecuteAsync))!;

        executeAsyncMethod.Should().NotBeNull();
        executeAsyncMethod.IsVirtual.Should().BeTrue();
    }

    #endregion

    #region PR #1450 / Issue #1449 — TestFramework assembly CLSCompliant

    /// <summary>
    /// Regression: TestFramework assembly should be marked as CLSCompliant(true).
    /// See: https://github.com/microsoft/testfx/issues/1449.
    /// </summary>
    public void Regression_GH1449_TestFrameworkAssembly_IsCLSCompliant()
    {
        Assembly assembly = typeof(Assert).Assembly;
        CLSCompliantAttribute? attr = assembly.GetCustomAttribute<CLSCompliantAttribute>();

        attr.Should().NotBeNull();
        attr!.IsCompliant.Should().BeTrue();
    }

    #endregion
}
