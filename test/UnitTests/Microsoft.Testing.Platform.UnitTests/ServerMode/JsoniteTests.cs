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
