// Source-owned candidate-tree audit. Test-All owns the log; never edits Git or remote state.
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';
import { publicImages } from './PublicImages.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const listed = [...new Set(execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z'],
  { cwd: root, encoding: 'utf8' }).split('\0').filter(Boolean))].sort();
const texts = new Map(), violations = [], reviewed = [], links = [];
const publicImagePaths = new Set(publicImages.map(([name]) => name));
const sha = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase();
for (const relative of listed) {
  let bytes;
  try { bytes = await fs.readFile(path.join(root, relative)); } catch (e) { if (e.code === 'ENOENT') continue; throw e; }
  if (/(^|\/)(artifacts|Outputs|bin|obj|profiles|evidence|tmp|TEMP|\.vs|\.git)(\/|$)|(^|\/)\.env(?:\.|$)|\.(?:log|zip|exe|dll|pdb|pem|key|tmp|bak)$/i.test(relative))
    violations.push({ path: relative, kind: 'private-generated-or-binary-path' });
  if (publicImagePaths.has(relative)) continue;
  if (/\.(png|jpe?g|gif|webp|bmp|svg)$/i.test(relative) || bytes.includes(0)) {
    violations.push({ path: relative, kind: 'unreviewed-image-or-binary' }); continue;
  }
  const text = bytes.toString('utf8'); texts.set(relative, text);
  for (const [i, line] of text.split(/\r?\n/).entries()) {
    const record = kind => ({ path: relative, line: i + 1, kind });
    if (/[A-Z]:[\\/]+Users[\\/]|\/Users\/|\/home\/[a-z][\w.-]+/i.test(line)) violations.push(record('personal-directory'));
    if (/gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----|Bearer\s+[A-Za-z0-9_-]{32,}/.test(line))
      violations.push(record('credential-pattern'));
    if (/(?:token|password|secret|capability)\s*[:=]\s*["'][A-Za-z0-9_+/-]{32,}["']/i.test(line)) violations.push(record('secret-literal'));
    for (const email of line.matchAll(/[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}/gi)) {
      const domain = email[0].split('@')[1].toLowerCase();
      if (/^(?:example\.(?:com|test|invalid)|users\.noreply\.github\.com)$/.test(domain)) reviewed.push({ ...record('fixture-or-noreply-address'), domain });
      else violations.push(record('unreviewed-email')); // Do not print the email itself.
    }
    if (/Yasuhiko|ym-sys|account[_-]?id\s*[:=]|machine[_-]?name\s*[:=]/i.test(line)) {
      const authorizedIdentity = (relative === 'LICENSE' && line === 'Copyright (c) 2026 Yasuhiko Mori') ||
        (relative === 'PROJECT.md' && line.includes('MIT, copyright 2026 Yasuhiko Mori')) ||
        (['docs/github-release.md', 'docs/homepage-copy.md', 'docs/releases/v0.1.0.md', 'docs/nuget.md', 'PRIVACY.md', 'docs/chrome-web-store.md', 'docs/releases/v0.1.0-distribution.md',
          'src/LazyChromeWindowBridge.Core/LazyChromeWindowBridge.Core.csproj', 'Scripts/Internal/VerifyNuGet.ps1',
          '.github/workflows/publish-nuget.yml'].includes(relative) && line.includes('Yasuhiko-m/LazyChromeWindowBridge')) ||
        (relative === 'src/LazyChromeWindowBridge.Core/LazyChromeWindowBridge.Core.csproj' && line.trim() === '<Authors>Yasuhiko Mori</Authors>') ||
        (relative === '.github/workflows/publish-nuget.yml' && line.trim() === 'user: Yasuhiko-m') ||
        (relative === 'docs/chrome-web-store.md' && line === 'reported complete by the owner; publisher display name: `yasuhiko-m`. Category: **Tools**.') ||
        (relative === 'docs/nuget.md' && (line.startsWith('Trusted-publishing policy:') || line.startsWith('The exact publisher tuple is')));
      const ownPolicy = relative === 'Scripts/Internal/AuditPublicRelease.mjs' &&
        /^(?:if \(|const authorizedIdentity =|\(relative ===|\(\['docs\/github-release.md'|'\.github\/workflows\/publish-nuget\.yml')/.test(line.trim());
      (authorizedIdentity || ownPolicy ? reviewed : violations).push(record(ownPolicy ? 'audit-policy-literal' : authorizedIdentity ? 'authorized-project-identity' : 'unreviewed-account-identifier'));
    }
  }
}

// Resolve Markdown destinations against repository files, including case and heading fragments.
const anchors = text => {
  const used = new Map(), result = new Set(); let fenced = false;
  for (const line of text.split(/\r?\n/)) {
    if (/^\s*```/.test(line)) { fenced = !fenced; continue; }
    if (fenced || !/^#{1,6}\s/.test(line)) continue;
    const base = line.replace(/^#{1,6}\s+/, '').trim().toLowerCase().replace(/[^\p{L}\p{N}_\s-]/gu, '').replace(/\s/g, '-');
    const n = used.get(base) ?? 0; used.set(base, n + 1); result.add(base + (n ? '-' + n : ''));
  }
  return result;
};
for (const [relative, text] of texts) {
  if (!relative.endsWith('.md')) continue;
  const prose = text.replace(/```[\s\S]*?```/g, '');
  const destinations = [...prose.matchAll(/!?\[[^\]\n]*\]\((<[^>]+>|[^\s)]+)(?:\s+"[^"]*")?\)/g)].map(m => m[1].replace(/^<|>$/g, ''));
  for (const destination of destinations) {
    if (/^(?:https?:|mailto:)/i.test(destination)) continue;
    const [rawFile, fragment] = destination.split('#');
    const target = path.resolve(path.dirname(path.join(root, relative)), decodeURIComponent(rawFile || path.basename(relative)));
    const targetRelative = path.relative(root, target).replaceAll('\\', '/');
    if (targetRelative.startsWith('../') || !listed.includes(targetRelative)) { violations.push({ path: relative, kind: 'broken-relative-link', target: destination }); continue; }
    if (fragment && !anchors(texts.get(targetRelative) ?? '').has(decodeURIComponent(fragment))) violations.push({ path: relative, kind: 'broken-heading-link', target: destination });
    links.push({ path: relative, target: destination });
  }
}

// A manual privacy review is tied to exact pixels. Reject unreviewed metadata/images.
const images = [];
for (const [imagePath, width, height, reviewPath] of publicImages) {
const png = await fs.readFile(path.join(root, imagePath));
assert.equal(png.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
const imageHash = sha(png), review = texts.get(reviewPath);
assert(review?.includes(imageHash), 'Hero changed: repeat manual privacy review and record its exact SHA256.');
assert.equal(png.readUInt32BE(16), width); assert.equal(png.readUInt32BE(20), height);
if (imagePath.includes('/icons/')) assert.equal(png[25], 6, 'Icon must preserve RGBA transparency.');
const chunks = [];
for (let at = 8; at < png.length;) {
  const size = png.readUInt32BE(at), name = png.toString('ascii', at + 4, at + 8);
  assert(at + 12 + size <= png.length); chunks.push(name);
  assert(['IHDR','IDAT','IEND','gAMA','sRGB','pHYs','cHRM'].includes(name), 'Unreviewed PNG metadata: ' + name);
  at += 12 + size;
}
images.push({path:imagePath,width,height,sha256:imageHash,chunks});
}

// Independent ZIP reader validates the .NET packager's actual archive, not an extraction folder.
const names = ['bindings.js','bootstrap.js','downloads.js','icons/icon-16.png','icons/icon-32.png','icons/icon-48.png','icons/icon-128.png','manifest.json','monitor.js','service-worker.js'];
const zipPath = 'artifacts/cws/LazyChromeWindowBridge.Extension-0.1.0-cws.zip';
const zip = await fs.readFile(path.join(root, zipPath)), end = zip.length - 22;
assert.equal(zip.readUInt32LE(end), 0x06054b50); assert.equal(zip.readUInt16LE(end + 20), 0);
assert.equal(zip.readUInt16LE(end + 10), names.length);
let at = zip.readUInt32LE(end + 16); const entries = [];
function crc32(bytes) { let n = 0xffffffff; for (const byte of bytes) { n ^= byte; for (let i = 0; i < 8; i++) n = (n >>> 1) ^ (n & 1 ? 0xedb88320 : 0); } return (n ^ 0xffffffff) >>> 0; }
for (const expected of names) {
  assert.equal(zip.readUInt32LE(at), 0x02014b50);
  const length = zip.readUInt32LE(at + 24), nameLength = zip.readUInt16LE(at + 28), extra = zip.readUInt16LE(at + 30), comment = zip.readUInt16LE(at + 32);
  const name = zip.toString('utf8', at + 46, at + 46 + nameLength);
  assert.equal(name, expected); assert.equal(zip.readUInt16LE(at + 10), 0); // stored, not compressed
  assert.equal(zip.readUInt16LE(at + 12), 0); assert.equal(zip.readUInt16LE(at + 14), 33);
  assert.equal(zip.readUInt32LE(at + 38), 0); assert.equal(extra, 0); assert.equal(comment, 0);
  const local = zip.readUInt32LE(at + 42); assert.equal(zip.readUInt32LE(local), 0x04034b50);
  const offset = local + 30 + zip.readUInt16LE(local + 26) + zip.readUInt16LE(local + 28);
  const content = zip.subarray(offset, offset + length);
  assert.equal(content.length, length); assert.equal(crc32(content), zip.readUInt32LE(at + 16));
  const raw = await fs.readFile(path.join(root, 'src/LazyChromeWindowBridge.Extension', name));
  const source = name.endsWith('.png') ? raw : Buffer.from(raw.toString('utf8').replace(/^\uFEFF/, '').replaceAll('\r\n','\n'));
  assert(content.equals(source), 'Packaged bytes differ: ' + name);
  if (name === 'manifest.json') assert.equal(JSON.parse(content).version, '0.1.0');
  entries.push(name); at += 46 + nameLength + extra + comment;
}
assert.equal(at, end); assert(!listed.includes(zipPath), 'Generated ZIP must remain ignored/untracked.');
console.log(JSON.stringify({ check: 'public-release-audit', result: violations.length ? 'FAIL' : 'PASS', candidateFiles: listed.length,
  linksChecked: links.length, reviewedIdentities: reviewed, images,
  package: { path:zipPath, entries, bytes:zip.length, sha256:sha(zip) }, violations,
  boundary:'Current tracked + untracked candidate files, not ignored evidence. Manual pixel review is hash-pinned; heuristic secret scan is not exhaustive. Historical commit email remains for owner review; no history rewrite.' }));
if (violations.length) process.exitCode = 1;
