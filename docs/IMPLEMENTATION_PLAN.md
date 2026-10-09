# ATLAS CAD — Codex implementation plan (Option B)

## Goal and current evidence
The private STEELFLOW AutoLISP performs LINE/ARC/LWPOLYLINE handling, text/profile matching, UCS/layer handling, and current MLINE generation. `SF-LoadLibrary`/`SF-LoadStyleFile` load MLN/MLNX and `SF-DrawMline` creates MLINE. At least one MLN can define multiple styles. The existing bridge is not integrated or compiled/tested with AutoCAD and its development gate denies execution.

## Deliverables by milestone

### M0 — Baseline and safety (1–2 days)
- Inventory projects, target frameworks, actual Autodesk assembly references, and current test/build errors.
- Establish private fixture storage; copy source LISP and MLN only into ignored local folders after user provides them to Codex environment.
- Produce behavior inventory of STEELFLOW public command entrypoints and affected helper functions; do not reproduce proprietary source in documentation.
- Capture baseline drawings/screenshots and entity data for LINE, ARC, LWPOLYLINE, UCS, layer, undo with licensed AutoCAD.
**Acceptance:** inventory, reproducible build instructions, source assets excluded from distribution and Git.

### M1 — Minimal C# bridge (3–6 days)
- Create two real AutoCAD adapter projects, one for 2022–2024 (.NET Framework 4.8) and one for 2025–2026 (.NET 8), with configurable Autodesk reference paths and CopyLocal=false.
- Define versioned DTO: requestId, styleName, pointsWcs (x/y/z), layerId/name, closed flag, source handle, expected drawing identity; validate finite coordinates and >=2 distinct points.
- Choose an AutoLISP-to-C# transport supported by both target versions. Prototype one documented approach (e.g. AutoCAD command invocation with short opaque request ID and locally stored payload); do not pass untrusted unescaped command text or enormous point lists through command arguments. Protect payload location against cross-user modification and cleanup on failure.
- Implement `ATLAS_DRAW_MLINE` using an existing synthetic style; create in a transaction; return success/failure and new entity handle through a documented response channel.
- Define explicit UCS/WCS handling and MLINE justification/scale semantics matching existing output.
**Acceptance:** C# can create MLINE from LISP-computed points; failure preserves original LINE; tests manually repeated in AutoCAD 2022 and 2026.

### M2 — Style import (4–8 days)
- Build offline MLN parser/normalizer with golden tests for single and multiple styles, element offsets, colors, linetypes, caps, flags, and unit conventions. Reject malformed files and duplicate/ambiguous style names.
- Compare AutoCAD-created style geometry and metadata against original imported style; do not assume MLN grammar solely from sample files.
- Implement C# `ATLAS_ENSURE_STYLE` that installs missing styles in drawing via supported API, idempotently; decide and document collision behavior.
- Keep source MLN private and out of client distribution.
**Acceptance:** exact style count and material style properties match approved fixtures; repeat import does not duplicate or corrupt styles.

### M3 — Integrate STEELFLOW (3–6 days)
- Work on a private copy of STEELFLOW. Replace only style loading and final draw boundary initially; preserve normalization, proximity matching, arcs, layer/UCS, and user commands.
- Define atomicity/rollback contract: never delete source entity on bridge timeout or partial failure; ensure undo remains intuitive.
- Test cancellation, locked/frozen layers, closed polylines, non-world UCS, drawing switch, document lock, zero-length segments, existing styles.
**Acceptance:** parity checklist passes for LINE/ARC/LWPOLYLINE using representative DWGs.

### M4 — Device licensing (5–10 days)
- Backend issues short-lived digitally signed entitlements; plugin verifies signature, product, activation, device key, expiry and protocol version.
- Device key pair generated on device; private key protected with OS facilities where available. Server stores public key and revocation/generation state.
- Activation and renewal online; bounded offline window (proposed 72h), transfer invalidates future renewals of old activation. Document impossibility of instant offline revocation.
- Never use a deterministic local key generator or XOR for production security. Protect keys and do not hardcode universal decrypt key.
**Acceptance:** invalid/missing/expired token denied; copying folder to second PC denied; device transfer and offline expiry behave as specified.

### M5 — Protected catalog packaging (4–8 days)
- Build encrypted authenticated catalog (e.g. AES-256-GCM) with versioning, integrity checks and per-product key handling. Define online key release/rotation and offline grace tradeoffs.
- Ensure style resolution and creation are gated inside C#, not merely by LISP boolean checks.
- Document that DWG-stored styles remain inspectable and C# can be reverse engineered.
**Acceptance:** tampered catalog rejected; unlicensed device cannot use protected style operations; no raw MLN in installer.

### M6 — Packaging and release readiness (5–10 days)
- Install separate builds with version-aware Autodesk bundle/package manifest and appropriate code signing where feasible.
- Provide diagnostics without logging proprietary geometry or license secrets; uninstall, upgrade, recovery instructions.
- CI runs unit tests; Windows runner builds with authorized Autodesk dependencies; manual matrix for 2022–2026.
**Acceptance:** clean install and uninstall; documented smoke test; no private fixtures in artifact.

## Priority and scope
Do M0→M1→M2→M3 before M4/M5. No website, payments, AI, Excel export, or macOS/Linux in this prototype. Estimates are engineering planning ranges, not guarantees.

## Engineering contracts to decide explicitly
- Which AutoCAD versions/editions (full AutoCAD vs LT) are in scope?
- Source geometry deletion: C# in one transaction vs LISP after verified response.
- Point coordinates WCS vs UCS, planar projection, normal vector, justification and scale.
- Exact MLINE style naming/collision policy and multiple styles per MLN.
- License activation experience and offline duration.

## First Codex task (copy/paste)
Inspect this repo and read AGENTS.md and docs/IMPLEMENTATION_PLAN.md. Start ONLY M0 and M1. First report the existing code's actual buildability and missing Autodesk references. Then implement a versioned LISP↔C# request/response bridge with synthetic MLINE style and safe failure behavior, targeting AutoCAD 2022 and 2026. Preserve all proprietary LISP and MLN sources unchanged; do not implement payment, production licensing, or encryption yet. Add automated contract tests and a Windows/AutoCAD manual test checklist. Do not claim runtime success unless executed in a real AutoCAD installation. Provide a concise list of changed files, exact build/test commands, limitations, and next steps.
