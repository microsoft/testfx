# MSTest SDK

## Design notes

Do not use the `IsImplicitlyDefined="true"` attribute in the `PackageReference` element in the `.targets` files. If we use
'IsImplicitlyDefined' attribute, the package will be "defined twice" which will lead to `NU1009` warnings that are most of
the time updated as errors (warnAsError).

Do not use the `VersionOverride` attribute on `PackageReference` items either. Although it provides a way to set a package
version even when Central Package Management (CPM) is enabled, it is forbidden when the user opts into the stricter
`CentralPackageVersionOverrideEnabled` set to `false` mode (which causes a `NU1013` build error).

Instead, the `.targets` files should split the version specification depending on whether CPM is enabled:

- When `ManagePackageVersionsCentrally` is not `true`, set the `Version` metadata directly on the `PackageReference`
  (typically as a conditional nested `<Version>` element).
- When `ManagePackageVersionsCentrally` is `true`, leave the `PackageReference` without any version metadata and add a
  matching `PackageVersion` item carrying the `Version` metadata so CPM resolves the version.

This approach works regardless of `CentralPackageVersionOverrideEnabled` and does not produce `NU1009` warnings.
