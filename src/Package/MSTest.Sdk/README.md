# MSTest SDK

## Design notes

Do not use the `IsImplicitlyDefined="true"` attribute in the `PackageReference` element in the `.targets` files. If we use
the `IsImplicitlyDefined` attribute, the package will be defined twice, which can lead to `NU1009` warnings that are most
of the time treated as errors (`warnAsError`).

Do not use the `VersionOverride` attribute on `PackageReference` items either. Although it provides a way to set a package
version even when Central Package Management (CPM) is enabled, it is forbidden when
`CentralPackageVersionOverrideEnabled` is set to `false` (which causes a `NU1013` build error).

Instead, the `.targets` files should split the version specification depending on whether CPM is enabled:

- When `ManagePackageVersionsCentrally` is not `true`, set the `Version` metadata directly on the `PackageReference`
  (typically as a conditional nested `<Version>` element).
- When `ManagePackageVersionsCentrally` is `true`, leave the `PackageReference` without any version metadata and add a
  matching `PackageVersion` item carrying the `Version` metadata so CPM resolves the version.

This approach works regardless of `CentralPackageVersionOverrideEnabled` and does not produce `NU1009` warnings.

## Layering on top of a different base SDK

`MSTest.Sdk` implicitly imports `Microsoft.NET.Sdk` as its base SDK, so `<Project Sdk="MSTest.Sdk">`
is enough for the common case. To combine it with a different base SDK (for example
`Microsoft.NET.Sdk.Web` for an ASP.NET Core integration test project), import both SDKs manually and
list `Microsoft.NET.Sdk.Web` first so it owns the base `Microsoft.NET.Sdk` import:

```xml
<Project>
  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk.Web" />
  <Import Project="Sdk.props" Sdk="MSTest.Sdk" />

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <Import Project="Sdk.targets" Sdk="MSTest.Sdk" />
  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk.Web" />
</Project>
```

Because the version cannot be specified on an `<Import>`'s `Sdk` attribute the same way it can on
`<Project Sdk="MSTest.Sdk/x.y.z">`, pin the `MSTest.Sdk` version through `global.json`:

```json
{
  "msbuild-sdks": {
    "MSTest.Sdk": "x.y.z"
  }
}
```

`Sdk.props`/`Sdk.targets` guard their `Microsoft.NET.Sdk` import behind the
`_MSTestSdkImportsMicrosoftNETSdk` property: `MSTest.Sdk` only imports the base SDK when no other SDK
has already done so (detected via the `UsingMicrosoftNETSdk` property that `Microsoft.NET.Sdk` sets
very early). This keeps the manual/mixed layering scenario free of `MSB4011` duplicate-import
warnings.
