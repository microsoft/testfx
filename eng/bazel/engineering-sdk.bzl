"""Opt-in compiler SDK; target packs remain owned by the published dotnet extension.

Archives and SHA-512 values are from Microsoft's version-specific public and
public-checksums containers. The SDK's bundled metadata and executed compiler
versions are recorded in docs/decisions/bazel-build.md. This is deliberately not
release metadata: engineering SDKs do not have a published release date.
"""

load("@rules_dotnet//dotnet:repositories.bzl", "dotnet_sdk_repositories", "dotnet_toolchains_repo")
load("@rules_dotnet//dotnet/private/sdk:releases.bzl", "hex_to_base64")

_SDK_VERSION = "12.0.100-alpha.1.26562.108"
_RUNTIME_VERSION = "12.0.0-alpha.1.26562.108"

# The platform keys are the pinned rules' execution platforms, not target RIDs.
_ARCHIVES = {
    "x86_64-pc-windows-msvc": (
        "win-x64.zip",
        "72C374CF7E2D0FF85588208E5BFC11417B903249F4FB6F2C523E1C50890DFD4C56B8AF23933A70EBB6592022166FCC5F92BBA7325F917DC528A55C93917CF41A",
    ),
    "arm64-pc-windows-msvc": (
        "win-arm64.zip",
        "78916B3685AF9CA016F2B71B65B4E64E07C6AC0579C48AE7D44A35EBDD679AA03ED857E907F2DCB0593C76D1DD8B00C04F507D811EECAAAEF7A725A74E6AABF9",
    ),
    "x86_64-unknown-linux-gnu": (
        "linux-x64.tar.gz",
        "4C757E60299AF0545A895D8CCA2C1AD38762259830D81C80B0A841ACBB607BAF040606F4EFF47051BD4FDF167230C8BDEAF9592256E4D558EECF3FD2EC305955",
    ),
    "arm64-unknown-linux-gnu": (
        "linux-arm64.tar.gz",
        "6BBF6B341C6798D1BAEA2F28D69FFB3E82F4F843034A8C34CC86750A94E637D9CB86CED7B7948DFFE1F71A7A3E6C9FC019CB31A1E11EB522937DEA3B36780749",
    ),
    "x86_64-apple-darwin": (
        "osx-x64.tar.gz",
        "273A5FFA8E412FD894F6272F55EB564C586CC6EA5EFD0A9E646BDB62B4E61045A9FC6E42FBB1256F607BF6BEF9FF7975092C506A8A1835BCE8A2E90A31AC0B57",
    ),
    "aarch64-apple-darwin": (
        "osx-arm64.tar.gz",
        "7A50FD2E8DE123A02B02813FE94592A305585430DE0AB4BB0BFB1DEBE91FB2802CCA4A441854C023609BCD3102D87AAC9A13D9DA9B50EB85FE4508C80584CFC1",
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
