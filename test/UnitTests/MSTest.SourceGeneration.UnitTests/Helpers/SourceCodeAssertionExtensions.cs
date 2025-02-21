// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentAssertions.Primitives;

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;

internal static class SourceCodeAssertionExtensions
{
    public static SourceCodeAssertions Should(this SourceText sourceText) => new(sourceText.ToString());

    public static AndConstraint<SourceCodeAssertions> ContainSourceCode(this SourceCodeAssertions parent, string expectedSourceCode)
    {
        AndConstraint<SourceCodeAssertions> assertions = new SourceCodeAssertions(parent.Subject).ContainSourceCode(expectedSourceCode);

        return assertions;
    }

    public static AndConstraint<SourceCodeAssertions> ContainSourceCode(this StringAssertions parent, string expectedSourceCode)
    {
        AndConstraint<SourceCodeAssertions> assertions = new SourceCodeAssertions(parent.Subject).ContainSourceCode(expectedSourceCode);

        return assertions;
    }
}
