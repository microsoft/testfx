"""Conformance fixtures for consumer-local native C# reference aliases."""

load("@rules_dotnet//dotnet:defs.bzl", "csharp_library")
load(":defs.bzl", "TESTFX_PUBLIC_KEY_FILE", "testfx_binary", "testfx_friend_identities", "testfx_test")

_PROJECT = "test/UnitTests/Bazel.ReferenceAliases.UnitTests"

def reference_alias_tests():
    for name in ["First", "Second"]:
        csharp_library(
            name = "alias_" + name.lower(),
            srcs = [_PROJECT + "/" + name + ".cs"],
            out = "Bazel.Aliases." + name,
            target_frameworks = ["net10.0"],
            internals_visible_to = testfx_friend_identities(["Bazel.ReferenceAliases.UnitTests"]),
            keyfile = TESTFX_PUBLIC_KEY_FILE,
            compiler_options = ["/publicsign+"],
            generate_documentation_file = False,
        )
    testfx_binary(
        name = "reference_alias_tests_app",
        project = _PROJECT,
        exclude = [_PROJECT + "/" + name + ".cs" for name in ["First", "Second", "InvalidConfiguration"]],
        linked_srcs = ["//eng/bazel:TestMain.cs"],
        defines = ["TESTFX_METHOD_PARALLEL"],
        target_frameworks = ["net10.0"],
        reference_aliases = {
            "Bazel.Aliases.First": ["global", "FirstAlias"],
            "Bazel.Aliases.Second": ["SecondAlias"],
        },
        # Exercise the extra reference-only compiler passes with the same aliases.
        internals_visible_to = ["Bazel.ReferenceAliases.Consumer"],
        generate_documentation_file = False,
        deps = [
            ":alias_first",
            ":alias_second",
            ":adapter",
            ":framework",
            ":framework_extensions",
            ":trx",
        ],
    )
    testfx_test(
        name = "reference_alias_tests",
        binary = ":reference_alias_tests_app",
        args = ["--minimum-expected-tests", "9"],
        size = "small",
    )
    for name, aliases in [
        ("unknown", {"Missing.Assembly": ["Ghost"]}),
        ("empty", {"Bazel.Aliases.First": []}),
        ("duplicate", {"Bazel.Aliases.First": ["Same", "Same"]}),
    ]:
        csharp_library(
            name = "reference_alias_" + name,
            srcs = [_PROJECT + "/InvalidConfiguration.cs"],
            target_frameworks = ["net10.0"],
            reference_aliases = aliases,
            generate_documentation_file = False,
            deps = [":alias_first"],
            tags = ["manual"],
        )
