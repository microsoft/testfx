// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class JsonStringWriterTests
{
    [TestMethod]
    public void JsonWriter_ControlAndSurrogatePartitionsProduceExactAsciiDocument()
    {
        JsonStringWriter writer = new();
        writer.WriteStartObject();
        writer.WriteString("escaped", "\b\f\n\r\t\\\"\0\u001f\u007fΩ\uD83D\uDE80\uDC00\uD800x");
        writer.WriteString("nil", null);
        writer.WriteStartArray("rows");
        writer.WriteStringValue(null);
        writer.WriteStringValue("row");
        writer.WriteEndArray();
        writer.WriteEndObject();

        Assert.AreEqual(
            "\n" + """
            {
              "escaped": "\b\f\n\r\t\\\"\u0000\u001f\u007f\u03a9\ud83d\ude80\ufffd\ufffdx",
              "nil": null,
              "rows": [
                null,
                "row"
              ]
            }
            """.Replace("\r\n", "\n"),
            writer.ToString());
    }

    [TestMethod]
    [DataRow(false, "JSON writer mismatch: expected to close Object but got Array.")]
    [DataRow(true, "JSON writer mismatch: expected to close Array but got Object.")]
    public void JsonWriter_MismatchedContainerKindReportsBothKinds(bool array, string expected)
    {
        JsonStringWriter writer = new();
        if (array)
        {
            writer.WriteStartArray("items");
        }
        else
        {
            writer.WriteStartObject();
        }

        Assert.AreEqual(
            expected,
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                if (array)
                {
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteEndArray();
                }
            }).Message);
    }
}
