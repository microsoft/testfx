// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class ReportFileNameSanitizationConsistencyTests
{
    // ReportFileNameSanitizer is an internal type linked into every report-engine assembly.
    // The test project has InternalsVisibleTo access to several of them, which creates a
    // CS0433 ambiguity when using the type name directly. Use the TrxReport assembly as the
    // unambiguous anchor to retrieve the shared sanitizer method via reflection.
    private static readonly MethodInfo SanitizeMethod =
        (typeof(TrxReportEngine).Assembly
            .GetType("Microsoft.Testing.Extensions.ReportFileNameSanitizer")
            ?? throw new InvalidOperationException("Could not find type ReportFileNameSanitizer in TrxReport assembly."))
            .GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve ReportFileNameSanitizer.ReplaceInvalidFileNameChars.");

    [TestMethod]
    [DynamicData(nameof(GetSanitizationInputs))]
    public void AllReportEngines_UseSharedFileNameSanitizationRules(string fileName)
    {
        // All report engines (Trx, Html, JUnit, Ctrf) delegate to the shared
        // ReportFileNameSanitizer.ReplaceInvalidFileNameChars. Verify it handles
        // edge-case file names without throwing and returns a non-empty result.
        string? sanitized = (string?)SanitizeMethod.Invoke(null, [fileName]);

        Assert.IsNotNull(sanitized);
        Assert.AreNotEqual(0, sanitized.Length, $"Sanitized file name for '{fileName}' must not be empty.");
    }

    public static IEnumerable<object[]> GetSanitizationInputs()
    {
        yield return ["report_{pname}.trx"];
        yield return ["contains<invalid>|chars?.trx"];
        yield return ["file\0name.trx"];
        yield return ["name\u001fcontrol.html"];
        yield return ["CON"];
        yield return ["COM1.trx"];
        yield return ["CLOCK$.html"];
        yield return [@"\\server\share\NUL.trx"];
    }
}
