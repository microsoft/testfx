﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public sealed class CommandLineTests : TestBase
{
    // The test method ParserTests is parameterized and one of the parameter needs to be CommandLineParseResult.
    // The test method has to be public to be run, but CommandLineParseResult is internal.
    // So, we introduce this wrapper to be used instead so that the test method can be made public.
    public class CommandLineParseResultWrapper
    {
        internal CommandLineParseResultWrapper(string? toolName, IReadOnlyList<OptionRecord> options, IReadOnlyList<string> errors)
            => Result = new CommandLineParseResult(toolName, options, errors);

        internal CommandLineParseResult Result { get; }
    }

    public CommandLineTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [ArgumentsProvider(nameof(ParserTestsData), TestArgumentsEntryProviderMethodName = nameof(ParserTestDataFormat))]
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

    internal static TestArgumentsEntry<(int TestNum, string[] Args, (string RspFileName, string RspFileContent)[]? RspFiles, CommandLineParseResultWrapper ParseResult)> ParserTestDataFormat(TestArgumentsContext ctx)
    {
        (int TestNum, string[] Args, (string RspFileName, string RspFileContent)[]? RspFiles, CommandLineParseResultWrapper ParseResult) item = ((int, string[], (string, string)[], CommandLineParseResultWrapper))ctx.Arguments;

        return item.TestNum == 13
            ? new(item, $"\"--option1\", $@\" \"\" \\{{Environment.NewLine}} \"\" \" {item.TestNum}")
            : new(item, $"{item.Args.Aggregate((a, b) => $"{a} {b}")} {item.TestNum}");
    }

    internal static IEnumerable<(int TestNum, string[] Args, (string RspFileName, string RspFileContent)[]? RspFiles, CommandLineParseResultWrapper ParseResult)> ParserTestsData()
    {
        yield return (1, ["--option1", "a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a"]) }.ToArray(), []));
        yield return (2, ["--option1", "a", "b"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a", "b"]) }.ToArray(), []));
        yield return (3, ["-option1", "a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a"]) }.ToArray(), []));
        yield return (4, ["--option1", "a", "-option2", "c"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["c"]),
        }.ToArray(), []));
        yield return (5, ["---option1", "a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord>().ToArray(), ["Unexpected argument ---option1", "Unexpected argument a"]));
        yield return (6, ["--option1", "'a'"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a"]) }.ToArray(), []));
        yield return (7, ["--option1", "'a'", "--option2", "'hello'"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["hello"]),
        }.ToArray(), []));
        yield return (8, ["--option1", "'a'b'"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", []) }.ToArray(), ["Unexpected single quote in argument: 'a'b' for option '--option1'"]));
        yield return (9, ["option1", "--option1"], null, new CommandLineParseResultWrapper("option1", new List<OptionRecord> { new("option1", []) }.ToArray(), []));
        yield return (10, ["--option1", @"""\\"""], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["\\"]) }.ToArray(), []));
        yield return (11, ["--option1", @" "" \"" "" "], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", [" \" "]) }.ToArray(), []));
        yield return (12, ["--option1", @" "" \$ "" "], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", [" $ "]) }.ToArray(), []));
        yield return (13, ["--option1", $@" "" \{Environment.NewLine} "" "], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", [$" {Environment.NewLine} "]) }.ToArray(), []));
        yield return (14, ["--option1", "a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a"]) }.ToArray(), []));
        yield return (15, ["--option1:a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a"]) }.ToArray(), []));
        yield return (16, ["--option1=a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a"]) }.ToArray(), []));
        yield return (17, ["--option1=a", "--option1=b"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a"]), new("option1", ["b"]) }.ToArray(), []));
        yield return (18, ["--option1=a", "--option1 b"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a"]), new("option1", ["b"]) }.ToArray(), []));
        yield return (19, ["--option1=a=a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a=a"]) }.ToArray(), []));
        yield return (20, ["--option1=a:a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a:a"]) }.ToArray(), []));
        yield return (21, ["--option1:a=a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a=a"]) }.ToArray(), []));
        yield return (22, ["--option1:a:a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a:a"]) }.ToArray(), []));
        yield return (23, ["--option1:a:a", "--option1:a=a"], null, new CommandLineParseResultWrapper(null, new List<OptionRecord> { new("option1", ["a:a"]), new("option1", ["a=a"]) }.ToArray(), []));
        yield return (24, ["--option1", "a", "@test.rsp", "--option5", "e"], [("test.rsp",
            """
            --option2 b
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
            new("option5", ["e"]),
        }.ToArray(), []));
        yield return (25, ["--option1", "a", "@test1.rsp", "--option6", "f"], [
            ("test1.rsp",
            """
            --option2 b
            @test2.rsp
            --option5 e
            """),
            ("test2.rsp",
            """
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
            new("option5", ["e"]),
            new("option6", ["f"]),
        }.ToArray(), []));
        yield return (26, ["@test.rsp", "--option3", "c", "--option4", "d"], [
            ("test.rsp",
            """
            --option1 a
            --option2 b
            """)], new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
        yield return (27, ["--option1", "a", "--option2", "b", "@test.rsp"], [
            ("test.rsp",
            """
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
        yield return (28, ["@test.rsp"], [
            ("test.rsp",
            """
            --option1 a
            --option2 b
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
        yield return (29, ["@test1.rsp", "@test2.rsp"], [
            ("test1.rsp",
            """
            --option1 a
            --option2 b
            """),
            ("test2.rsp",
            """
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
        yield return (30, ["@test1.rsp"], [
            ("test1.rsp",
            """
            --option1 a
            --option2 b
            @test2.rsp
            """),
            ("test2.rsp",
            """
            --option3 c
            --option4 d
            """)], new CommandLineParseResultWrapper(null, new List<OptionRecord>
        {
            new("option1", ["a"]),
            new("option2", ["b"]),
            new("option3", ["c"]),
            new("option4", ["d"]),
        }.ToArray(), []));
    }

    public void CommandLineOptionWithNumber_IsSupported()
    {
        _ = new CommandLineOption("123", "sample", ArgumentArity.ZeroOrOne, false);
        _ = new CommandLineOption("1aa1", "sample", ArgumentArity.ZeroOrOne, false);
    }
}
