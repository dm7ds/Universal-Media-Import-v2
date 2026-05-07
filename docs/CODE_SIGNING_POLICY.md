# UMI — Code Signing Policy

This document describes who is authorised to release signed UMI binaries, how
release artifacts are produced, and how user data is handled in connection with
those releases. It is published as a precondition for free OSS code signing
provided by [SignPath Foundation](https://signpath.org/).

## Project

- **Name:** Universal Media Import (UMI)
- **Repository:** <https://github.com/dm7ds/Universal-Media-Import-v2>
- **License:** [GPL-3.0-or-later](LICENSE) — OSI-approved, no commercial dual-licensing
- **Maintainer:** Dirk Schelhasse — <dirk.schelhasse@gmail.com>

UMI is a single-maintainer Windows desktop project. Its public release flow is
fully reproducible from source via the GitHub Actions workflow at
[`.github/workflows/release.yml`](.github/workflows/release.yml).

## Team and Roles

For the purposes of SignPath Foundation's required role separation:

| Role | Member(s) |
|---|---|
| **Authors** — trusted to modify source code without additional review | Dirk Schelhasse |
| **Reviewers** — review changes from non-committers (PRs from outside the project) | Dirk Schelhasse |
| **Approvers** — decide which tagged commits get a code signature | Dirk Schelhasse |

GitHub multi-factor authentication is enforced on the maintainer's account.
Should the project ever gain additional contributors, this section is updated
before any new role assignment takes effect.

## How Releases Are Built

Signed Windows binaries (`UMI_Setup_*.exe`, `umi-slim.zip`, `umi-portable.zip`)
are produced exclusively by the workflow at
[`.github/workflows/release.yml`](.github/workflows/release.yml). The workflow:

1. Triggers only on tags matching `v*` pushed to `dm7ds/Universal-Media-Import-v2`
2. Runs on GitHub-hosted `windows-latest` runners — no self-hosted infrastructure
3. Builds from source at exactly the tagged revision
4. Uploads the unsigned installer as a GitHub Actions artifact
5. Submits the artifact to SignPath via the official
   [`signpath/github-action-submit-signing-request`](https://github.com/signpath/github-action-submit-signing-request)
   action
6. Attaches the signed installer to a GitHub Release with the same name as the tag

A development repository (`dm7ds/Universal-Media-Import-v2-dev`, private) holds
the day-to-day commit history. The mirror script
[`scripts/publish-to-public.ps1`](scripts/publish-to-public.ps1) produces the
public-repo snapshot the workflow then builds — it filters internal task cards,
framework notes and side projects through an explicit allowlist before pushing.
Only the maintainer holds the credentials required to push to either repository.

## Privacy and Data Transfer

UMI is a local Windows desktop tool. None of its features collect, transmit, or
persist user data on remote servers maintained by the project.

The signed binary contains exactly one optional network feature:

- **Update check.** At application startup, only when the user has enabled
  *Check for updates on startup* in Settings (the default since `v2.1.0`),
  UMI calls
  `https://api.github.com/repos/dm7ds/Universal-Media-Import-v2/releases/latest`
  to compare the installed version with the latest published release. The HTTP
  request carries a `User-Agent` string identifying UMI and its version. No
  personal data is sent. If a newer release is available, the installer is
  downloaded from the corresponding GitHub Release asset URL into the user's
  `%TEMP%` directory and offered for installation via a banner inside the GUI.

The maintainer does not collect telemetry, crash reports, or usage statistics.

## Attribution

Free code signing provided by [SignPath.io](https://signpath.io/), certificate
by [SignPath Foundation](https://signpath.org/).
