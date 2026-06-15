// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Holds the per-async-flow stack of user-registered value formatters consulted by
/// <see cref="AssertionValueRenderer.RenderValue(object?)"/> when rendering values inside structured
/// assertion failure messages.
/// </summary>
/// <remarks>
/// <para>
/// Formatters are chained "newest-first": when rendering a value the most recently registered formatter
/// runs first and may either return a string (handling the value) or delegate to the supplied
/// <c>next</c> formatter (falling through to the formatter that was registered before it). The terminal
/// <c>next</c> is the built-in renderer captured in <see cref="AssertionValueRenderer"/>.
/// </para>
/// <para>
/// Disposing a registration marks its node as removed; the renderer skips removed nodes when assembling
/// the chain. This keeps node identity stable so out-of-order disposal of multiple registrations works
/// correctly regardless of stack position.
/// </para>
/// </remarks>
[StackTraceHidden]
internal static class AssertionValueFormatterRegistry
{
    // Singly-linked list of factories. Head is the newest registration so it ends up wrapping the
    // chain last (and therefore runs first). Nodes are append-only and reused across async flows that
    // captured a chain head at registration time; Dispose marks them inactive instead of rewriting
    // the list to keep identity stable for out-of-order disposal.
    private static readonly AsyncLocal<Node?> CurrentStack = new();

    /// <summary>
    /// Gets a value indicating whether any formatter node is present in the current async flow.
    /// The fast-path check; the renderer still needs <see cref="Render"/> to skip removed nodes.
    /// </summary>
    internal static bool HasFormatters => CurrentStack.Value is not null;

    /// <summary>
    /// Registers a chain-of-responsibility factory and returns an <see cref="IDisposable"/> whose
    /// <see cref="IDisposable.Dispose"/> removes the registration. Disposing is safe to call multiple times
    /// and from a different async flow than the registration.
    /// </summary>
    internal static IDisposable Add(Func<Func<object?, string>, Func<object?, string>> factory)
    {
        Node node = new(factory, CurrentStack.Value);
        CurrentStack.Value = node;
        return new Registration(node);
    }

    /// <summary>
    /// Invokes the user formatter chain (if any) for <paramref name="value"/>, falling back to
    /// <paramref name="builtIn"/> when no formatter handles the value.
    /// </summary>
    /// <remarks>
    /// If a user formatter throws — either while building the chain (the factory delegate) or while
    /// rendering the value — the built-in renderer's output is returned with a trailing annotation so the
    /// original assertion failure is not lost behind a formatter bug.
    /// </remarks>
    internal static string Render(object? value, Func<object?, string> builtIn)
    {
        Node? head = CurrentStack.Value;
        if (head is null)
        {
            return builtIn(value);
        }

        try
        {
            return BuildChain(head, builtIn)(value);
        }
        catch (Exception ex)
        {
            return $"{builtIn(value)} (value formatter threw {ex.GetType().Name})";
        }
    }

    private static Func<object?, string> BuildChain(Node? node, Func<object?, string> terminal)
    {
        if (node is null)
        {
            return terminal;
        }

        Func<object?, string> inner = BuildChain(node.Next, terminal);

        // Skip removed nodes — their factory is not invoked and the chain behaves as if they were
        // never registered.
        return node.IsRemoved ? inner : node.Factory(inner);
    }

    private sealed class Node
    {
        internal Node(Func<Func<object?, string>, Func<object?, string>> factory, Node? next)
        {
            Factory = factory;
            Next = next;
        }

        internal Func<Func<object?, string>, Func<object?, string>> Factory { get; }

        internal Node? Next { get; }

        // Mutated by Registration.Dispose, potentially from a different async flow/thread than the one
        // rendering. Marked volatile so the renderer reliably observes the removal across threads — a
        // plain bool read/write is atomic but carries no memory barrier, so visibility would otherwise
        // not be guaranteed for out-of-flow disposal.
        private volatile bool _isRemoved;

        internal bool IsRemoved
        {
            get => _isRemoved;
            set => _isRemoved = value;
        }
    }

    private sealed class Registration : IDisposable
    {
        private readonly Node _node;

        internal Registration(Node node) => _node = node;

        public void Dispose() => _node.IsRemoved = true;
    }
}
