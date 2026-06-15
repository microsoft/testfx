// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    /// <summary>
    /// Registers a custom formatter that controls how values of <typeparamref name="T"/> are rendered
    /// inside structured assertion failure messages. The registration is scoped to the current asynchronous
    /// flow (backed by <see cref="AsyncLocal{T}"/>) and is removed when the returned <see cref="IDisposable"/>
    /// is disposed.
    /// </summary>
    /// <typeparam name="T">The type whose rendered representation should be customized.</typeparam>
    /// <param name="formatter">
    /// A function that returns the string representation to use, or <see langword="null"/> to fall through to
    /// the next registered formatter (or the built-in renderer when no other formatter handles the value).
    /// </param>
    /// <returns>An <see cref="IDisposable"/> whose disposal removes the registration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="formatter"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Formatters affect only how values are rendered in failure messages. They do not change equality
    /// comparisons performed by assertions such as <c>Assert.AreEqual</c>. To customize equality, use the
    /// existing <see cref="IEqualityComparer{T}"/> overloads.
    /// </para>
    /// <para>
    /// Registrations stack newest-first: the most recently registered formatter is consulted first.
    /// A formatter returning <see langword="null"/> defers to the next formatter, and ultimately to the
    /// built-in renderer. <see langword="null"/> values are always rendered as <c>"null"</c> by the
    /// built-in renderer and are never passed to user formatters.
    /// </para>
    /// <para>
    /// Common registration sites and their effective scope:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Inside a <c>[TestMethod]</c>: that test method (and its async continuations).</description></item>
    /// <item><description><c>[TestInitialize]</c>: each test in the containing class.</description></item>
    /// <item><description><c>[ClassInitialize]</c>: all tests in the containing class.</description></item>
    /// <item><description><c>[AssemblyInitialize]</c>: all tests in the containing assembly.</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// [TestMethod]
    /// public void DateTimeFailureShowsKind()
    /// {
    ///     using var _ = Assert.AddValueFormatter&lt;DateTime&gt;(dt =&gt; $"{dt:O} [{dt.Kind}]");
    ///     Assert.AreEqual(DateTime.UtcNow, DateTime.Now);
    /// }
    /// </code>
    /// </example>
    [Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
    public static IDisposable AddValueFormatter<T>(Func<T, string?> formatter)
    {
        _ = formatter ?? throw new ArgumentNullException(nameof(formatter));

        return AssertionValueFormatterRegistry.Add(next => value =>
        {
            if (value is T typed)
            {
                string? result = formatter(typed);
                if (result is not null)
                {
                    return result;
                }
            }

            return next(value);
        });
    }

    /// <summary>
    /// Registers a chain-of-responsibility value-formatter factory. The factory receives the <c>next</c>
    /// formatter in the chain and returns a new formatter; the returned formatter should either produce a
    /// string for the values it handles or delegate to <c>next</c> for the values it does not handle.
    /// The registration is scoped to the current asynchronous flow and is removed when the returned
    /// <see cref="IDisposable"/> is disposed.
    /// </summary>
    /// <param name="factory">
    /// A function that receives the next formatter in the chain and returns a formatter to install ahead of it.
    /// </param>
    /// <returns>An <see cref="IDisposable"/> whose disposal removes the registration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Use this overload when you need to inspect more than a single type (for example to render any object
    /// that implements a marker interface) or when you need explicit control over delegation to <c>next</c>.
    /// For the common "format a specific type" case prefer <see cref="AddValueFormatter{T}(Func{T, string?})"/>.
    /// <para>
    /// <paramref name="factory"/> is the chain builder, not the formatter itself: it is invoked every time a
    /// value is rendered while the registration is active (the chain is rebuilt per render so that out-of-order
    /// disposal of other registrations is honored), not just once at registration time. Keep it cheap and
    /// side-effect free; perform any expensive setup outside the factory and have it return a formatter that
    /// closes over the precomputed state.
    /// </para>
    /// </remarks>
    [Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
    public static IDisposable AddValueFormatter(Func<Func<object?, string>, Func<object?, string>> factory)
    {
        _ = factory ?? throw new ArgumentNullException(nameof(factory));

        return AssertionValueFormatterRegistry.Add(factory);
    }
}
