# Shared Agent Instructions

This file is the canonical instruction source for Codex, Claude Code, and GitHub Copilot CLI.

## Maintenance

- Keep this file synchronized with the repository's verified behavior.
- Update this file in the same change when build commands, validation steps, architecture constraints, workflows, or conventions change.
- Remove obsolete instructions instead of appending corrections.
- Record only durable facts that are not obvious from the codebase.
- Tool-specific instruction files may contain only an import of this file and genuine tool-specific exceptions.

## First-use onboarding

If any `[TO FILL]` entry remains, complete this onboarding before implementing the user's first task:

1. Inspect the repository and determine every project fact that can be verified from existing files and commands.
2. Do not ask the user for information that can be discovered reliably from the repository.
3. Briefly present the discovered facts, then ask guided questions only for the remaining decisions or unknowns.
4. Ask one focused question or one closely related group of no more than three questions at a time. Explain why each answer matters and offer a recommended option when useful while allowing a free-form answer.
5. Cover the remaining topics in this order: purpose and scope, runtimes and platforms, build and validation commands, environment requirements, then architecture and generated-file constraints.
6. After the user answers, replace the applicable placeholders with concise verified facts, remove entries that do not apply, and record unresolved decisions explicitly as `OPEN` rather than inventing an answer.
7. Summarize what was written to this file, then continue with the user's original task.

## Implementation

- Implement only what the current requirement needs.
- Prefer editing existing code over adding files, layers, helpers, or abstractions.
- Use the standard library and existing dependencies before writing custom implementations.
- Do not add speculative extension points, configuration, parameters, or abstractions.
- Add a dependency only when it provides a concrete benefit and does not duplicate existing functionality.
- Preserve validation, security, accessibility, error handling, and data-integrity safeguards.

## Language and comments

- Use English for source code, identifiers, comments, tests, documentation, logs, and commit messages.
- Keep commit messages limited to change- and process-related content.
- Comments explain why a decision, constraint, workaround, or non-obvious trade-off exists.
- Do not comment what readable code already expresses.
- Prefer clear naming and small functions over explanatory comments.

## Working method

- Inspect the relevant implementation and existing conventions before editing.
- Search for an existing implementation before creating a new one.
- Make the smallest coherent change that fully satisfies the request.
- Preserve unrelated user changes.
- Do not perform unrelated refactoring during a focused change.
- Ask before destructive, irreversible, security-sensitive, or materially out-of-scope actions.

## Verification

- Run the narrowest relevant test, build, lint, format, or executable check after the last change.
- Add or update tests for non-trivial behavior changes and bug fixes.
- Do not claim success without current verification evidence.
- Report what was verified and what could not be verified.
- Distinguish product failures from environment, permission, network, and tooling failures.

## Security

- Never commit, print, store, or document secrets, tokens, credentials, or private keys.
- Validate data at user, file, environment, process, and network boundaries.
- Preserve authentication, authorization, escaping, permission checks, and safe defaults.
- Do not weaken security controls to make tests or local execution pass.

## Project facts

- Purpose: AcuLume — a local, deterministic tool for output-aware adaptive multi-frequency image sharpening (photography). No cloud, no AI. Ships a CLI and a desktop GUI over one shared engine. Full spec: `docs/AcuLume_IMPLEMENTATION_REQUIREMENTS.md` (written CLI-only; the GUI is a later addition on top of the same `AcuLume.Core`).
- Primary languages and runtimes: C# 14 on .NET 10 LTS (`net10.0`); GUI on Avalonia 12 with CommunityToolkit.Mvvm.
- Important entry points: `src/AcuLume.Cli/CliApp.cs` (CLI), `src/AcuLume.Gui/Program.cs` (GUI; optional first argument opens an image), `src/AcuLume.Core/AcuLumeProcessor.cs` (file-to-file pipeline used by both).
- Build command: `dotnet build`.
- Test command: `dotnet test` (xUnit).
- Lint and format command: none configured; `dotnet format` works.
- Local run command: `dotnet run --project src/AcuLume.Cli -- info <image>`; GUI: `dotnet run --project src/AcuLume.Gui [image]`.
- Required environment: .NET 10 SDK; NetVips + libvips native binaries (via NetVips.Native.* packages, ~8.18.x); dev target is Windows 11 x64 first, Linux x64 second (per spec §47 Phase 5).
- Architecture constraints: `AcuLume.Core` must not reference `AcuLume.Cli` or `AcuLume.Gui` — both are thin adapters. The GUI must not reimplement pipeline maths: it composes `AcuLume.Core` stages (`CaptureSharpenStage`, `ResizeEngine`, `OutputSharpenStage`) so the interactive preview and an export agree — a stage added to `AcuLumeProcessor` must be added to `PreviewRenderer` in the same change, or the preview silently stops matching the export. GUI previews render at the *output* resolution, because sharpening is output-size dependent. Processing must be deterministic (no randomness) for identical input+preset+version. Internal sharpening math uses float precision, luminance/detail-based (not independent RGB channel sharpening). No per-pixel managed C# loops in the main pipeline — compose via NetVips/libvips ops. Never overwrite source files by default. No telemetry, no image upload, no external API/cloud calls. Presets are versioned JSON (System.Text.Json) with strict validation (reject malformed values, no silent clamping). Do not hard-code the `1800`px web target inside Core algorithm logic — output-size must be passed as context.
- Generated files: standard `bin/`/`obj/`; Avalonia XAML code-behind partials.
- Files or directories not to edit manually: `docs/AcuLume_IMPLEMENTATION_REQUIREMENTS.md` is the canonical spec — treat as a requirements source, update only to record genuine spec changes, not implementation notes. `docs/AcuLume interactive app/` and `docs/AcuLume interactive app.zip` are the design-canvas mockup the GUI is built from — read them as the visual reference, do not edit them.
- Platform-specific constraints: NetVips native binaries are platform-specific; Windows x64 is the first supported release target, Linux x64 second (spec §47).

GUI parameter coverage: the Sharpen panel drives preset, output target/long edge, upscale, quality,
overall, both bands' amount+radius, dark/light detail, the three protection amounts with on/off, and
capture sharpening (level, engine, assumed blur, iterations). The granular protection thresholds
(`edge-threshold`, `edge-softness`, `edge-blur`, `noise-threshold`, `noise-softness`, `halo-window`,
`halo-dark-limit`, `halo-light-limit`) and the Phase 3 resize research flags are CLI-only by
decision — calibration knobs, not controls. `PreviewRenderer` caches the capture-sharpened source
separately from the resize, so an output slider does not re-run capture sharpening and a capture
change invalidates the resize below it.

GUI status: all six screens are implemented — Sharpen (live preview, before/after/split, export), Batch (queue, progress, cancel), Presets (built-in + user library under `%APPDATA%/AcuLume/presets`, import/export/duplicate/rename/delete), Compare (the spec's five-variant comparison set), Image Info and Settings.

GUI layout constraint: never nest a `ListBox` (or any control with its own `ScrollViewer`) inside a page `ScrollViewer` that gives it unbounded height — layout recurses until the process dies with no managed exception and no event-log entry. Use an `ItemsControl` and put selection state on the item view model instead.

## Definition of done

A change is complete when:

- The requested behavior is implemented.
- Relevant verification passes after the final edit.
- Appropriate error paths and edge cases are handled.
- Documentation and this file reflect changed behavior or workflows.
- No unrelated files, abstractions, or dependencies were introduced.
- Remaining limitations and unverified points are stated clearly.
