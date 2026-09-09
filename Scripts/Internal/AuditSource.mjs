// Source-owned audit; the public validation wrapper records this JSON in its flat log.
import fs from 'node:fs/promises';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const projectId = ['Lazy', 'Chrome', 'Extension'].join('');
const oldTerms = [projectId, ['Lazy', ' Chrome', ' Extension'].join(''),
  ['Caller', 'Harness'].join(''), ['Caller', ' Harness'].join(''), '/' + ['lazy', 'chrome', 'extension'].join('-')];
const policy = JSON.parse(await fs.readFile(path.join(root, 'docs/legacy-name-allowlist.json'), 'utf8'));
const rules = policy.rules.map(r => ({ ...r, regex: new RegExp(r.linePattern.replaceAll('<project-id>', projectId)) }));
const listed = [...new Set(execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z'],
  { cwd: root, encoding: 'utf8' }).split('\0').filter(Boolean))].sort();
const files = [], residuals = [], violations = [];
for (const relative of listed) {
  let bytes;
  try { bytes = await fs.readFile(path.join(root, relative)); }
  catch (error) { if (error.code === 'ENOENT') continue; throw error; } // Deleted old paths are not current Source.
  files.push(relative);
  if (oldTerms.some(term => relative.includes(term))) violations.push({ path: relative, kind: 'old-name-path' });
  if (/(^|\/)(bin|obj|Outputs|profiles|evidence|TEMP|tmp)(\/|$)|\.(log|png|jpg|jpeg|pdb|dll|exe)$/i.test(relative))
    violations.push({ path: relative, kind: 'generated-or-private-artifact' });
  if (bytes.includes(0)) { violations.push({ path: relative, kind: 'unexpected-binary' }); continue; }
  const text = bytes.toString('utf8');
  for (const [index, line] of text.split(/\r?\n/).entries()) {
    const terms = oldTerms.filter(term => line.includes(term));
    if (terms.length) {
      const rule = rules.find(r => r.path === relative && r.regex.test(line));
      const occurrence = { path: relative, line: index + 1, terms, reason: rule?.reason ?? null };
      (rule ? residuals : violations).push(occurrence);
    }
    if (/[A-Z]:[\\/]+Users[\\/]/i.test(line) || /Bearer\s+[A-Za-z0-9]{32,}/.test(line) ||
        /(?:token|password|secret)\s*[:=]\s*['"][A-Za-z0-9+/]{32,}['"]/i.test(line))
      violations.push({ path: relative, line: index + 1, kind: 'developer-path-or-secret-literal' });
  }
}
const extensionRoot = path.join(root, 'src/LazyChromeWindowBridge.Extension');
const manifest = JSON.parse(await fs.readFile(path.join(extensionRoot, 'manifest.json'), 'utf8'));
assert.equal(manifest.name, 'LazyChromeWindowBridge');
assert.equal(manifest.manifest_version, 3);
assert.deepEqual([...manifest.permissions].sort(), ['alarms', 'debugger', 'downloads', 'storage']);
assert.deepEqual(manifest.host_permissions, ['http://127.0.0.1/*']);
assert.deepEqual(manifest.content_scripts[0].matches, ['http://127.0.0.1/lazy-chrome-window-bridge/bootstrap*']);
assert.equal(manifest.content_scripts.length, 1);
const monitor = await fs.readFile(path.join(extensionRoot, 'monitor.js'), 'utf8');
const commands = [...monitor.matchAll(/sendCommand\(\{ tabId \}, '([^']+)'/g)].map(m => m[1]).sort();
assert.deepEqual(commands, ['Page.captureScreenshot', 'Page.getLayoutMetrics']);
const downloadCalls = [];
for (const name of manifest.content_scripts[0].js.concat(['bindings.js', 'monitor.js', 'service-worker.js', 'downloads.js'])) {
  const code = await fs.readFile(path.join(extensionRoot, name), 'utf8');
  assert(!/['"](?:Runtime\.|DOM\.|Network\.|Input\.)/.test(code), 'Unexpected debugger domain in ' + name);
  for (const call of code.matchAll(/(?:chrome|this\.browser)\.downloads\.([A-Za-z]+)/g)) {
    assert(['onCreated', 'onChanged', 'search'].includes(call[1]), 'Forbidden download capability: ' + call[1]);
    downloadCalls.push({ file: name, member: call[1] });
  }
}
assert.deepEqual([...new Set(downloadCalls.map(c => c.member))].sort(), ['onChanged', 'onCreated', 'search']);
const projects = files.filter(f => f.endsWith('.csproj'));
assert.equal(projects.length, 4, 'Only Core, SampleCaller, Core.Tests and PublicApi.Tests projects');
for (const project of projects) assert(!/<PackageReference\b/.test(await fs.readFile(path.join(root, project), 'utf8')), 'No new external NuGet dependency');
const report = { check: 'source-name-security-hygiene-audit', result: violations.length ? 'FAIL' : 'PASS',
  filesScanned: files.length, pathAudit: 'All current repository file paths and their parent directories',
  projects, extensionPermissions: manifest.permissions, debuggerCommands: commands, downloadCalls,
  allowlistedOccurrences: residuals, violations,
  boundary: 'Repository Source only; ignored build output, local evidence and transient handoff excluded. Secret patterns are a bounded static check, not a comprehensive credential proof.' };
console.log(JSON.stringify(report));
if (violations.length) process.exitCode = 1;
