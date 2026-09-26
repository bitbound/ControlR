# GitHub Actions Workflows for ControlR

This repository uses GitHub Actions to build, test, and deploy ControlR.

## Build Metadata

`build.yml` is the only workflow that knows what a build actually produced, so it writes that
down as `build_metadata.json` and uploads it as the `BuildMetadata` artifact. Every publishing
workflow reads the metadata instead of re-deriving version and prerelease state from the
payload, which is what keeps a build from being published under the wrong version or channel.

```json
{
  "version": "1.2.3.0",
  "prerelease": false,
  "serverRuntime": "linux-multiarch",
  "serverRids": ["linux-x64", "linux-arm64"]
}
```

| Property        | Meaning                                                                    |
| --------------- | -------------------------------------------------------------------------- |
| `version`       | Numeric build version, without any prerelease suffix                       |
| `prerelease`    | `true` when the build came from `build.yml` with the prerelease input       |
| `serverRuntime` | The `server_runtime` input the build used (`linux-multiarch`, `win-x64`, ...) |
| `serverRids`    | Runtime identifiers the build published, in build order                    |

Consumers use the `.github/actions/get-build-metadata` composite action, which downloads the
artifact and exposes `version`, `prerelease`, `release_version` (`<version>-dev` for prerelease
builds), `server_rids`, and `primary_rid`. Pass `expected-rid` when a workflow only makes sense
for one runtime; the action then fails if the build did not produce it.

To carry a new property from the build to its consumers:

1. Add it to the `$Metadata` hashtable in the `Write Build Metadata` step of `build.yml`.
2. Add a matching output in `.github/actions/get-build-metadata/action.yml`.
3. Read it in the publishing workflow that needs it.

`Version.txt` is separate and stays numeric: it is served by the running server at
`/downloads/Version.txt`, so it is a runtime artifact rather than build metadata.

## Available Workflows

### build.yml - Build

The producer. Builds the server payloads for the requested runtimes, packs the NuGet
packages, and writes the `BuildMetadata` artifact. Every publishing workflow consumes the
output of one of these runs.

| Input                | Purpose                                                        |
| -------------------- | -------------------------------------------------------------- |
| `version`            | Version to build; defaults to the version resolved by `set-version` |
| `ref`                | Git ref to build from (reusable calls only)                    |
| `server_runtime`     | Runtime to build: `linux-multiarch`, `linux-x64`, `linux-arm64`, `win-x64` |
| `prerelease`         | Marks the build as a prerelease, which suffixes the release version with `-dev` |
| `build_nugets`       | Also pack and sign the NuGet packages                          |
| `run_tests`          | Run the test workflows first                                   |
| `use_local_storage`  | Store artifacts on the SCP host instead of on GitHub           |
| `use_self_hosted_runners` | Run on self-hosted runners instead of GitHub-hosted ones  |

### publish-*.yml - Publishing

Each publishing workflow takes a run ID (empty means "most recent successful build") and
reads its version and prerelease state from the build metadata.

| Workflow             | Publishes                                                       |
| -------------------- | --------------------------------------------------------------- |
| `publish-github.yml` | A draft GitHub Release, tagged `v<version>` (`v<version>-dev` for prerelease builds) |
| `publish-docker.yml` | `bitbound/controlr` on Docker Hub, tagged with the derived channel and the version |
| `publish-nugets.yml` | The NuGet packages to NuGet.org                                  |
| `publish-zip.yml`    | A slot server ZIP to the sponsor's Azure blob container          |
| `publish-acr.yml`    | A slot container image to Azure Container Registry               |

These are also called by the two orchestrators:

- `publish-all.yml` (Production Release) - dispatch only. Runs `publish-docker`, then
  `publish-github` and `publish-nugets` for the same build run.
- `acr-build-and-publish.yml` (ACR Build and Publish) - dispatch only, driven by the
  Subscriber Portal. Validates the requested delivery format and runtime, calls `build.yml`,
  and then runs either `publish-acr` (container delivery) or `publish-zip` (ZIP delivery).

### Test and automation workflows

| Workflow                  | Purpose                                                       |
| ------------------------- | ------------------------------------------------------------- |
| `run-tests.yml`           | Called by `build.yml` when `run_tests` is set                 |
| `build-sign-pack-nugets.yml` | Called by `build.yml` when `build_nugets` is set           |
| `test.yml` (Tests)        | Unit tests on pull requests to `main`/`dev` and pushes to `main` |
| `ui-tests.yml` (UI Tests) | Playwright UI tests, dispatch only                             |
| `bitbound-bot.yml`        | Bot replies to review comments and pull requests               |
| `auto-close-external-prs.yml` | Closes pull requests from external forks                   |

## Required Secrets

For these workflows to function properly, you need to set up the following repository secrets:

- `DOCKER_USERNAME`: Your Docker Hub username
- `DOCKER_PAT`: Your Docker Hub access token
- `CERTIFICATE_THUMBPRINT`: Thumbprint of the code signing certificate (optional)
- `SIGNTOOL_BINARY`: Base64-encoded SignTool executable (optional)

## GitHub Release Assets

When creating a GitHub Release, the following assets are included:

- `server-linux-amd64.zip`, `server-linux-arm64.zip`, `server-win-x64.zip`: server payloads, one
  per runtime in the build metadata that has a release archive
- `ControlR.Web.Server_internal.json`, `ControlR.Web.Server_v1.json`: API schemas
- `docker-compose.yml`: Docker Compose file

Releases are created as drafts. The release name and tag are `v<release_version>`, which carries
a `-dev` suffix for prerelease builds, and such builds are also marked as a prerelease on GitHub.

## Docker Images

Docker images are published to Docker Hub:

- `bitbound/controlr:dev` - Prerelease builds, whose version tag carries a `-dev` suffix
- `bitbound/controlr:preview` - Preview channel (stable builds)
- `bitbound/controlr:latest` - Production version
- `bitbound/controlr:[version]` - Specific version (`[version]-dev` for prerelease builds)
- `bitbound/controlr-relay:preview` - Preview relay server
- `bitbound/controlr-relay:latest` - Production relay server
- `bitbound/controlr-relay:[version]` - Specific version of relay server
