// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.CtrfReport;
using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Extensions.JUnitReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class ReportFileNameSanitizationConsistencyTests
{
    private static readonly MethodInfo TrxSanitizeMethod =
        typeof(TrxReportEngine).GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve TrxReportEngine.ReplaceInvalidFileNameChars.");

    // CtrfReportEngine, HtmlReportEngine and JUnitReportEngine inherit ReplaceInvalidFileNameChars from
    // ReportEngineBase, so we flatten the hierarchy to resolve the inherited protected static method.
    private static readonly MethodInfo HtmlSanitizeMethod =
        typeof(HtmlReportEngine).GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        ?? throw new InvalidOperationException("Could not resolve HtmlReportEngine.ReplaceInvalidFileNameChars.");

    private static readonly MethodInfo JUnitSanitizeMethod =
        typeof(JUnitReportEngine).GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        ?? throw new InvalidOperationException("Could not resolve JUnitReportEngine.ReplaceInvalidFileNameChars.");

    private static readonly MethodInfo CtrfSanitizeMethod =
        typeof(CtrfReportEngine).GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        ?? throw new InvalidOperationException("Could not resolve CtrfReportEngine.ReplaceInvalidFileNameChars.");

    [TestMethod]
    [DynamicData(nameof(GetSanitizationInputs))]
    public void TrxAndHtmlReportEngines_UseSameFileNameSanitizationRules(string fileName)
    {
        string expectedSanitizedFileName = InvokeSanitizer(TrxSanitizeMethod, fileName);
        string actualSanitizedFileName = InvokeSanitizer(HtmlSanitizeMethod, fileName);

        Assert.AreEqual(expectedSanitizedFileName, actualSanitizedFileName, $"Sanitization mismatch for file name '{fileName}'.");
    }

    [TestMethod]
    [DynamicData(nameof(GetSanitizationInputs))]
    public void TrxAndJUnitReportEngines_UseSameFileNameSanitizationRules(string fileName)
    {
        string expectedSanitizedFileName = InvokeSanitizer(TrxSanitizeMethod, fileName);
        string actualSanitizedFileName = InvokeSanitizer(JUnitSanitizeMethod, fileName);

        Assert.AreEqual(expectedSanitizedFileName, actualSanitizedFileName, $"Sanitization mismatch for file name '{fileName}'.");
    }

    [TestMethod]
    [DynamicData(nameof(GetSanitizationInputs))]
    public void TrxAndCtrfReportEngines_UseSameFileNameSanitizationRules(string fileName)
    {
        string expectedSanitizedFileName = InvokeSanitizer(TrxSanitizeMethod, fileName);
        string actualSanitizedFileName = InvokeSanitizer(CtrfSanitizeMethod, fileName);

        Assert.AreEqual(expectedSanitizedFileName, actualSanitizedFileName, $"Sanitization mismatch for file name '{fileName}'.");
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

    private static string InvokeSanitizer(MethodInfo method, string fileName)
        => (string)(method.Invoke(null, [fileName]) ?? throw new InvalidOperationException("Sanitizer returned null."));
}
