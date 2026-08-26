# Package README guidelines

Every packable project must include a `PACKAGE.md` next to its project file. The repository packs this file as the NuGet package README, and nuget.org renders it on the package page.

Use this checklist when adding or updating a package:

- Start with an H1 containing the exact package ID and a short package-specific description.
- Show the installation command unless the package is not directly consumable.
- Include a compact `Getting started` or `Usage` section. Prefer a runnable API example, the CLI option that enables the extension, or the MSBuild configuration that activates the package. For abstraction-only packages, explain who should reference the package and identify its main extension point.
- Link to the package's dedicated documentation. Also link to the product overview when it provides useful additional context.
- State supported platforms or important runtime limitations when they differ from the product defaults.
- Provide a feedback or contributing link for packages whose README uses the standard product template.

Keep examples short and verify every API name, command-line option, environment variable, and documentation URL against the current implementation. Use absolute URLs because NuGet renders the file outside this repository.

The project must also define `PackageDescription`. Lead with what the package does and close with `$(CommonProductDescription)`. Wrap multiline values in CDATA without indenting continuation lines so project-file whitespace does not leak into the published description:

```xml
<PackageDescription><![CDATA[Explains what this package does. $(CommonProductDescription)]]></PackageDescription>
```

`Directory.Build.targets` automatically sets `PackageReadmeFile` to `PACKAGE.md`, packs it at the package root, and rejects packable projects that omit either the README or an authored package description.
