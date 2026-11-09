# AGENTS.md

## Project Goal

This Unity project is intended to explore and implement a GAS-inspired Ability System that can support large-project skill design, long-term maintenance, complex gameplay rules, editor tooling, debugging, and future networking or ECS expansion.

The design direction is:

```text
Production-ready design -> optimization
```

not:

```text
Simplified prototype -> gradual feature patching
```

Core gameplay infrastructure should be designed against Unreal GAS-level capability boundaries first, then optimized, trimmed, or staged for implementation as needed.

## GAS Documentation

The detailed GAS documentation lives in:

```text
GAS/
```

For all GAS-related design and implementation work, read this file first:

```text
GAS/AGENTS.md
```

That file defines the documentation planning rules, subsystem writing requirements, and the principle that core systems should be designed as a complete landing version rather than a temporary simplified version.

## Documentation Authority

The documents currently under `GAS/` are not final design specifications. They are working references, planning notes, and examples for how to write the eventual implementation design.

When answering GAS-related questions or making design decisions:

```text
Do not treat the current documents as the final source of truth.
Use them as context and drafting guidance.
If a user question exposes a gap, simplification, contradiction, or missing UE-level behavior, analyze against Unreal GAS capability boundaries first.
Then update or reinterpret the local documents as needed.
```

This is especially important when the user asks about behavior that differs from the current documents. In that case, do not simply answer from the existing document text. Re-evaluate the design against UE/GAS concepts and the project goal of building a large-project-ready Ability System.

## Implementation Guidance

When implementing GAS-related systems:

```text
Prefer stable runtime data structures.
Prefer explicit lifecycle and ownership.
Prefer complete serialization boundaries.
Prefer debuggable event and state flow.
Prefer future-proof extension points for editor, AI, replay, networking, and ECS.
```

Avoid designs that are easy only for the first demo but expensive to migrate later, such as runtime string-based tags, missing specs, missing handles, missing source tracking, or one-off skill logic bypassing the ability/effect pipeline.
