# AcuLume

Local, deterministic, output-aware image sharpening CLI for photographers. Full specification: `docs/AcuLume_IMPLEMENTATION_REQUIREMENTS.md`.

## Status

Phase 1 (spec Tasks 1-9) complete: solution/projects, `info`, a full adaptive multi-frequency
output-sharpening pipeline (fine + medium bands, dark/light asymmetric split, edge protection,
noise protection, halo limiter — see `docs/ALGORITHM.md`), presets, and a `compare` command for
visual before/after evaluation. Phase 2 (empirical calibration against real photos) is next.

## Requirements

- .NET 10 SDK (see `global.json`)

## Build & test

```bash
dotnet build
dotnet test
```

## Quick start

```bash
dotnet run --project src/AcuLume.Cli -- info photo.jpg

dotnet run --project src/AcuLume.Cli -- sharpen photo.jpg --preset web-1800-natural --output photo.web.jpg

# Manual parameter overrides (always take precedence over the preset):
dotnet run --project src/AcuLume.Cli -- sharpen photo.jpg --preset web-1800-natural --fine-amount 1.2 --output photo.web.jpg
```

Source files are never overwritten: `--output` must differ from the input path, and if omitted the tool writes `<name>.aculume<ext>` next to the input.

## Presets

Built-in presets (see `presets/*.json`): `full-natural`, `web-1800-natural`, `web-1800-crisp`
(experimental), `neutral` (all sharpening disabled, useful as a baseline). `--preset` also accepts
a path to a custom JSON file with the same schema.

```bash
dotnet run --project src/AcuLume.Cli -- preset list
dotnet run --project src/AcuLume.Cli -- preset show web-1800-natural
dotnet run --project src/AcuLume.Cli -- preset validate my-preset.json
```

## Comparing output quality

`compare` generates original / resize-only / naive-USM-baseline / AcuLume-natural / AcuLume-crisp
variants side by side (spec section 43-44), optionally with fixed crop regions repeated across
every variant for close inspection:

```bash
dotnet run --project src/AcuLume.Cli -- compare photo.jpg --output ./compare \
  --crop edge=1200,800,300,300 --crop foliage=2000,1500,300,300
```

## Exit codes

| Code | Meaning |
|---|---|
| 0 | success |
| 1 | general processing error |
| 2 | invalid command/options |
| 3 | input not found / inaccessible |
| 4 | unsupported format |
| 5 | invalid preset |
| 6 | output conflict |
| 7 | cancelled |

---

# Shared coding-agent template

Provides one living instruction source for Codex, Claude Code, and GitHub Copilot CLI.

## Use as a project template

Include these files in each new project:

- `AGENTS.md`, the canonical shared instructions and project facts
- `CLAUDE.md`, the Claude Code import
- `.github/copilot-instructions.md`, the Copilot CLI import

When an agent first reads the template, it inspects the repository, presents the facts it can verify, and guides the user through the remaining `[TO FILL]` decisions. It then updates `AGENTS.md` before starting the original task.

## Maintain the base

Edit only `AGENTS.md` for shared behavior. Keep adapter files limited to their import unless a tool requires a genuine exception. Later project changes must keep the project facts synchronized with verified commands, architecture constraints, workflows, and conventions.
