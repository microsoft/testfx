// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers.Test;

internal static class ProductReflection
{
    internal const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static Type Type(bool codeFix, string name)
        => (codeFix ? typeof(AddResourceLockFixer).Assembly : typeof(RedundantTestMethodAttributeAnalyzer).Assembly).GetType(name, throwOnError: true)!;

    internal static object Create(Type type, Type[] parameters, params object?[] arguments)
        => Invoke(type.GetConstructor(AllMembers, binder: null, parameters, modifiers: null)!, null, arguments)!;

    internal static object? Call(Type type, object? instance, string name, Type[] parameters, params object?[] arguments)
        => Invoke(
            type.GetMethods(AllMembers).Single(method => method.Name == name && !method.ContainsGenericParameters
                && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters)), instance, arguments);

    internal static object? Invoke(MethodBase method, object? instance, params object?[] arguments)
    {
        try
        {
            return method is ConstructorInfo constructor ? constructor.Invoke(arguments) : ((MethodInfo)method).Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    internal static T Get<T>(object instance, string property)
        => (T)instance.GetType().GetProperty(property, AllMembers)!.GetValue(instance)!;

    internal static IParenthesizedOperation Parenthesize(IOperation operand, SemanticModel model)
    {
        Type type = typeof(IOperation).Assembly.GetType("Microsoft.CodeAnalysis.Operations.ParenthesizedOperation", throwOnError: true)!;
        ConstructorInfo constructor = type.GetConstructors(AllMembers).Single();
        return (IParenthesizedOperation)Invoke(
            constructor, null, operand, model, SyntaxFactory.ParenthesizedExpression((ExpressionSyntax)operand.Syntax), operand.Type, null, false)!;
    }
}
