This is a .NET based repository that contains the MSTest testing framework and Microsoft.Testing.Platform (aka MTP) testing platform. Please follow these guidelines when contributing:

## Code Standards

You MUST follow all code-formatting and naming conventions defined in [`.editorconfig`](./.editorconfig).

In addition to the rules enforced by `.editorconfig`, you SHOULD:

- Prefer file-scoped namespace declarations and single-line using directives.
- Ensure that the final return statement of a method is on its own line.
- Use pattern matching and switch expressions wherever possible.
- Use `nameof` instead of string literals when referring to member names.
- Always use `is null` or `is not null` instead of `== null` or `!= null`.
- Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.
- Prefer `?.` if applicable (e.g. `scope?.Dispose()`).
- Use `ObjectDisposedException.ThrowIf` where applicable.
- Respect StyleCop.Analyzers rules.

You MUST minimize adding public API surface area but any newly added public API MUST be declared in the related `PublicAPI.Unshipped.txt` file.

## Localization Guidelines

Anytime you add a new localization resource, you MUST:
- Add a corresponding entry in the localization resource file.
- Add an entry in all `*.xlf` files related to the modified `.resx` file.

## Testing Guidelines

- Tests for MTP and MSTest analyzers MUST use MSTest.
- Unit tests for MSTest MUST use the internal test framework defined in [`TestFramework.ForTestingMSTest`](./test/Utilities/TestFramework.ForTestingMSTest).
- All assertions must be written using FluentAssertions style of assertion.
