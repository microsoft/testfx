// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers.Helpers.Lightup;

[SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Not a comparable instance.")]
internal readonly struct IAttributeOperationWrapper : IOperationWrapper
{
    internal const string WrappedTypeName = "Microsoft.CodeAnalysis.Operations.IAttributeOperation";
    private static readonly Type? WrappedType = OperationWrapperHelper.GetWrappedType(typeof(IAttributeOperationWrapper));

    private static readonly Func<IOperation, IOperation> OperationAccessor = LightupHelpers.CreateOperationPropertyAccessor<IOperation, IOperation>(WrappedType, nameof(Operation), fallbackResult: null!);

    private IAttributeOperationWrapper(IOperation operation)
        => WrappedOperation = operation;

    public IOperation WrappedOperation { get; }

    public ITypeSymbol? Type => WrappedOperation.Type;

    /// <summary>
    /// Gets the operation representing the attribute. This can be a <see cref="IObjectCreationOperation" /> in non-error cases, or an <see cref="IInvalidOperation" /> in error cases.
    /// </summary>
    public IOperation Operation => OperationAccessor(WrappedOperation);

    public static IAttributeOperationWrapper FromOperation(IOperation operation)
        => operation == null
            ? default
            : (!IsInstance(operation)
                ? throw new InvalidCastException($"Cannot cast '{operation.GetType().FullName}' to '{WrappedTypeName}'")
                : new IAttributeOperationWrapper(operation));

    public static bool IsInstance(IOperation operation)
        => operation != null && LightupHelpers.CanWrapOperation(operation, WrappedType);
}
