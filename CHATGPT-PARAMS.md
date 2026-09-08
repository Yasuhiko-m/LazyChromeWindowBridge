# CHATGPT Parameters

- Format Version: 1
- Policy Revision: 2026-08-18
- State: Bootstrap
- Distribution: Lazy AI Deck Starter Pack

## Purpose
ChatGPT Project policy for Codex model/reasoning selection.
Runtime capability is separate. Exchange-Protocol.md is authoritative for how selected values are represented in `_toCodex`.

## Bootstrap
When State is Bootstrap:
1. Read the current Lazy AI Deck Codex runtime model catalog when available.
2. If `SYS-ModelAnalyzing.cmd` is installed, ask the user to run it and attach the bounded CSV.
3. Combine official policy + runtime capability + execution statistics.
4. Produce a complete tuned replacement CHATGPT-PARAMS.md and set State to Tuned.
5. If no statistics exist, use the official baseline.

Do not require full prompts, conversation transcripts, or code for tuning.

## Standard model families
| Role | Exact Model ID | Use |
| --- | --- | --- |
| Luna | `gpt-5.6-luna` | routine / mechanical |
| Terra | `gpt-5.6-terra` | normal judgment / implementation |
| Sol | `gpt-5.6-sol` | architecture / high uncertainty |

Before exact execution, validate the exact pair against current runtime capability. If unavailable, fail closed; do not silently substitute.

## Selection policy

### Luna Low
Completed specification determines the output; mechanical fixed-file generation/replacement; almost no judgment.

### Luna Medium
Routine work with light formatting, verification, or bounded interpretation.

### Terra Medium
Small/localized implementation judgment, diagnostic interpretation, synthesis-oriented documentation, or bounded changes in a clear design.

### Terra High
Normal implementation Revision, multi-file work, UI/infrastructure/tests, meaningful choices inside established architecture.

### Sol High
Architecture-boundary redesign, broad unknown external behavior, consequential approach comparison, high failure impact, or Terra-insufficient retry.

Do not select Sol merely because a Task looks difficult.

## Non-standard reasoning
xhigh / max / ultra may exist but standard policy does not auto-select them.
Use only with explicit policy justification.
Ultra automatic task delegation is not standard Lazy AI Deck behavior; the normal pattern is small Chat-bounded Revisions.

## Historical performance
When bounded metrics exist, prefer evidence for the same Task class.
Optimize for cost per accepted Revision, later cost per completed Milestone, not one-Turn token minimum.
Historical evidence never overrides policy, runtime availability, or safety.

## Compatibility
If the current Exchange Protocol still uses recommendation fields, follow it and do not invent future exact-execution fields.
Migrate to mandatory exact parameters only when current Project runtime/protocol implements that contract.

## Updates
Official policy is distributed through Git and may be updated by whole-file replacement.
Local customization is allowed, but automatic merge with later official replacements is not guaranteed.
