---
name: post-release-activities
description: Systematic activities that need to be performed after a release is done.
---

# Post-release Activities Skill

## When to Use This Skill

Use this skill when assigned an issue asking you to do the post-release activities. You will be given the branch name for the release. If that information is missing, ask for it and don't proceed with doing anything.

## Steps to Follow

### Step 1: Find the release version

The branch name only contains the major and minor parts of the version, but not the patch part. To find the full version:

1. Look for `eng/Versions.props` file in the given branch.
2. Find the value of `VersionPrefix`.

### Step 2: Tagging the branch

Create a tag for the branch using the version you found in Step 1. The tag name should be `v{version}` (replace `{version}` with the version you found in Step 1). After creating the tag, push it to the remote repository.

### Step 3: Create a GitHub draft release

1. Create a file `release-notes.md` and add the following content to it: `See the release notes [here](https://github.com/microsoft/testfx/blob/main/docs/Changelog.md#{version})` (replace `{version}` with the version you found in Step 1).
2. Run `gh release create v{version} --draft --title v{version} --notes-file release-notes.md`.

### Step 4: Open a PR to main updating the change log files

1. Run the following command to get markdown of the changelog: `gh api --method POST -H "Accept: application/vnd.github+json" -H "X-GitHub-Api-Version: 2022-11-28" /repos/microsoft/testfx/releases/generate-notes -f 'tag_name=v{version}'` (replace `{version}` with the version you found in Step 1).
2. Classify each entry as either an MSTest change or a Microsoft.Testing.Platform change.
3. MSTest changes go to `docs/Changelog.md` and Microsoft.Testing.Platform changes go to `docs/Changelog-Platform.md`.
4. Classify further the changes of each product into categories like "Added", "Changed", "Fixed", etc.
5. Update the changelog markdown files using the above instructions and following the existing format of those files.
6. When not confident about the classification of a specific change, indicate in the PR description that it requires attention and manual review. This PR must target the `main` branch, not the release branch.

### Step 5: Create a PR to release branch to update patch version

Open a PR to the release branch to update the patch version in `eng/Versions.props` file. The new patch version should be one more than the patch version you found in Step 1.

Note that in this step, both `TestingPlatformVersionPrefix` and `VersionPrefix` must be updated.

### Step 6: Create a PR to main to update minor version

Open a PR to the main branch to update the minor version in `eng/Versions.props` file. The new minor version should be one more than the minor version you found in Step 1, and the patch version should be reset to 0.

Note that in this step, both `TestingPlatformVersionPrefix` and `VersionPrefix` must be updated.

### Step 7: Update public samples

Open a PR to the main branch to update the product versions of public samples to latest. Public samples are present in `samples/public` directory.

### Step 8: Mark APIs and analyzers as shipped

1. Run `eng/mark-shipped.ps1` (requires PowerShell 7.0).
2. Move the contents of `src/Analyzers/MSTest.Analyzers/AnalyzerReleases.Unshipped.md` (except the first two lines that start with `;`) to `src/Analyzers/MSTest.Analyzers/AnalyzerReleases.Shipped.md`.

Create a PR to main with these changes.
