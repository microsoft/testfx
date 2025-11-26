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
            .WithMessage("*name failed. {*");
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
        action.Should().Throw<AggregateException>()
            .WithInnerException<AssertFailedException>()
            .WithMessage("Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.Exception>. Actual exception: System.Exception: Exception of type 'System.Exception' was thrown.*'action' expression: '() => throw new Exception()'.");
    }

    public void ThrowsExactlyAsync_WhenExceptionIsDerivedFromExpectedType_ShouldThrow()
    {
        Task t = Assert.ThrowsExactlyAsync<ArgumentException>(() => throw new ArgumentNullException());
        Action action = t.Wait;
        action.Should().Throw<AggregateException>()
            .WithInnerException<AssertFailedException>()
            .WithMessage("Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentException>. Actual exception type:<System.ArgumentNullException>. Actual exception: System.ArgumentNullException: Value cannot be null.*'action' expression: '() => throw new ArgumentNullException()'.");
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
            .WithMessage("Assert.Throws failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. 'action' expression: '() => { }'. message constructed via builder.");

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
            .WithMessage("Assert.Throws failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. Actual exception: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values.*'action' expression: '() => throw new ArgumentOutOfRangeException(\"MyParamNameHere\")'. message constructed via builder.");

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
            .WithMessage("Assert.ThrowsExactly failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. 'action' expression: '() => { }'. message constructed via builder.");

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
            .WithMessage("Assert.ThrowsExactly failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. Actual exception: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values.*'action' expression: '() => throw new ArgumentOutOfRangeException(\"MyParamNameHere\")'. message constructed via builder.");

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
            .WithMessage("Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. 'action' expression: '() => Task.CompletedTask'. message constructed via builder.");

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
            .WithMessage("Assert.ThrowsAsync failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. Actual exception: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values.*'action' expression: '() => Task.FromException(new ArgumentOutOfRangeException(\"MyParamNameHere\"))'. message constructed via builder.");

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
            .WithMessage("Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentNullException> but no exception was thrown. 'action' expression: '() => Task.CompletedTask'. message constructed via builder.");

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
            .WithMessage("Assert.ThrowsExactlyAsync failed. Expected exception type:<System.ArgumentNullException>. Actual exception type:<System.ArgumentOutOfRangeException>. Actual exception: System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values.*'action' expression: '() => Task.FromException(new ArgumentOutOfRangeException(\"MyParamNameHere\"))'. message constructed via builder.");

        wasBuilderCalled.Should().BeTrue();
        exceptionPassedToBuilder.Should().BeOfType<ArgumentOutOfRangeException>();
        ((ArgumentOutOfRangeException)exceptionPassedToBuilder!).ParamName.Should().Be("MyParamNameHere");
    }
}
