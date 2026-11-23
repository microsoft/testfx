"""Opt-in compiler SDK; target packs remain owned by the published dotnet extension.

Archives and SHA-512 values are from Microsoft's version-specific public and
public-checksums containers. The SDK's bundled metadata and executed compiler
versions are recorded in docs/decisions/bazel-build.md. This is deliberately not
release metadata: engineering SDKs do not have a published release date.
"""

load("@rules_dotnet//dotnet:repositories.bzl", "dotnet_sdk_repositories", "dotnet_toolchains_repo")
load("@rules_dotnet//dotnet/private/sdk:releases.bzl", "hex_to_base64")

_SDK_VERSION = "12.0.100-alpha.1.26570.125"
_RUNTIME_VERSION = "12.0.0-alpha.1.26570.125"

# The platform keys are the pinned rules' execution platforms, not target RIDs.
_ARCHIVES = {
    "x86_64-pc-windows-msvc": (
        "win-x64.zip",
        "7B4E41A5B85C68366EF0D18A810A364C8D98D43A27F592AB780D2C80644EA4C9EFCBC8A6958927BBBCB50C1E7594A049943CD0B69ECC056E7E96E3AF9EA2D269",
    ),
    "arm64-pc-windows-msvc": (
        "win-arm64.zip",
        "02F82A65B205BFBDC1DB675A266DF1277956555DF8DDC0FFAAF2B7A0BCE7C0DC4E43DDB2887BEEA4FACC2B0944A538866F0E3C7E2B4B5200D1EA866E0DB15B63",
    ),
    "x86_64-unknown-linux-gnu": (
        "linux-x64.tar.gz",
        "B6DE68183AF58398F1CF8DC84669C3DCFBA900801A6DE7B2C358FAEB205891803BF93BCA63741EAF7BCF6072A2C404E0399DBDB4FC2D27D6F0F9645479A8E649",
    ),
    "arm64-unknown-linux-gnu": (
        "linux-arm64.tar.gz",
        "98C7B586C226AA4FCB9709669E93B63E798BE4CD72E3D48FC9647115BBC14ADCA1D89639BEF808DC21AB7A285D86FB68A65FB08753097CA2106AE2A6B29A96D9",
    ),
    "x86_64-apple-darwin": (
        "osx-x64.tar.gz",
        "719189B7DF192852870F70837AF921E739065A91973CB3605C705925135EEF63472E834CBEDDD91813E988B35D7AA35B3C4432F7AEE11DA538E0279D01460E6A",
    ),
    "aarch64-apple-darwin": (
        "osx-arm64.tar.gz",
        "6FBEC4643A6C02CB2893CB6E46C9444606608E19A482D5866448C277BBD95DDDD9D60D8D1ADCBFB0D8A831A19A7A4E50489715DFB9B215D7D6FA08172012559A",
    ),
}

def _engineering_sdk_impl(module_ctx):
    global_json = json.decode(module_ctx.read(Label("//:global.json")))
    if global_json["sdk"]["version"] != _SDK_VERSION or global_json["tools"]["dotnet"] != _SDK_VERSION:
        fail("The engineering Bazel SDK must match both global.json pins. Re-verify archives, checksums, bundled metadata, and native consumers before updating eng/bazel/engineering-sdk.bzl.")

    sdk = struct(
        version = _SDK_VERSION,
        runtime_version = _RUNTIME_VERSION,
        tfm = "net12.0",
        csharp_version = "15.0",
        fsharp_version = "11.2",
        archives = {
            platform: {
                "url": "https://ci.dot.net/public/Sdk/{}/dotnet-sdk-{}-{}".format(_SDK_VERSION, _SDK_VERSION, archive),
                "integrity": "sha512-" + hex_to_base64(sha512),
            }
            for platform, (archive, sha512) in _ARCHIVES.items()
        },
    )
    dotnet_sdk_repositories(sdk)
    dotnet_toolchains_repo(
        name = "dotnet_engineering_toolchains",
        dotnet_version = _SDK_VERSION,
    )

    return module_ctx.extension_metadata(reproducible = True)

engineering_sdk = module_extension(implementation = _engineering_sdk_impl)
