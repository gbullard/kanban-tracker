# Crush headless invocation (verified on this host)

Verified: 2026-08-03
Crush version: v0.68.0

## Command

Executable: `crush` (on PATH)
Arguments:  `run "{prompt}"`
Prompt delivered via: positional argument (also accepts additional context on stdin)
Auto-approve flag: `-y` / `--yolo`
Plain-output flag: none needed

## Exit code behaviour

- Success: 0
- Could not complete the task: 0
- Reliable failure signal? no — result.json is authoritative

## Output characteristics

- Writes ANSI escape sequences to a captured pipe: no
- Approximate lines of output for a small task: 1–2

## Configuration

OpenRouter key location: `C:\Users\Admin\AppData\Local\crush\crush.json`
Model in use: `deepseek/deepseek-v4-pro`