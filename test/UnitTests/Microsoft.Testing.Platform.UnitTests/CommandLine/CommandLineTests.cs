// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CommandLineTests
{
    // The test method ParserTests is parameterized and one of the parameter needs to be CommandLineParseResult.
    // The test method has to be public to be run, but CommandLineParseResult is internal.
    // So, we introduce this wrapper to be used instead so that the test method can be made public.
    public sealed class CommandLineParseResultWrapper
    {
        internal CommandLineParseResultWrapper(string? toolName, IReadOnlyList<CommandLineParseOption> options, IReadOnlyList<string> errors)
            => Result = new CommandLineParseResult(toolName, options, errors);

        internal CommandLineParseResult Result { get; }
    }

    [TestMethod]
    [DynamicData(nameof(ParserTestsData), DynamicDataDisplayName = nameof(ParserTestDataFormat))]
    public void ParserTests(int testNum, string[] args, (string RspFileName, string RspFileContent)[]? rspFiles, CommandLineParseResultWrapper parseResultWrapper)
    {
        try
        {
            if (rspFiles is not null)
            {
                foreach ((string rspFileName, string rspFileContent) in rspFiles)
                {
                    File.WriteAllText(rspFileName, rspFileContent);
                }
            }

            CommandLineParseResult result = CommandLineParser.Parse(args, new SystemEnvironment());
            Assert.AreEqual(parseResultWrapper.Result, result, $"Test num '{testNum}' failed");
        }
        finally
        {
            if (rspFiles is not null)
            {
                foreach ((string rspFileName, _) in rspFiles)
                {
                    File.Delete(rspFileName);
                }
            }
        }
    }

    public static string ParserTestDataFormat(MethodInfo methodInfo, object?[]? data)
    {
        (int testNum, string[] args, (string RspFileName, string RspFileContent)[]? rspFiles, CommandLineParseResultWrapper parseResult) = ((int)data![0]!, (string[])data[1]!, ((string, string)[])data[2]!, (CommandLineParseResultWrapper)data[3]!);

        return testNum == 13
            ? $"\"--option1\", $@\" \"\" \\{{Environment.NewLine}} \"\" \" {testNum}"
            : $"{args.Aggregate((a, b) => $"{a} {b}")} {testNum}";
    }

    internal static IEnumerable<(int TestNum, string[] Args, (string RspFileName, string RspFileContent)[]? RspFiles, CommandLineParseResultWrapper ParseResult)> ParserTestsData()
    {
        yield return (1, ["--option1", "a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a"]) }.ToArray(), []));
        yield return (2, ["--option1", "a", "b"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a", "b"]) }.ToArray(), []));
        yield return (3, ["-option1", "a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a"]) }.ToArray(), []));
        yield return (4, ["--option1", "a", "-option2", "c"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["c"]),
        }.ToArray(), []));
        yield return (5, ["---option1", "a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>().ToArray(), ["Unexpected argument ---option1", "Unexpected argument a"]));
        yield return (6, ["--option1", "'a'"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a"]) }.ToArray(), []));
        yield return (7, ["--option1", "'a'", "--option2", "'hello'"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["hello"]),
        }.ToArray(), []));
        yield return (8, ["--option1", "'a'b'"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", []) }.ToArray(), ["Unexpected single quote in argument: 'a'b' for option '--option1'"]));
        yield return (9, ["option1", "--option1"], null, new CommandLineParseResultWrapper("option1", new List<CommandLineParseOption> { new("option1", []) }.ToArray(), []));
        yield return (10, ["--option1", @"""\\"""], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["\\"]) }.ToArray(), []));
        yield return (11, ["--option1", @" "" \"" "" "], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", [" \" "]) }.ToArray(), []));
        yield return (12, ["--option1", @" "" \$ "" "], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", [" $ "]) }.ToArray(), []));
        yield return (13, ["--option1", $@" "" \{Environment.NewLine} "" "], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", [$" {Environment.NewLine} "]) }.ToArray(), []));
        yield return (14, ["--option1", "a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a"]) }.ToArray(), []));
        yield return (15, ["--option1:a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a"]) }.ToArray(), []));
        yield return (16, ["--option1=a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a"]) }.ToArray(), []));
        yield return (17, ["--option1=a", "--option1=b"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a"]), new("option1", ["b"]) }.ToArray(), []));
        yield return (18, ["--option1=a", "--option1", "b"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a"]), new("option1", ["b"]) }.ToArray(), []));
        yield return (19, ["--option1=a=a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a=a"]) }.ToArray(), []));
        yield return (20, ["--option1=a:a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a:a"]) }.ToArray(), []));
        yield return (21, ["--option1:a=a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a=a"]) }.ToArray(), []));
        yield return (22, ["--option1:a:a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a:a"]) }.ToArray(), []));
        yield return (23, ["--option1:a:a", "--option1:a=a"], null, new CommandLineParseResultWrapper(null, new List<CommandLineParseOption> { new("option1", ["a:a"]), new("option1", ["a=a"]) }.ToArray(), []));
        yield return (24, ["--option1", "a", "@test.rsp", "--option5", "e"], [("test.rsp",
            """
            --option2 b
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
            new("option5", ["e"]),
        }.ToArray(), []));
        yield return (25, ["--option1", "a", "@25_test1.rsp", "--option6", "f"], [
            ("25_test1.rsp",
            """
            --option2 b
            @25_test2.rsp
            --option5 e
            """),
            ("25_test2.rsp",
            """
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
            new("option5", ["e"]),
            new("option6", ["f"]),
        }.ToArray(), []));
        yield return (26, ["@26_test.rsp", "--option3", "c", "--option4", "d"], [
            ("26_test.rsp",
            """
            --option1 a
            --option2 b
            """)], new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
        yield return (27, ["--option1", "a", "--option2", "b", "@27_test.rsp"], [
            ("27_test.rsp",
            """
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
        yield return (28, ["@28_test.rsp"], [
            ("28_test.rsp",
            """
            --option1 a
            --option2 b
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
        yield return (29, ["@29_test1.rsp", "@29_test2.rsp"], [
            ("29_test1.rsp",
            """
            --option1 a
            --option2 b
            """),
            ("29_test2.rsp",
            """
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
        yield return (30, ["@30_test1.rsp"], [
            ("30_test1.rsp",
            """
            --option1 a
            --option2 b
            @30_test2.rsp
            """),
            ("30_test2.rsp",
            """
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<CommandLineParseOption>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
    }

    [TestMethod]
    public void CommandLineOptionWithNumber_IsSupported()
    {
        _ = new CommandLineOption("123", "sample", ArgumentArity.ZeroOrOne, false);
        _ = new CommandLineOption("1aa1", "sample", ArgumentArity.ZeroOrOne, false);
    }
}
