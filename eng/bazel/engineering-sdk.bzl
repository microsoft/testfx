"""Opt-in compiler SDK; target packs remain owned by the published dotnet extension.

Archives and SHA-512 values are from Microsoft's version-specific public and
public-checksums containers. The SDK's bundled metadata and executed compiler
versions are recorded in docs/decisions/bazel-build.md. This is deliberately not
release metadata: engineering SDKs do not have a published release date.
"""

load("@rules_dotnet//dotnet:repositories.bzl", "dotnet_sdk_repositories", "dotnet_toolchains_repo")
load("@rules_dotnet//dotnet/private/sdk:releases.bzl", "hex_to_base64")

_SDK_VERSION = "12.0.100-alpha.1.26556.115"
_RUNTIME_VERSION = "12.0.0-alpha.1.26556.115"

# The platform keys are the pinned rules' execution platforms, not target RIDs.
_ARCHIVES = {
    "x86_64-pc-windows-msvc": (
        "win-x64.zip",
        "AC4F315BCDD32ABDD5A9A40656E8F13F54244BFE2CDE691714FDFD1B4DE06FABAB0909A547FCCB986BBB213C45CFEE55964AD045E90027F8F5A732974651C27E",
    ),
    "arm64-pc-windows-msvc": (
        "win-arm64.zip",
        "9E511E5B19438BD1E8AE0494F576F727F6EF6EDFBE47B639C5EABD89114473D51C86F017360B7AFABFB3C2BF3831212B7C5823770F1539079401E24CBD261CD2",
    ),
    "x86_64-unknown-linux-gnu": (
        "linux-x64.tar.gz",
        "46B3976E1792EF68B05E59B727F116527FADEA29822E0334E45E79007ADA183EB2678211661013EA80E17603EB3F3D392EFE15D5F1B1CFF817AE1149E611497E",
    ),
    "arm64-unknown-linux-gnu": (
        "linux-arm64.tar.gz",
        "938C5CE53042E5E7AA1E9BDF2AAA2491D8BFA24D19BD1B7BD75F16C04914255D6C0BBBCC48BEA711759C8CE9519BD2221835920823CFE96FC4FBC4C1BB68F9E3",
    ),
    "x86_64-apple-darwin": (
        "osx-x64.tar.gz",
        "F662633856F91AC18683E527DC7570B52DB480A14630183336F3D6B5D5A396887FBF42193B6E30D3156834D5E6E0980EA5FA742A090AA0EA8191282E79CCE3D1",
    ),
    "aarch64-apple-darwin": (
        "osx-arm64.tar.gz",
        "06DC432250212225D8B30E56103EB6880C701D41C570D1BB4BB24CC050612FF4236AA9378A6144559D136A64D990B37D8F1B5C0E0FE47254443F89916A4BF6F3",
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
