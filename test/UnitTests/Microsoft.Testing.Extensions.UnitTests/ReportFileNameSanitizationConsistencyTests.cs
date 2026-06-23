// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class ReportFileNameSanitizationConsistencyTests
{
    // ReportFileNameSanitizer is an internal type that is *linked* (compiled) into every
    // report-engine assembly, so referencing it by name directly is ambiguous (CS0433).
    // Resolve the shared method from each engine assembly via reflection, anchoring on one
    // public type per assembly. Verifying every engine's copy produces identical output is
    // what actually proves the engines share the same sanitization rules.
    private static readonly (string Engine, MethodInfo Sanitize)[] Sanitizers =
    [
        ("TrxReport", GetSanitizer(typeof(TrxReportEngine).Assembly)),
        ("HtmlReport", GetSanitizer(typeof(HtmlReportExtensions).Assembly)),
        ("JUnitReport", GetSanitizer(typeof(JUnitReportExtensions).Assembly)),
        ("CtrfReport", GetSanitizer(typeof(CtrfReportExtensions).Assembly)),
    ];

    private static MethodInfo GetSanitizer(Assembly assembly)
        => (assembly.GetType("Microsoft.Testing.Extensions.ReportFileNameSanitizer")
                ?? throw new InvalidOperationException(
                    $"Could not find type ReportFileNameSanitizer in assembly '{assembly.GetName().Name}'."))
            .GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Could not resolve ReportFileNameSanitizer.ReplaceInvalidFileNameChars in assembly '{assembly.GetName().Name}'.");

    [TestMethod]
    [DynamicData(nameof(GetSanitizationInputs))]
    public void AllReportEngines_UseSharedFileNameSanitizationRules(string fileName)
    {
        // Invoke the ReportFileNameSanitizer that is linked into every report-engine assembly
        // (Trx, Html, JUnit, Ctrf) and assert they all sanitize the same input to the same,
        // non-empty result. If any engine diverged (different or out-of-date sanitizer), the
        // cross-engine equality assertions below would fail.
        (string anchorEngine, MethodInfo anchorSanitize) = Sanitizers[0];
        string? expected = (string?)anchorSanitize.Invoke(null, [fileName]);

        Assert.IsNotNull(expected);
        Assert.AreNotEqual(0, expected.Length, $"Sanitized file name for '{fileName}' must not be empty.");

        for (int i = 1; i < Sanitizers.Length; i++)
        {
            (string engine, MethodInfo sanitize) = Sanitizers[i];
            string? actual = (string?)sanitize.Invoke(null, [fileName]);
            Assert.AreEqual(
                expected,
                actual,
                $"{engine} sanitized '{fileName}' to '{actual}', but {anchorEngine} produced '{expected}'. "
                + "All report engines must share identical file-name sanitization rules.");
        }
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
