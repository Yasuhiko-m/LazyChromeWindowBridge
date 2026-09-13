// Optional task-start evidence separates unchanged inherited dirt from this task.
// Without that evidence the existing full candidate audit remains unchanged.
import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';

export async function taskAudit(root, violations) {
  if (!process.env.LCWB_TASK_BASELINE) return { violations, inheritedViolations: [] };
  const inside = file => {
    const resolved = path.resolve(root, file), relative = path.relative(root, resolved);
    assert(relative && !relative.startsWith('..') && !path.isAbsolute(relative), 'Task baseline paths must stay inside Source.');
    return resolved;
  };
  const baseline = JSON.parse(await fs.readFile(inside(process.env.LCWB_TASK_BASELINE), 'utf8'));
  assert.equal(execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(), baseline.startHead,
    'Task-start HEAD changed.');
  const inherited = new Set();
  for (const entry of baseline.inherited) {
    assert.equal(createHash('sha256').update(await fs.readFile(inside(entry.path))).digest('hex').toUpperCase(), entry.sha256,
      'Inherited file changed: ' + entry.path);
    inherited.add(entry.path);
  }
  return { violations: violations.filter(v => !inherited.has(v.path)),
    inheritedViolations: violations.filter(v => inherited.has(v.path)) };
}
