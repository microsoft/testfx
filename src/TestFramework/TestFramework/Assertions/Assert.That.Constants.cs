// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
public static partial class AssertExtensions
{
    // Constants for standardized display values
    private const string NullDisplay = "null";
    private const string NullAngleBrackets = "<null>";

    // Constants for indexer method names
    private const string GetItemMethodName = "get_Item";
    private const string GetMethodName = "Get";

    // Constants for compiler-generated patterns
    private const string AnonymousTypePrefix = "<>f__AnonymousType";
    private const string ValueWrapperPattern = "value(";
    private const string ArrayLengthWrapperPattern = "ArrayLength(";
    private const string NewKeyword = "new ";
    private const string ActionTypePrefix = "Action`";
    private const string FuncTypePrefix = "Func`";

    // Constants for collection type patterns
    private const string ListInitPattern = "`1()";

    // Constants for parenthesis limits
    private const int MaxConsecutiveParentheses = 2;

    // Sentinel placed in the evaluation cache when a sub-expression cannot be evaluated.
    // Using a reference-identity object (rather than a string) prevents accidentally
    // substituting it for a same-typed operand when rebuilding parent expressions, and
    // lets diagnostic extraction translate it to a localized "<Failed to evaluate>" display.
    private static readonly object FailedToEvaluateSentinel = new();
}
