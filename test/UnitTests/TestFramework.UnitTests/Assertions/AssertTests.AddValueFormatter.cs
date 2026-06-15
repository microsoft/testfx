// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void AddValueFormatter_Typed_NullFormatter_ThrowsArgumentNullException()
    {
        Action act = () => Assert.AddValueFormatter<int>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("formatter");
    }

    public void AddValueFormatter_Factory_NullFactory_ThrowsArgumentNullException()
    {
        Action act = () => Assert.AddValueFormatter(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("factory");
    }

    public void AddValueFormatter_Typed_CustomizesFailureMessage()
    {
        using (Assert.AddValueFormatter<DateTime>(dt =>
            $"{dt.ToString("O", CultureInfo.InvariantCulture)} [{dt.Kind}]"))
        {
            // Use different ticks so AreEqual actually fails, while still exercising Kind in the rendering.
            var expected = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var actual = new DateTime(2024, 1, 1, 12, 0, 1, DateTimeKind.Local);

            Action act = () => Assert.AreEqual(expected, actual);
            AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;
            ex.Message.Should().Contain("[Utc]");
            ex.Message.Should().Contain("[Local]");
        }
    }

    public void AddValueFormatter_Typed_FormatterRemovedAfterDispose()
    {
        var dt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Inside the scope the custom rendering is used.
        using (Assert.AddValueFormatter<DateTime>(d => "CUSTOM"))
        {
            AssertionValueRenderer.RenderValue(dt).Should().Be("CUSTOM");
        }

        // After dispose the built-in renderer takes over again.
        AssertionValueRenderer.RenderValue(dt).Should().Be(dt.ToString("O", CultureInfo.InvariantCulture));
    }

    public void AddValueFormatter_Typed_ReturningNullFallsThroughToBuiltIn()
    {
        // The typed formatter returns null for non-utc DateTimes so the built-in rendering applies.
        using (Assert.AddValueFormatter<DateTime>(dt =>
            dt.Kind == DateTimeKind.Utc ? "UTC!" : null))
        {
            var utc = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var local = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);

            AssertionValueRenderer.RenderValue(utc).Should().Be("UTC!");
            AssertionValueRenderer.RenderValue(local).Should().Be(local.ToString("O", CultureInfo.InvariantCulture));
        }
    }

    public void AddValueFormatter_Typed_DoesNotMatchUnrelatedTypes()
    {
        using (Assert.AddValueFormatter<DateTime>(_ => "CUSTOM"))
        {
            // Int is not DateTime, so the int still uses the built-in renderer.
            AssertionValueRenderer.RenderValue(42).Should().Be("42");
        }
    }

    public void AddValueFormatter_NullValuesAlwaysRenderedAsNullLiteral()
    {
        // Even with a registered formatter, nulls always render as "null" (built-in fast-path).
        using (Assert.AddValueFormatter<object>(_ => "FORMATTER-RAN"))
        {
            AssertionValueRenderer.RenderValue(null).Should().Be("null");
        }
    }

    public void AddValueFormatter_StacksNewestFirst()
    {
        using (Assert.AddValueFormatter<int>(i => $"outer:{i}"))
        using (Assert.AddValueFormatter<int>(i => $"inner:{i}"))
        {
            // The most recently registered formatter wins.
            AssertionValueRenderer.RenderValue(7).Should().Be("inner:7");
        }
    }

    public void AddValueFormatter_StacksFallThroughInOrder()
    {
        using (Assert.AddValueFormatter<int>(i => $"outer:{i}"))
        using (Assert.AddValueFormatter<int>(i => i > 100 ? $"inner:{i}" : null))
        {
            // Inner returns null for small ints so the outer formatter handles it.
            AssertionValueRenderer.RenderValue(7).Should().Be("outer:7");

            // Large ints are handled by the inner formatter directly.
            AssertionValueRenderer.RenderValue(500).Should().Be("inner:500");
        }
    }

    public void AddValueFormatter_DisposeRemovesOnlyThatRegistration()
    {
        IDisposable outer = Assert.AddValueFormatter<int>(i => $"outer:{i}");
        IDisposable inner = Assert.AddValueFormatter<int>(i => $"inner:{i}");

        try
        {
            // Disposing the outer registration (not the top of the stack) keeps inner active.
            outer.Dispose();
            AssertionValueRenderer.RenderValue(7).Should().Be("inner:7");

            // Disposing the inner registration falls back to the built-in renderer.
            inner.Dispose();
            AssertionValueRenderer.RenderValue(7).Should().Be("7");
        }
        finally
        {
            outer.Dispose();
            inner.Dispose();
        }
    }

    public void AddValueFormatter_DisposeIsIdempotent()
    {
        IDisposable d = Assert.AddValueFormatter<int>(i => $"x:{i}");
        d.Dispose();

        // A second dispose call is a no-op.
        Action act = () => d.Dispose();
        act.Should().NotThrow();

        AssertionValueRenderer.RenderValue(7).Should().Be("7");
    }

    public void AddValueFormatter_AppliesToCollectionItems()
    {
        using (Assert.AddValueFormatter<int>(i => $"#{i}"))
        {
            AssertionValueRenderer.RenderValue(new[] { 1, 2, 3 }).Should().Be("[#1, #2, #3]");
        }
    }

    public void AddValueFormatter_FactoryOverload_ReceivesNextDelegate()
    {
        using (Assert.AddValueFormatter(next => value =>
            value is int i ? $"int={i}" : next(value)))
        {
            AssertionValueRenderer.RenderValue(42).Should().Be("int=42");

            // Non-int still goes to the built-in renderer via the `next` delegate.
            AssertionValueRenderer.RenderValue("hello").Should().Be("\"hello\"");
        }
    }

    public void AddValueFormatter_FactoryOverload_ChainsWithTypedOverload()
    {
        using (Assert.AddValueFormatter<int>(i => $"typed:{i}"))
        using (Assert.AddValueFormatter(next => value =>
            value is int i && i < 0 ? $"factory-negative:{i}" : next(value)))
        {
            // Factory is newest so it runs first; for negative ints it handles, otherwise falls through.
            AssertionValueRenderer.RenderValue(-5).Should().Be("factory-negative:-5");
            AssertionValueRenderer.RenderValue(5).Should().Be("typed:5");
        }
    }

    public void AddValueFormatter_ThrowingFormatterDoesNotDerailRendering()
    {
        using (Assert.AddValueFormatter<int>(_ => throw new InvalidOperationException("boom")))
        {
            // The renderer must not propagate the exception; it falls back to the built-in renderer
            // with a marker so the assertion failure remains useful.
            string rendered = AssertionValueRenderer.RenderValue(42);
            rendered.Should().Contain("42");
            rendered.Should().Contain("InvalidOperationException");
        }
    }
}
