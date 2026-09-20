# Release automation

Release automation is deliberately split between local Source preparation and the
Controller's bounded GitHub operations. It does not make Chrome Web Store review a
condition of GitHub or NuGet availability.

For an accepted, clean checkpoint tagged `vX.Y.Z`, use this sequence:

1. Run `Scripts/Prepare-Release.ps1` locally. It resolves and cross-checks the Core,
   Extension, and SampleCaller product version; runs package gates; and produces the
   exact NuGet, extension, and Windows bundle artifacts.
2. Use the Controller `github-release-publish.bat` operation for `vX.Y.Z`.
3. Use Controller `github-workflow-dispatch.bat` to dispatch `publish-nuget` for the
   same immutable tag.
4. Optionally dispatch `publish-cws` for that tag through the same Controller route.

GitHub Releases remain the exact-version Extension fallback. Chrome Web Store review
can lag or a product version can be intentionally skipped; it is not a complete archive
of every LazyChromeWindowBridge version.

## Chrome Web Store V2 setup

`publish-cws` is restricted to the existing Store item. It uploads the deterministic
matching extension ZIP and submits it for normal Google review; it does not create an
item, change visibility, set a rollout percentage, or wait for review completion.
The current general-public CWS 0.2.0 listing remains that same existing item.

One-time maintainer setup:

1. Enable and use Chrome Web Store API V2 in the selected Google Cloud project.
2. Create a dedicated service account.
3. Grant that service-account email Chrome Web Store API access for the existing
   publisher and item in the Developer Dashboard.
4. Store its JSON only as the GitHub `release` environment secret
   `CWS_SERVICE_ACCOUNT_JSON`.
5. Store `CWS_PUBLISHER_ID` and `CWS_EXTENSION_ID` as GitHub `release` environment
   variables.

Credentials, JWT assertions, and access tokens must never be committed or printed.
The workflow uses the official `https://www.googleapis.com/auth/chromewebstore` scope,
keeps the token in memory, and only makes fixed Chrome Web Store V2 upload, status, and
normal-review submission requests after local extension gates pass. Google review is an
asynchronous external process, not a successful workflow-completion requirement.
