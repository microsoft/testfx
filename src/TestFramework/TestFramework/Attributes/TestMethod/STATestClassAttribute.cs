// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test class attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class STATestClassAttribute : TestClassAttribute
{
    /// <inheritdoc />
    public override TestMethodAttribute? GetTestMethodAttribute(TestMethodAttribute? testMethodAttribute)
        => new STATestMethodAttribute(testMethodAttribute, testMethodAttribute?.DeclaringFilePath ?? string.Empty, testMethodAttribute?.DeclaringLineNumber ?? -1);
}
