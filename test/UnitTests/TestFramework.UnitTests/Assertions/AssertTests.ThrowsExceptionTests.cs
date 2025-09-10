// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region ThrowAssertFailed tests
    // See https://github.com/dotnet/sdk/issues/25373
    public void ThrowAssertFailedDoesNotThrowIfMessageContainsInvalidStringFormatComposite()
    {
        Action action = () => Assert.ThrowAssertFailed("name", "{");
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("name failed. {");
    }
    #endregion

    #region ThrowsException tests
    public void ThrowsExceptionWithLambdaExpressionsShouldThrowAssertionOnNoException()
    {
        Action action = () => Assert.ThrowsException<ArgumentException>(() => { });
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException> but no exception was thrown. ");
    }

    public void ThrowsExceptionWithLambdaExpressionsShouldThrowAssertionOnWrongException()
    {
        Action action = () => Assert.ThrowsException<ArgumentException>(() => throw new FormatException());
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.FormatException>. ");
    }

    public void ThrowsException_FuncArgument_AllowsToReturnNull()
    {
        Action action = () => Assert.ThrowsException<ArgumentException>(() => null);
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException> but no exception was thrown. ");
    }

    public void ThrowsException_FuncArgumentOverloadWithMessage_AllowsToReturnNull()
    {
        Action action = () => Assert.ThrowsException<ArgumentException>(() => null, "message");
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException> but no exception was thrown. message");
    }

    public void ThrowsException_FuncArgumentOverloadWithMessagesAndParameters_AllowsToReturnNull()
    {
        Action action = () => Assert.ThrowsException<ArgumentException>(() => null, "message {0}", 1);
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.ThrowsException failed. Expected exception type:<System.ArgumentException> but no exception was thrown. message 1");
    }
    #endregion

    #region ThrowsExceptionAsync tests.
    public async Task ThrowsExceptionAsyncShouldNotThrowAssertionOnRightException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new ArgumentException();
            });

        // Should not throw an exception.
        await t.ConfigureAwait(false);
    }

    public void ThrowsExceptionAsyncShouldThrowAssertionOnNoException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await Task.Delay(5).ConfigureAwait(false));
        Action action = t.Wait;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;

        ex.InnerException.Should().NotBeNull();
        ex.InnerException!.Should().BeOfType<AssertFailedException>();
        ex.InnerException!.Message.Should().Be("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException> but no exception was thrown. ");
    }

    public void ThrowsExceptionAsyncShouldThrowAssertionOnWrongException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            });
        Action action = t.Wait;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;

        ex.InnerException.Should().NotBeNull();
        ex.InnerException!.Should().BeOfType<AssertFailedException>();
        ex.InnerException!.Message.Should().Be("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.FormatException>. ");
    }

    public void ThrowsExceptionAsyncWithMessageShouldThrowAssertionOnNoException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await Task.Delay(5).ConfigureAwait(false),
            "The world is not on fire.");
        Action action = t.Wait;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;

        ex.InnerException.Should().NotBeNull();
        ex.InnerException!.Should().BeOfType<AssertFailedException>();
        ex.InnerException!.Message.Should().Be("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException> but no exception was thrown. The world is not on fire.");
    }

    public void ThrowsExceptionAsyncWithMessageShouldThrowAssertionOnWrongException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            },
            "Happily ever after.");
        Action action = t.Wait;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;

        ex.InnerException.Should().NotBeNull();
        ex.InnerException!.Should().BeOfType<AssertFailedException>();
        ex.InnerException!.Message.Should().Be("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.FormatException>. Happily ever after.");
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullAction()
    {
        static void A()
        {
            Task t = Assert.ThrowsExceptionAsync<ArgumentException>(null!, null!, null);
            t.Wait();
        }

        Action action = A;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;

        ex.InnerException.Should().NotBeNull();
        ex.InnerException!.Should().BeOfType<ArgumentNullException>();
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowOnNullMessage()
    {
        static void A()
        {
            Task t = Assert.ThrowsExceptionAsync<ArgumentException>(async () => await Task.FromResult(true).ConfigureAwait(false), null!, null);
            t.Wait();
        }

        Action action = A;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;

        ex.InnerException.Should().NotBeNull();
        ex.InnerException!.Should().BeOfType<ArgumentNullException>();
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowAssertionOnNoException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await Task.Delay(5).ConfigureAwait(false),
            "The world is not on fire {0}.{1}-{2}.",
            "ta",
            "da",
            123);
        Action action = t.Wait;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;

        ex.InnerException.Should().NotBeNull();
        Assert.AreEqual(typeof(AssertFailedException), ex.InnerException.GetType());
        ex.InnerException!.Message.Should().Be("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException> but no exception was thrown. The world is not on fire ta.da-123.");
    }

    public void ThrowsExceptionAsyncWithMessageAndParamsShouldThrowAssertionOnWrongException()
    {
        Task t = Assert.ThrowsExceptionAsync<ArgumentException>(
            async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                throw new FormatException();
            },
            "Happily ever after. {0} {1}.",
            "The",
            "End");
        Action action = t.Wait;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;

        ex.InnerException.Should().NotBeNull();
        Assert.AreEqual(typeof(AssertFailedException), ex.InnerException.GetType());
        ex.InnerException!.Message.Should().Be("Assert.ThrowsExceptionAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.FormatException>. Happily ever after. The End.");
    }
    #endregion

    public void Throws_WhenExceptionIsDerivedFromExpectedType_ShouldNotThrow()
        => Assert.Throws<ArgumentException>(() => throw new ArgumentNullException());

    public void Throws_WhenExceptionIsNotExpectedType_ShouldThrow()
    {
        static void Action() => Assert.Throws<ArgumentException>(() => throw new Exception());
        Action action = Action;
        action.Should().Throw<AssertFailedException>();
    }

    public void Throws_WithInterpolation()
    {
        static string GetString() => throw new Exception();

#pragma warning disable IDE0200 // Remove unnecessary lambda expression - intentionally testing overload resolution for this case.
        Exception ex = Assert.Throws<Exception>(() => GetString(), $"Hello {GetString()}");
#pragma warning restore IDE0200 // Remove unnecessary lambda expression
        Exception ex2 = Assert.Throws<Exception>(GetString, $"Hello {GetString()}");
        ex.Should().NotBeNull();
        ex2.Should().NotBeNull();
    }

    public void ThrowsExactly_WithInterpolation()
    {
        static string GetString() => throw new Exception();

#pragma warning disable IDE0200 // Remove unnecessary lambda expression - intentionally testing overload resolution for this case.
        Exception ex = Assert.ThrowsExactly<Exception>(() => GetString(), $"Hello {GetString()}");
#pragma warning restore IDE0200 // Remove unnecessary lambda expression
        Exception ex2 = Assert.ThrowsExactly<Exception>(GetString, $"Hello {GetString()}");
        ex.Should().NotBeNull();
        ex2.Should().NotBeNull();
    }

    public void ThrowsExactly_WhenExceptionIsDerivedFromExpectedType_ShouldThrow()
    {
        static void Action() => Assert.ThrowsExactly<ArgumentException>(() => throw new ArgumentNullException());
        Action action = Action;
        action.Should().Throw<AssertFailedException>();
    }

    public void ThrowsExactly_WhenExceptionExpectedType_ShouldNotThrow()
        => Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentNullException());

    public async Task ThrowsAsync_WhenExceptionIsDerivedFromExpectedType_ShouldNotThrow()
        => await Assert.ThrowsAsync<ArgumentException>(() => throw new ArgumentNullException());

    public void ThrowsAsync_WhenExceptionIsNotExpectedType_ShouldThrow()
    {
        Task t = Assert.ThrowsAsync<ArgumentException>(() => throw new Exception());
        Action action = t.Wait;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;
        ex.InnerException.Should().BeOfType<AssertFailedException>();
        ex.InnerException!.Message.Should().Be("Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.Exception>. ");
    }

    public void ThrowsExactlyAsync_WhenExceptionIsDerivedFromExpectedType_ShouldThrow()
    {
        Task t = Assert.ThrowsExactlyAsync<ArgumentException>(() => throw new ArgumentNullException());
        Action action = t.Wait;
        AggregateException ex = action.Should().Throw<AggregateException>().Which;
        ex.InnerException.Should().BeOfType<AssertFailedException>();
        ex.InnerException!.Message.Should().Be("Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.ArgumentNullException>. ");
    }

    public void Throws_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        Assert.Throws<ArgumentNullException>(() => throw new ArgumentNullException(), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        wasBuilderCalled.Should().BeFalse();
    }

    public void Throws_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Action action = () => Assert.Throws<ArgumentNullException>(() => { }, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.Throws failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeNull();
    }

    public void Throws_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Action action = () => Assert.Throws<ArgumentNullException>(() => throw new ArgumentOutOfRangeException("MyParamNameHere"), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.Throws failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }

    public void ThrowsExactly_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentNullException(), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        wasBuilderCalled.Should().BeFalse();
    }

    public void ThrowsExactly_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Action action = () => Assert.ThrowsExactly<ArgumentNullException>(() => { }, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.ThrowsExactly failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeNull();
    }

    public void ThrowsExactly_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Action action = () => Assert.ThrowsExactly<ArgumentNullException>(() => throw new ArgumentOutOfRangeException("MyParamNameHere"), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("Assert.ThrowsExactly failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }

    public async Task ThrowsAsync_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromException(new ArgumentNullException()), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        wasBuilderCalled.Should().BeFalse();
    }

    public async Task ThrowsAsync_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Func<Task> action = async () => await Assert.ThrowsAsync<ArgumentNullException>(() => Task.CompletedTask, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        (await action.Should().ThrowAsync<AssertFailedException>())
            .And.Message.Should().Be("Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeNull();
    }

    public async Task ThrowsAsync_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Func<Task> action = async () => await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromException(new ArgumentOutOfRangeException("MyParamNameHere")), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        (await action.Should().ThrowAsync<AssertFailedException>())
            .And.Message.Should().Be("Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_Passes()
    {
        bool wasBuilderCalled = false;
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.FromException(new ArgumentNullException()), messageBuilder: _ =>
        {
            wasBuilderCalled = true;
            return "message constructed via builder.";
        });

        wasBuilderCalled.Should().BeFalse();
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_FailsBecauseNoException()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Func<Task> action = async () => await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.CompletedTask, messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        (await action.Should().ThrowAsync<AssertFailedException>())
            .And.Message.Should().Be("Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeNull();
    }

    public async Task ThrowsExactlyAsync_WithMessageBuilder_FailsBecauseTypeMismatch()
    {
        bool wasBuilderCalled = false;
        Exception? exceptionPassedToBuilder = null;
        Func<Task> action = async () => await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Task.FromException(new ArgumentOutOfRangeException("MyParamNameHere")), messageBuilder: ex =>
        {
            wasBuilderCalled = true;
            exceptionPassedToBuilder = ex;
            return "message constructed via builder.";
        });
        (await action.Should().ThrowAsync<AssertFailedException>())
            .And.Message.Should().Be("Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }
}
