# GitHub release preparation

These are post-acceptance maintainer instructions, not a script to run during candidate
work. At R008 preparation the repository is PRIVATE. R008 does not push, tag, publish, change
remote configuration, visibility, About settings or another website.

## Recommended About metadata
Repository: [Yasuhiko-m/LazyChromeWindowBridge](https://github.com/Yasuhiko-m/LazyChromeWindowBridge).

Description (exact):
> Windows-to-Chrome bridge for session-bound windows, native PARK/RESTORE, human-only monitoring and download lifecycle events — without DOM automation.

Topics (exact): chrome, chrome-extension, windows, dotnet, win32, manifest-v3,
browser-integration, window-management, multi-monitor, chrome-extension-api.

No homepage URL is proposed: [homepage copy](homepage-copy.md) is text for a separate
site to consume later. It does not authorize editing that site.

## Local extension asset
From the repository root with PowerShell7:

```powershell
.\Scripts\Package-Extension.ps1
```

Output: artifacts/LazyChromeWindowBridge.Extension-v0.1.0.zip (ignored, not committed).
Each invocation has its own flat Scripts/Outputs packaging log. It includes exactly
bindings.js, bootstrap.js, downloads.js, manifest.json, monitor.js and service-worker.js
at the archive root. Extract into a dedicated folder and use **Load unpacked** there.
There is no installer, updater, CRX signing or Web Store package.

The script sorts an explicit six-file allowlist, uses UTF-8 without BOM and LF in the
archive, fixed1980 timestamps, zero external attributes and no compression. It builds
twice and requires identical SHA256. Source bytes are never rewritten. The stable
validation audit independently parses CRC/central-directory entries and compares their
content with canonical Source. Rebuild using the accepted checkout, not stale artifacts.
The release asset is v0.1.0; the accepted runtime manifest is still0.0.7. This deliberate
distinction avoids a runtime change solely for publication. The ZIP contains extension
files only; downstream redistribution must also retain the repository MIT notice.

## Privacy review before publication
The working tree audit includes tracked files and untracked candidate files, excludes
ignored local artifacts/evidence and permits only the reviewed hero PNG as a committed
binary. It scans paths, emails, credential patterns, account identifiers and relative
Markdown links; exact source name/history exceptions are in
[the old-name allowlist](legacy-name-allowlist.json). It is a bounded static audit, not
a proof against every possible secret. Review findings before publication.

The screenshot privacy record is [here](images/privacy-review.md). Keep just that one
useful demo image. Do not commit browser profiles, generated ZIPs, logs or raw captures.

Git history contains historic author/committer email metadata. R008 does not rewrite
history or change old SHAs. Public visibility will expose published Git history, including
that metadata and governance records. Review both history and the current tree before
the final visibility change. Current local commit identity uses the account's GitHub
noreply address; the R007 checkpoint used it. To derive future local configuration
from the authenticated account rather than inventing an address:

```powershell
gh auth status
$releaseAccount = gh api user | ConvertFrom-Json
git config --local user.email "$($releaseAccount.id)+$($releaseAccount.login)@users.noreply.github.com"
git config --get user.email
```

This changes future local commits only. If GitHub rejects preserved history with GH007,
stop: the account owner must decide whether to allow that historic email metadata to
be published through GitHub's email privacy controls. Do not substitute credentials,
disable privacy protection automatically, amend old commits or rewrite history.

## Final steps after Chat acceptance and publication authorization
Run in PowerShell7 from the repository root. Check every exit code and stop on a
failure. Do not run these blocks during R008 candidate work.

1. Establish accepted R008 in PLAN.md / Revisions.md / PLANS.md first. Review the exact
   candidate diff, update the privacy record if the image changed, and run complete
   clean validation with an isolated Chrome for Testing executable.

```powershell
$ErrorActionPreference = 'Stop'
.\Scripts\Test-All.ps1 -Clean -ChromeExecutable $chromeForTesting
if ($LASTEXITCODE) { throw 'Validation failed' }
git diff --check -- . ':(exclude)AGENTS.md' ':(exclude)Exchange-Protocol.md'
if ($LASTEXITCODE) { throw 'Task whitespace gate failed' }
git add -- LICENSE README.md PROJECT.md SPEC.md PLAN.md Revisions.md PLANS.md Scripts/Test-All.ps1 Scripts/Package-Extension.ps1 Scripts/Internal/PackageExtension.ps1 Scripts/Internal/AuditSource.mjs Scripts/Internal/AuditPublicRelease.mjs docs/architecture.md docs/downloads.md docs/limitations.md docs/testing.md docs/github-release.md docs/homepage-copy.md docs/releases/v0.1.0.md docs/images/monitor-overview.png docs/images/privacy-review.md
if ($LASTEXITCODE) { throw 'Staging failed; stop for review' }
git diff --cached --name-only
git diff --cached --check
if ($LASTEXITCODE) { throw 'Staged whitespace gate failed' }
if (@(git diff --cached --name-only -- AGENTS.md Exchange-Protocol.md).Count) { throw 'Protected governance staged; stop for review' }
git commit -m 'docs: establish V1-M002-R008 public release preparation'
if ($LASTEXITCODE) { throw 'Checkpoint failed' }
$releaseCommit = (git rev-parse HEAD).Trim()
git status --short
```

Only the unchanged inherited AGENTS.md / Exchange-Protocol.md dirt should remain.
Never stage, normalize, revert or commit those files. CHANGELOG.md, Scripts/Outputs
and artifacts remain ignored. Preserve the exact accepted checkpoint in the handoff.

2. Verify origin and branch, then push main without forcing. Stop on GH007 as above.

```powershell
if ((git branch --show-current).Trim() -ne 'main') { throw 'Expected main' }
if ((git remote get-url origin).Trim() -ne 'https://github.com/Yasuhiko-m/LazyChromeWindowBridge.git') { throw 'Unexpected origin' }
git push origin main
if ($LASTEXITCODE) { throw 'Push failed; stop' }
$remoteMain = (git ls-remote origin refs/heads/main).Split()[0]
if ($remoteMain -ne $releaseCommit) { throw 'Remote main mismatch' }
```

3. Inspect both local and remote v0.1.0 before creating it. No tag exists locally at
   preparation time. For an absent remote/local tag:

```powershell
git tag --list v0.1.0
git ls-remote origin refs/tags/v0.1.0 'refs/tags/v0.1.0^{}'
# Run only after confirming both are absent:
git tag v0.1.0 $releaseCommit
if ($LASTEXITCODE) { throw 'Tag creation failed' }
git push origin refs/tags/v0.1.0
if ($LASTEXITCODE) { throw 'Tag push failed' }
```

If either tag already resolves to the accepted commit, reuse it. If a prior private
baseline tag differs, stop for explicit approval to replace that exact unpublished
tag; never move a published release tag as routine work. After that separate approval,
record the existing remote tag object's SHA in $oldRemoteTagObject and use:

```powershell
# Conditional replacement only, after approval and verifying no published release uses it:
git tag -f v0.1.0 $releaseCommit
if ($LASTEXITCODE) { throw 'Local tag replacement failed' }
git push "--force-with-lease=refs/tags/v0.1.0:$oldRemoteTagObject" origin refs/tags/v0.1.0
if ($LASTEXITCODE) { throw 'Tag lease failed; inspect remote again' }
```

Verify local peeled target and remote peeled target (use the direct object SHA for a
lightweight tag; the ^{} line for an annotated tag) both equal $releaseCommit:

```powershell
git rev-parse 'v0.1.0^{commit}'
git ls-remote origin refs/heads/main refs/tags/v0.1.0 'refs/tags/v0.1.0^{}'
```

4. Apply About metadata only after acceptance. The flags below are documented by the
   installed GitHub CLI help; these commands were not executed during preparation.

```powershell
$releaseRepo = 'Yasuhiko-m/LazyChromeWindowBridge'
gh repo edit $releaseRepo --description 'Windows-to-Chrome bridge for session-bound windows, native PARK/RESTORE, human-only monitoring and download lifecycle events — without DOM automation.' --add-topic chrome,chrome-extension,windows,dotnet,win32,manifest-v3,browser-integration,window-management,multi-monitor,chrome-extension-api
if ($LASTEXITCODE) { throw 'About update failed' }
gh repo view $releaseRepo --json visibility,description,repositoryTopics,url
```

5. After the owner reviews historic metadata and authorizes publication, make the
   repository public, then create the release from the verified tag and generated asset:

```powershell
gh repo edit $releaseRepo --visibility public --accept-visibility-change-consequences
if ($LASTEXITCODE) { throw 'Visibility update failed' }
$releaseView = gh repo view $releaseRepo --json visibility | ConvertFrom-Json
if ($releaseView.visibility -ne 'PUBLIC') { throw 'Visibility verification failed' }
.\Scripts\Package-Extension.ps1
if ($LASTEXITCODE) { throw 'Packaging failed' }
gh release create v0.1.0 artifacts/LazyChromeWindowBridge.Extension-v0.1.0.zip --repo $releaseRepo --verify-tag --title 'LazyChromeWindowBridge v0.1.0' --notes-file docs/releases/v0.1.0.md
if ($LASTEXITCODE) { throw 'Release creation failed' }
gh release view v0.1.0 --repo $releaseRepo --json url,tagName,assets,isDraft,isPrerelease
```

6. Open the repository and release signed out: verify README, hero image, Mermaid
   diagrams, MIT recognition, topics, relative links and asset download/extraction.
   Check remote main and v0.1.0 still target the accepted commit. Public rendering
   cannot be verified while this candidate intentionally remains private.
