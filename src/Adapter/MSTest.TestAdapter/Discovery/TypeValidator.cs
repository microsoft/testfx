// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

/// <summary>
/// Determines whether a type is a valid test class for this adapter.
/// </summary>
[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class TypeValidator
{
    // Setting this to a string representation instead of a typeof(TestContext).FullName
    // since the later would require a load of the Test Framework extension assembly at this point.
    private const string TestContextFullName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestContext";
    private readonly ReflectHelper _reflectHelper;
    private readonly bool _discoverInternals;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeValidator"/> class.
    /// </summary>
    /// <param name="reflectHelper">An instance to reflection helper for type information.</param>
    internal TypeValidator(ReflectHelper reflectHelper)
        : this(reflectHelper, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeValidator"/> class.
    /// </summary>
    /// <param name="reflectHelper">An instance to reflection helper for type information.</param>
    /// <param name="discoverInternals">True to discover test classes which are declared internal in
    /// addition to test classes which are declared public.</param>
    internal TypeValidator(ReflectHelper reflectHelper, bool discoverInternals)
    {
        _reflectHelper = reflectHelper;
        _discoverInternals = discoverInternals;
    }

    /// <summary>
    /// Determines if a type is a valid test class for this adapter.
    /// </summary>
    /// <param name="type">The reflected type.</param>
    /// <param name="warnings">Contains warnings if any, that need to be passed back to the caller.</param>
    /// <returns>Return true if it is a valid test class.</returns>
    internal virtual bool IsValidTestClass(Type type, List<string> warnings)
    {
        // PERF: We are doing caching reflection here, meaning we will cache every class info in the
        // assembly, this is because when we discover and run we will repeatedly inspect all the types in the assembly, and this
        // gives us a better performance.
        // It would be possible to use non-caching reflection here if we knew that we are only doing discovery that won't be followed by run,
        // but the difference is quite small, and we don't expect a huge amount of non-test classes in the assembly.
        if (!type.IsClass || !_reflectHelper.IsDerivedAttributeDefined<TestClassAttribute>(type, inherit: false))
        {
            return false;
        }

        // inaccessible class
        if (!TypeHasValidAccessibility(type, _discoverInternals))
        {
            string warning = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorNonPublicTestClass, type.FullName);
            warnings.Add(warning);
            return false;
        }

        // Generic class
        if (type is { IsGenericTypeDefinition: true, IsAbstract: false })
        {
            // In IDE generic classes that are not abstract are treated as not runnable. Keep consistence.
            string warning = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorTestClassIsGenericNonAbstract, type.FullName);
            warnings.Add(warning);
            return false;
        }

        // Class is not valid if the testContext property is incorrect
        if (!HasCorrectTestContextSignature(type))
        {
            string warning = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInValidTestContextSignature, type.FullName);
            warnings.Add(warning);
            return false;
        }

        // Abstract test classes can be base classes for derived test classes.
        //   There is no way to see if there are derived test classes.
        //   Thus if a test class is abstract, just ignore all test methods from it
        //   (they will be visible in derived classes). No warnings (such as test method, deployment item,
        //   etc attribute is defined on the class) will be generated for this class:
        // What we do is:
        //   - report the class as "not valid" test class. This will cause to skip enumerating tests from it.
        //   - Do not generate warnings/do not create NOT RUNNABLE tests.
        return !type.IsAbstract;
    }

    /// <summary>
    /// Determines if the type has a valid TestContext property definition.
    /// </summary>
    /// <param name="type">The reflected type.</param>
    /// <returns>Returns true if type has a valid TestContext property definition.</returns>
    internal static bool HasCorrectTestContextSignature(Type type)
    {
        DebugEx.Assert(type != null, "HasCorrectTestContextSignature type is null");

        PropertyInfo[] propertyInfoEnumerable = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredProperties(type);
        var propertyInfo = new List<PropertyInfo>();

        foreach (PropertyInfo pinfo in propertyInfoEnumerable)
        {
            // PropertyType.FullName can be null if the property is a generic type.
            if (TestContextFullName.Equals(pinfo.PropertyType.FullName, StringComparison.Ordinal))
            {
                propertyInfo.Add(pinfo);
            }
        }

        if (propertyInfo.Count == 0)
        {
            return true;
        }

        // NOTE: It's okay to not have a setter for the property at all.
        // The user may set the TestContext property in the constructor.
        // In case the user didn't set it in the constructor, the TestContextShouldBeValidAnalyzer will report a diagnostic.
        foreach (PropertyInfo pinfo in propertyInfo)
        {
            MethodInfo? getInfo = pinfo.GetMethod;
            if (getInfo == null)
            {
                // we don't have a getter.
                return false;
            }

            if (getInfo.IsPrivate || getInfo.IsStatic || getInfo.IsAbstract)
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TypeHasValidAccessibility(Type type, bool discoverInternals)
    {
        if (type.IsVisible)
        {
            // The type is public or a public nested class of entirely public container classes.
            return true;
        }

        if (!discoverInternals)
        {
            // The type is not externally visible and internal test classes are not to be discovered.
            return false;
        }

        // Either the type is not public or it is a nested class and itself or one of its containers is not public.
        if (!type.IsNested)
        {
            // The type is not public and is not nested. Non-nested types can be only public or internal
            // so this type must be internal.
            return true;
        }

        // Assembly is CLR term for internal visibility:
        // Private == private,
        // FamilyANDAssembly == private protected,
        // Assembly == internal,
        // Family == protected,
        // FamilyORAssembly == protected internal,
        // Public == public.
        // So this reads IsNestedInternal || IsNestedPublic:
        bool isNestedPublicOrInternal = type.IsNestedAssembly || type.IsNestedPublic;

        if (!isNestedPublicOrInternal)
        {
            // This type is nested, but is not public or internal.
            return false;
        }

        // The type itself is nested and is public, or internal, but could be in hierarchy of types
        // where some of the parent types is private (or other modifier that is not public and is not internal)
        // if we looked for just public types we could just look at IsVisible, but internal type nested in internal type
        // is not Visible, so we need to check all the parents and make sure they are all either public or internal.
        bool parentsArePublicOrInternal = true;
        Type? declaringType = type.DeclaringType;
        while (declaringType != null && parentsArePublicOrInternal)
        {
            bool declaringTypeIsPublicOrInternal =

                // Declaring type is non-nested type, and we are looking for internal or public, which are the only
                // two valid options that non-nested type can be.
                !declaringType.IsNested

                // Or the type is nested internal, or nested public type, but not any other
                // like nested protected internal type, or nested private type.
                || declaringType.IsNestedAssembly || declaringType.IsNestedPublic;

            if (!declaringTypeIsPublicOrInternal)
            {
                parentsArePublicOrInternal = false;
                break;
            }

            declaringType = declaringType.DeclaringType;
        }

        return parentsArePublicOrInternal;
    }
}
