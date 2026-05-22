// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides That extension to Assert class.
/// </summary>
[StackTraceHidden]
public static partial class AssertExtensions
{
    /// <summary>
    /// Provides That extension to Assert class.
    /// </summary>
    extension(Assert _)
    {
        /// <summary>
        /// Evaluates a boolean condition and throws an <see cref="AssertFailedException"/> if the condition is <see
        /// langword="false"/>.
        /// </summary>
        /// <param name="condition">An expression representing the condition to evaluate. Cannot be <see langword="null"/>.</param>
        /// <param name="message">An optional message to include in the exception if the assertion fails.</param>
        /// <param name="conditionExpression">The source code of the condition expression. This parameter is automatically populated by the compiler.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="condition"/> is <see langword="null"/>.</exception>
        /// <exception cref="AssertFailedException">Thrown if the evaluated condition is <see langword="false"/>.</exception>
#if NET7_0_OR_GREATER
        [RequiresDynamicCode("Calls Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.EvaluateExpression(Expression, Dictionary<Expression, Object>)")]
#endif
        public static void That(Expression<Func<bool>> condition, string? message = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
        {
            TelemetryCollector.TrackAssertionCall("Assert.That");

            if (condition == null)
            {
                throw new ArgumentNullException(nameof(condition));
            }

            Dictionary<Expression, object?>? evaluationCache = null;
            bool result;

            if (RequiresSinglePassEvaluation(condition.Body))
            {
                // Potentially side-effecting expressions must be evaluated once while caching values.
                evaluationCache = CreateEvaluationCache();
                result = EvaluateExpression(condition.Body, evaluationCache);
            }
            else
            {
                // For side-effect-free expressions, keep the fast path and only compute details on failures.
                result = condition.Compile().Invoke();
                if (result)
                {
                    return;
                }

                evaluationCache = CreateEvaluationCache();
                EvaluateAllSubExpressions(condition.Body, evaluationCache);
            }

            if (result)
            {
                return;
            }

            var sb = new StringBuilder();
            string expressionText = conditionExpression
                ?? throw new ArgumentNullException(nameof(conditionExpression));
            if (!string.IsNullOrWhiteSpace(message))
            {
                sb.AppendLine();
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.AssertThatMessageFormat, message));
            }

            string details = ExtractDetails(condition.Body, evaluationCache!);
            if (!string.IsNullOrWhiteSpace(details))
            {
                if (sb.Length == 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine(FrameworkMessages.AssertThatDetailsPrefix);
                sb.AppendLine(details);
            }

            Assert.ReportAssertFailed($"Assert.That({expressionText})", sb.ToString().TrimEnd());
        }
    }
}
