# RULES

Rules for any assistant working in this repository. Read before making changes.

## Workflow

1. Start every task by invoking the `md-orchestrator` skill.
2. Finish every task by invoking the `md-sync` skill.

Neither skill is installed on this machine yet (`~/.claude/skills` is empty and the configured
marketplace does not carry them). Until they are installed, say so explicitly instead of
silently skipping the step.

## Product naming

- Never surface the name of an external API, package, or platform component in the user
  interface. `ConPTY`, `conhost`, `WPF`, `Win32` and the like belong in code comments and
  documentation, not in window chrome, pane headers, status bars, or user-visible messages.
- User-visible text names what the user sees: the shell, the pane, the session state.

## Engineering

- .NET Framework 4.8, C# 7.3, WPF, x64. No third-party terminal control, no new NuGet packages
  without an explicit request.
- Every launched process is assigned to a job object with KILL_ON_JOB_CLOSE. No orphaned shells
  or console hosts after a pane, window, or the application closes.
- Background IO threads must never let an exception escape: a throw on the reader or writer
  thread terminates the whole shell and every other pane with it. Turn failures into an exit the
  pane can display.
- Tear down in order: stop producers, close the pseudo console and job, join the IO threads, and
  only then dispose streams and queues. Disposing a queue or stream under a blocked thread is
  what caused the pane-close crash on 2026-08-04.
- Git and package installs happen only when the user asks for them by name.
