// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Extensions;

public class ExceptionExtensionsMutationTests : TestContainer
{
    public void IsOperationCanceledExceptionFromTokenShouldMatchTheTokenOfCancellationsWrappedInAnAggregateException()
    {
        using var source = new CancellationTokenSource();
        using var otherSource = new CancellationTokenSource();
        var aggregate = new AggregateException(
            new InvalidOperationException("unrelated"),
            new OperationCanceledException(source.Token));

        aggregate.IsOperationCanceledExceptionFromToken(source.Token).Should().BeTrue();
        aggregate.IsOperationCanceledExceptionFromToken(otherSource.Token).Should().BeFalse();
        aggregate.IsOperationCanceledExceptionFromToken(CancellationToken.None).Should().BeFalse();
    }

    public void IsOperationCanceledExceptionFromTokenShouldMatchWhenAnyWrappedCancellationUsesTheToken()
    {
        using var source = new CancellationTokenSource();
        using var otherSource = new CancellationTokenSource();
        var aggregate = new AggregateException(
            new OperationCanceledException(otherSource.Token),
            new OperationCanceledException(source.Token));

        aggregate.IsOperationCanceledExceptionFromToken(source.Token).Should().BeTrue();
        aggregate.IsOperationCanceledExceptionFromToken(otherSource.Token).Should().BeTrue();
    }

    public void IsOperationCanceledExceptionFromTokenShouldNotMatchAnAggregateExceptionWithoutCancellations()
    {
        using var source = new CancellationTokenSource();
        var aggregate = new AggregateException(new InvalidOperationException("unrelated"));

        aggregate.IsOperationCanceledExceptionFromToken(source.Token).Should().BeFalse();
    }
}
