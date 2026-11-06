"""Opt-in compiler SDK; target packs remain owned by the published dotnet extension.

Archives and SHA-512 values are from Microsoft's version-specific public and
public-checksums containers. The SDK's bundled metadata and executed compiler
versions are recorded in docs/decisions/bazel-build.md. This is deliberately not
release metadata: engineering SDKs do not have a published release date.
"""

load("@rules_dotnet//dotnet:repositories.bzl", "dotnet_sdk_repositories", "dotnet_toolchains_repo")
load("@rules_dotnet//dotnet/private/sdk:releases.bzl", "hex_to_base64")

_SDK_VERSION = "12.0.100-alpha.1.26509.101"
_RUNTIME_VERSION = "12.0.0-alpha.1.26509.101"

# The platform keys are the pinned rules' execution platforms, not target RIDs.
_ARCHIVES = {
    "x86_64-pc-windows-msvc": (
        "win-x64.zip",
        "DDAA58A799737D1EF648A8665476E5A8A0E5DCD713D7C3F1A78C1B2F59E9000D9B4381CAF1436B8A490CA58403D23854BC7EC154024832B2E6F5C007A8FF1C51",
    ),
    "arm64-pc-windows-msvc": (
        "win-arm64.zip",
        "B8DF0AEC696FCFFA32B439E526EA03499481F4475BAEBB435B52BE723B0B10A826975C57A481D689C1CE892C0C9285C9FB7D118AD9FB3689A3A09DDEE803C850",
    ),
    "x86_64-unknown-linux-gnu": (
        "linux-x64.tar.gz",
        "9D89DBE587D5BD67A8D81F345969F79B2469A97FC1EE8785D7924E851855050E555DEBCFB455A9C0FBE03664291329B1EBCCED8027498FC7753AA5F0DB6965F8",
    ),
    "arm64-unknown-linux-gnu": (
        "linux-arm64.tar.gz",
        "F281BAAECC3F07737662AB694D73D3A8C35F7D403A7C12A9689A116AE1767E1525A0111296EA215928778FC4FB62C74B2EA45073207901040094837502B2149E",
    ),
    "x86_64-apple-darwin": (
        "osx-x64.tar.gz",
        "B570849536847C7FF4D0EE1C7C7BFC07BB0B774D8BF6B12A804E7203C5E33A228120BFD8CE3BECAA0F84F140C58DB5D7F05790E4B7EF4C2CAC43F0D44E4F36C3",
    ),
    "aarch64-apple-darwin": (
        "osx-arm64.tar.gz",
        "4268DB295F7E087E7CB343EC49C67F55FDA20CD34BF87A54C55C306C513187348912D2B23F001071E419E689CFEF4CF1AFF686037F2ED02F9F47AEE362B81FF2",
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
