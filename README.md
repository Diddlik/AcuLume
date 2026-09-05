# AcuLume

Local, deterministic, output-aware image sharpening CLI for photographers. Full specification: `docs/AcuLume_IMPLEMENTATION_REQUIREMENTS.md`.

## Status

Phase 0 (bootstrap) + Tasks 1–3 complete: solution/projects, `info` command, and a resize-only `sharpen` baseline (decode → EXIF orientation → resize → encode, no sharpening algorithm yet).

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

dotnet run --project src/AcuLume.Cli -- sharpen photo.jpg --long-edge 1800 --output photo.web.jpg
```

Source files are never overwritten: `--output` must differ from the input path, and if omitted the tool writes `<name>.aculume<ext>` next to the input.

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
