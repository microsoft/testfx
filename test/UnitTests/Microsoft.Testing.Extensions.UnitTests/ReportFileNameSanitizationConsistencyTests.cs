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
    private const string ReportFileNameSanitizerTypeName = "Microsoft.Testing.Extensions.ReportFileNameSanitizer";

    private static readonly MethodInfo TrxSanitizeMethod =
        typeof(TrxReportEngine).GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve TrxReportEngine.ReplaceInvalidFileNameChars.");

    // HtmlReport and JUnitReport delegate sanitization to the shared ReportFileNameSanitizer via
    // ReportEngineBase.BuildDefaultFileName — get that shared type from each engine's assembly.
    private static readonly MethodInfo HtmlSanitizeMethod = GetSanitizerMethod(typeof(HtmlReportEngine).Assembly, "HtmlReport");

    private static readonly MethodInfo JUnitSanitizeMethod = GetSanitizerMethod(typeof(JUnitReportEngine).Assembly, "JUnitReport");

    private static MethodInfo GetSanitizerMethod(Assembly assembly, string assemblyAlias)
    {
        Type? sanitizerType = assembly.GetType(ReportFileNameSanitizerTypeName);
        return sanitizerType?.GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Could not resolve {ReportFileNameSanitizerTypeName}.ReplaceInvalidFileNameChars from {assemblyAlias} assembly.");
    }

    private static readonly MethodInfo CtrfSanitizeMethod =
        typeof(CtrfReportEngine).GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static)
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
