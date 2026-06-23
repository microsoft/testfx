// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// <b>Infrastructure.</b> Describes one constructor and a delegate that invokes it, used by the
/// MSTest source generator to register reflection-free instantiation for a test class through
/// <see cref="ReflectionMetadataHook"/>.
/// </summary>
/// <remarks>
/// This type is public only because the source generator emits a <c>[ModuleInitializer]</c> in the
/// test assembly that constructs it across the assembly boundary. It is not intended to be used
/// directly from application code and its shape may evolve with the generator.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct ConstructorInvokerInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructorInvokerInfo"/> struct.
    /// </summary>
    /// <param name="parameterTypes">The constructor's parameter types, in declaration order.</param>
    /// <param name="invoker">A delegate that constructs the instance from an argument array.</param>
    public ConstructorInvokerInfo(Type[] parameterTypes, Func<object?[]?, object> invoker)
    {
        ParameterTypes = parameterTypes ?? throw new ArgumentNullException(nameof(parameterTypes));
        Invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    /// <summary>
    /// Gets the constructor's parameter types, in declaration order.
    /// </summary>
    public Type[] ParameterTypes { get; }

    /// <summary>
    /// Gets the delegate that constructs the instance from an argument array.
    /// </summary>
    public Func<object?[]?, object> Invoker { get; }
}
