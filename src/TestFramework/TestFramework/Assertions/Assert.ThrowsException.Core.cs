// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    private static TException ThrowsException<TException>(Action action, bool isStrictType, string? message, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall(GetTrackedThrowsName(assertMethodName));

        _ = action ?? throw new ArgumentNullException(nameof(action));
        _ = message ?? throw new ArgumentNullException(nameof(message));

        ThrowsExceptionState state = IsThrowsFailing<TException>(action, isStrictType);
        if (state.FailureKind != ThrowsFailureKind.NotFailing)
        {
            ReportThrowsFailed<TException>(isStrictType, state, message, actionExpression, assertMethodName);
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
        return null!;
    }

    private static TException ThrowsException<TException>(Action action, bool isStrictType, Func<Exception?, string> messageBuilder, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall(GetTrackedThrowsName(assertMethodName));

        _ = action ?? throw new ArgumentNullException(nameof(action));
        _ = messageBuilder ?? throw new ArgumentNullException(nameof(messageBuilder));

        ThrowsExceptionState state = IsThrowsFailing<TException>(action, isStrictType);
        if (state.FailureKind != ThrowsFailureKind.NotFailing)
        {
            ReportThrowsFailed<TException>(isStrictType, state, messageBuilder(state.ExceptionThrown), actionExpression, assertMethodName);
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
        return null!;
    }

    private static async Task<TException> ThrowsExceptionAsync<TException>(Func<Task> action, bool isStrictType, string? message, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall(GetTrackedThrowsName(assertMethodName));

        _ = action ?? throw new ArgumentNullException(nameof(action));
        _ = message ?? throw new ArgumentNullException(nameof(message));

        ThrowsExceptionState state = await IsThrowsAsyncFailingAsync<TException>(action, isStrictType).ConfigureAwait(false);
        if (state.FailureKind != ThrowsFailureKind.NotFailing)
        {
            ReportThrowsFailed<TException>(isStrictType, state, message, actionExpression, assertMethodName);
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
        return null!;
    }

    private static async Task<TException> ThrowsExceptionAsync<TException>(Func<Task> action, bool isStrictType, Func<Exception?, string> messageBuilder, string actionExpression, [CallerMemberName] string assertMethodName = "")
        where TException : Exception
    {
        TelemetryCollector.TrackAssertionCall(GetTrackedThrowsName(assertMethodName));

        _ = action ?? throw new ArgumentNullException(nameof(action));
        _ = messageBuilder ?? throw new ArgumentNullException(nameof(messageBuilder));

        ThrowsExceptionState state = await IsThrowsAsyncFailingAsync<TException>(action, isStrictType).ConfigureAwait(false);
        if (state.FailureKind != ThrowsFailureKind.NotFailing)
        {
            ReportThrowsFailed<TException>(isStrictType, state, messageBuilder(state.ExceptionThrown), actionExpression, assertMethodName);
        }
        else
        {
            return (TException)state.ExceptionThrown!;
        }

        // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
        return null!;
    }

    [DebuggerDisableUserUnhandledExceptions]
    private static async Task<ThrowsExceptionState> IsThrowsAsyncFailingAsync<TException>(Func<Task> action, bool isStrictType)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            bool isExceptionOfType = isStrictType
                ? typeof(TException) == ex.GetType()
                : ex is TException;

            return isExceptionOfType
                ? ThrowsExceptionState.CreateNotFailingState(ex)
                : ThrowsExceptionState.CreateWrongTypeState(ex);
        }

        return ThrowsExceptionState.CreateNoExceptionState();
    }

    [DebuggerDisableUserUnhandledExceptions]
    private static ThrowsExceptionState IsThrowsFailing<TException>(Action action, bool isStrictType)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            bool isExceptionOfType = isStrictType
                ? typeof(TException) == ex.GetType()
                : ex is TException;

            return isExceptionOfType
                ? ThrowsExceptionState.CreateNotFailingState(ex)
                : ThrowsExceptionState.CreateWrongTypeState(ex);
        }

        return ThrowsExceptionState.CreateNoExceptionState();
    }

    [StackTraceHidden]
    private static void ReportThrowsFailed<TException>(
        bool isStrictType,
        ThrowsExceptionState state,
        string? userMessage,
        string actionExpression,
        string assertMethodName)
        where TException : Exception
    {
        Type expectedType = typeof(TException);
        string expectedTypeName = GetDisplayTypeName(expectedType, includeNamespace: false);
        string expectedTypeFullName = GetDisplayTypeName(expectedType, includeNamespace: true);

        StructuredAssertionMessage message;

        if (state.FailureKind == ThrowsFailureKind.NoExceptionThrown)
        {
            string summary = isStrictType
                ? $"Expected exception of exact type {expectedTypeName} but no exception was thrown."
                : $"Expected exception of type {expectedTypeName} (or derived) but no exception was thrown.";

            message = new StructuredAssertionMessage(summary)
                .WithUserMessage(userMessage)
                .WithCallSiteExpression(FormatCallSiteExpression($"Assert.{assertMethodName}<{expectedTypeName}>", actionExpression, "action"));
        }
        else
        {
            Exception actualException = state.ExceptionThrown!;
            Type actualType = actualException.GetType();
            string actualTypeName = GetDisplayTypeName(actualType, includeNamespace: false);

            string summary = isStrictType
                ? $"Expected exception of exact type {expectedTypeName} but caught {actualTypeName}."
                : $"Expected exception of type {expectedTypeName} (or derived) but caught {actualTypeName}.";

            string expectedTypeLabel = isStrictType ? expectedTypeFullName : $"{expectedTypeFullName} (or derived)";

            // Render the full exception (type, message, inner-exception chain and stack trace) via ToString so the
            // unexpected exception can be diagnosed without re-running under a debugger. See issue #9190.
            // Exception.ToString() prefixes the output with Type.ToString(), which uses CLR notation for generic
            // types (e.g. "MyException`1[System.Int32]"). Replace that leading prefix with the friendly name so it
            // stays consistent with the "expected type:" line; for non-generic types the two notations are identical.
            string actualExceptionText = actualException.ToString();
            string clrTypeName = actualType.ToString();
            if (actualExceptionText.StartsWith(clrTypeName, StringComparison.Ordinal))
            {
                actualExceptionText = GetDisplayTypeName(actualType, includeNamespace: true) + actualExceptionText[clrTypeName.Length..];
            }

            EvidenceBlock evidence = EvidenceBlock.Create()
                .AddLine("expected type:", expectedTypeLabel)
                .AddLine("actual exception:", actualExceptionText);

            message = new StructuredAssertionMessage(summary)
                .WithUserMessage(userMessage)
                .WithEvidence(evidence)
                .WithCallSiteExpression(FormatCallSiteExpression($"Assert.{assertMethodName}<{expectedTypeName}>", actionExpression, "action"));
        }

        ReportAssertFailed(message);
    }

    // Renders a type name without the CLR backtick-arity suffix and with closed generic arguments expanded recursively
    // (e.g. "MyException`1" with int → "MyException<Int32>") so it stays readable in summary lines and pasteable in call-site lines.
    private static string GetDisplayTypeName(Type type, bool includeNamespace)
    {
        if (!type.IsGenericType)
        {
            return includeNamespace ? type.FullName ?? type.Name : type.Name;
        }

        string name = type.Name;
        int tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name[..tick];
        }

        if (includeNamespace && !string.IsNullOrEmpty(type.Namespace))
        {
            name = $"{type.Namespace}.{name}";
        }

        Type[] args = type.GetGenericArguments();
        StringBuilder sb = new(name.Length + 16);
        sb.Append(name);
        sb.Append('<');
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(GetDisplayTypeName(args[i], includeNamespace));
        }

        sb.Append('>');
        return sb.ToString();
    }

    private enum ThrowsFailureKind : byte
    {
        NotFailing,
        NoExceptionThrown,
        WrongExceptionType,
    }

    [StackTraceHidden]
    private readonly struct ThrowsExceptionState
    {
        public Exception? ExceptionThrown { get; }

        public ThrowsFailureKind FailureKind { get; }

        private ThrowsExceptionState(ThrowsFailureKind failureKind, Exception? exceptionThrown)
        {
            ExceptionThrown = exceptionThrown;
            FailureKind = failureKind;
        }

        public static ThrowsExceptionState CreateWrongTypeState(Exception exceptionThrown)
            => new(ThrowsFailureKind.WrongExceptionType, exceptionThrown);

        public static ThrowsExceptionState CreateNoExceptionState()
            => new(ThrowsFailureKind.NoExceptionThrown, null);

        public static ThrowsExceptionState CreateNotFailingState(Exception exception)
            => new(ThrowsFailureKind.NotFailing, exception);
    }

    // assertMethodName comes from [CallerMemberName] for the Throws/ThrowsExactly/ThrowsAsync/
    // ThrowsExactlyAsync helpers — a small fixed set. Use a switch to avoid allocating a fresh
    // "Assert." + name string on every call.
    private static string GetTrackedThrowsName(string assertMethodName)
        => assertMethodName switch
        {
            "Throws" => "Assert.Throws",
            "ThrowsExactly" => "Assert.ThrowsExactly",
            "ThrowsAsync" => "Assert.ThrowsAsync",
            "ThrowsExactlyAsync" => "Assert.ThrowsExactlyAsync",
            _ => string.Concat("Assert.", assertMethodName),
        };
}
