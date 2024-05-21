# MSTest SDK

## Design notes

Do not use the `IsImplictlyDefined="true"` attribute in the `PackageReference` element in the `.targets` files. Instead,
rely on the `VersionOverride` attribute to define the package version. This is because for big projects, teams are usually
slowly migrating to MSTest.Sdk so they need to keep defining MSTest (and platform) packages in their CPM file.

If we use 'IsImplicitlyDefined' attribute, the package will be "defined twice" which will lead to `NU1009` warnings that
are most of the time updated as errors (warnAsError). We created a thread with MSBuild and NuGet teams and the only suggested
solution is for users to suppress the warning which is not ideal. Until a better solution is provided, we will use the
`VersionOverride` trick instead as it achieves a relatively close behavior while preventing the warning.

We could also consider having a property like `MSTestSdkDisableIsImplicitlyDefinedAttribute` that users can set to `true` to
disable the `IsImplicitlyDefined` attribute in the `.targets` files but we don't see any strong reason to do that at the moment.
