// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class JsoniteTests
{
    [TestMethod]
    public void Serialize_DateTimeOffset()
    {
        string actual = Jsonite.Json.Serialize(new DateTimeOffset(2023, 01, 01, 01, 01, 01, 01, TimeSpan.Zero));

        // Assert
        Assert.AreEqual("2023-01-01T01:01:01.0010000+00:00", actual.Trim('"'));
    }

    [TestMethod]
    public void Deserialize_CommentsAreDisallowedByDefault()
        => Assert.ThrowsExactly<Jsonite.JsonException>(() => Jsonite.Json.Deserialize("{// Comment\n\"value\": true}"));

    [TestMethod]
    public void Deserialize_CommentsCanBeAllowed()
    {
        Jsonite.JsonSettings settings = new()
        {
            AllowComments = true,
        };

        var result = (Jsonite.JsonObject)Jsonite.Json.Deserialize("{// Line comment\n\"value\": /* Block comment */ true}", settings);

        Assert.IsTrue((bool)result["value"]!);
    }

    [TestMethod]
    public void Deserialize_UnterminatedBlockCommentThrows()
    {
        Jsonite.JsonSettings settings = new()
        {
            AllowComments = true,
        };

        Assert.ThrowsExactly<Jsonite.JsonException>(() => Jsonite.Json.Deserialize("{/* Comment", settings));
    }

    [TestMethod]
    public void Deserialize_DuplicatePropertiesKeepLastValue()
    {
        Jsonite.JsonObject result = Assert.IsInstanceOfType<Jsonite.JsonObject>(
            Jsonite.Json.Deserialize("""{"value":1,"value":[false,null,{"value":2}]}"""));

        Assert.HasCount(1, result);
        Jsonite.JsonArray values = Assert.IsInstanceOfType<Jsonite.JsonArray>(result["value"]);
        Assert.HasCount(3, values);
        Assert.AreEqual(false, values[0]);
        Assert.IsNull(values[1]);
        Jsonite.JsonObject nested = Assert.IsInstanceOfType<Jsonite.JsonObject>(values[2]);
        Assert.AreEqual(2, nested["value"]);
    }

    [TestMethod]
    public void Deserialize_PrimitivesPreserveValuesAndNumericTypes()
    {
        Jsonite.JsonArray result = Assert.IsInstanceOfType<Jsonite.JsonArray>(
            Jsonite.Json.Deserialize("""[true,false,null,"\"\\\/\b\f\n\r\t\u0041",-999999999,2147483647,2147483648,9223372036854775808,18446744073709551616,1.25,1e2]"""));
        object?[] expected =
        [
            true, false, null, "\"\\/\b\f\n\r\tA", -999999999, int.MaxValue,
            2147483648L, 9223372036854775808UL, 18446744073709551616m, 1.25d, 100d,
        ];

        Assert.AreSequenceEqual(expected, result);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i]?.GetType(), result[i]?.GetType());
        }
    }

    [TestMethod]
    public void Deserialize_PrimitiveSettingsPreserveTextAndDecimal()
    {
        Jsonite.JsonSettings textSettings = new() { ParseValuesAsStrings = true };
        Jsonite.JsonArray textValues = Assert.IsInstanceOfType<Jsonite.JsonArray>(
            Jsonite.Json.Deserialize("[true,false,null,-0,1.20e+2]", textSettings));
        Assert.AreSequenceEqual(new object?[] { "true", "false", null, "-0", "1.20e+2" }, textValues);

        Jsonite.JsonSettings decimalSettings = new() { ParseFloatAsDecimal = true };
        Assert.AreEqual(1.25m, Assert.IsInstanceOfType<decimal>(Jsonite.Json.Deserialize("1.25", decimalSettings)));
    }

    [TestMethod]
    public void Deserialize_EmptyValuesAndTrailingContent()
    {
        Assert.IsNull(Jsonite.Json.Deserialize(" \r\n\t"));
        Assert.IsEmpty(Assert.IsInstanceOfType<Jsonite.JsonObject>(Jsonite.Json.Deserialize("{}")));
        Assert.IsEmpty(Assert.IsInstanceOfType<Jsonite.JsonArray>(Jsonite.Json.Deserialize("[]")));
        Assert.AreEqual(true, Jsonite.Json.Deserialize("true false"));
    }

    [TestMethod]
    [DataRow("[1,]", 3, 0, 3, "Unexpected character ']' while parsing an array. Expecting a STRING, NUMBER, OBJECT, ARRAY, true, false or null after a comma ','")]
    [DataRow("{\n\"x\": 01}", 8, 1, 6, "Unexpected character '1' while parsing a number. The number '0' must followed by '.' or by an exponent or nothing")]
    [DataRow("[tru]", 4, 0, 4, "Unexpected character ']' while trying to parse a BOOL 'true' value")]
    [DataRow("""["\u12x4"]""", 6, 0, 6, "Unexpected character 'x' while parsing a string. Expecting only hexadecimals [0-9a-fA-F] after escape \\u")]
    [DataRow("[1e+]", 4, 0, 4, "Unexpected character ']' while parsing the exponent of a number. Expecting a digit 0-9 after an exponent")]
    public void Deserialize_MalformedValuesPreserveDiagnostics(string input, int offset, int line, int column, string message)
    {
        Jsonite.JsonException exception = Assert.ThrowsExactly<Jsonite.JsonException>(() => Jsonite.Json.Deserialize(input));

        Assert.AreEqual(offset, exception.Offset);
        Assert.AreEqual(line, exception.Line);
        Assert.AreEqual(column, exception.Column);
        Assert.AreEqual(message, exception.Message);
    }

    [TestMethod]
    public void Deserialize_MaxDepthPreservesDiagnostic()
    {
        Jsonite.JsonSettings settings = new() { MaxDepth = 1 };
        Jsonite.JsonException exception = Assert.ThrowsExactly<Jsonite.JsonException>(
            () => Jsonite.Json.Deserialize("[[]]", settings));

        Assert.AreEqual(1, exception.Offset);
        Assert.AreEqual(0, exception.Line);
        Assert.AreEqual(1, exception.Column);
        Assert.AreEqual(
            "The maximum allowed depth [{settings.MaxDepth}] level has been reached. The object graph is too deep",
            exception.Message);
    }

    [TestMethod]
    public void SerializeJsoniteInvalidStringHighSurrogateAtTheEnd()
    {
        const string Input = "Hello\uD800";
        string actual = Jsonite.Json.Serialize(Input);
        Assert.AreEqual("\"Hello\\uFFFD\"", actual);
    }

    [TestMethod]
    public void SerializeJsoniteInvalidStringHighSurrogateNotFollowedByLowSurrogate()
    {
        const string Input = "Hello\uD800A";
        string actual = Jsonite.Json.Serialize(Input);
        Assert.AreEqual("\"Hello\\uFFFDA\"", actual);
    }

    [TestMethod]
    public void SerializeJsoniteInvalidStringLowSurrogateWithoutPreviousHighSurrogate()
    {
        const string Input = "Hello\uDC00A";
        string actual = Jsonite.Json.Serialize(Input);
        Assert.AreEqual("\"Hello\\uFFFDA\"", actual);
    }

    [TestMethod]
    public void SerializeJsoniteValidSurrogatePair()
    {
        const string Input = "Hello\uD800\uDC00A";
        string actual = Jsonite.Json.Serialize(Input);
        Assert.AreEqual("\"Hello\\uD800\\uDC00A\"", actual);
    }

    [TestMethod]
    public void Serialize_SpecialCharacters()
    {
        // This test is testing if we can serialize the range 0x0000 - 0x001FF correctly, this range contains special characters like NUL.
        // This is a fix for Jsonite, which throws when such characters are found in a string (but does not fail when we provide them as character).
        List<Exception> errors = [];

        // This could be converted to Data source, but this way we have more control about where in the result message the
        // special characters will be (hopefully nowhere) so in case of failure, we can still serialize the message to IDE
        // even if the serializer does not support special characters.
        foreach (char character in Enumerable.Range(0x0000, 0x001F).Select(v => (char)v))
        {
            // Convert the char to string, otherwise there is no failure.
            string text = $"{character}";

            // Serialize text via Jsonite, this is where the error used to happen.
            string jsoniteText = Jsonite.Json.Serialize(text);

            // Serialize text via System.Text.Json to get our control examples, preserving special characters in text is
            // hard, and so we better test against a known, hopefully good state.
            string stjText = System.Text.Json.JsonSerializer.Serialize(text);

            // This is our expected result, to do it the way System.Text.Json does it.
            string? deserializeStjViaStj = System.Text.Json.JsonSerializer.Deserialize<string>(stjText);

            // Make sure we can deserialize the messages we send from server to VS.
            string? deserializeJsoniteViaStj = System.Text.Json.JsonSerializer.Deserialize<string>(jsoniteText);

            // Make sure we can deserialie messages that VS sends to our server.
            string? deserializeStjViaJsonite = (string)Jsonite.Json.Deserialize(stjText);

            // Make sure we can deserialize messages we produced, to know the Jsonite code is handling all the cases.
            string? deserializeJsoniteViaJsonite = (string)Jsonite.Json.Deserialize(jsoniteText);

            try
            {
                Assert.AreEqual(deserializeStjViaStj, deserializeJsoniteViaStj);
                Assert.AreEqual(deserializeStjViaStj, deserializeStjViaJsonite);
                Assert.AreEqual(deserializeStjViaStj, deserializeJsoniteViaJsonite);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new Exception(string.Join(Environment.NewLine, errors));
        }
    }
}

#endif
