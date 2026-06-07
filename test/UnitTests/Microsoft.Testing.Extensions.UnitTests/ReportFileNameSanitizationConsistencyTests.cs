// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public class ReportFileNameSanitizationConsistencyTests
{
    private static readonly MethodInfo TrxSanitizeMethod =
        typeof(TrxReportEngine).GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve TrxReportEngine.ReplaceInvalidFileNameChars.");

    private static readonly MethodInfo HtmlSanitizeMethod =
        typeof(HtmlReportEngine).GetMethod("ReplaceInvalidFileNameChars", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not resolve HtmlReportEngine.ReplaceInvalidFileNameChars.");

    [TestMethod]
    [DynamicData(nameof(GetSanitizationInputs))]
    public void TrxAndHtmlReportEngines_UseSameFileNameSanitizationRules(string fileName)
    {
        string trxSanitized = InvokeSanitizer(TrxSanitizeMethod, fileName);
        string htmlSanitized = InvokeSanitizer(HtmlSanitizeMethod, fileName);

        Assert.AreEqual(trxSanitized, htmlSanitized);
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
