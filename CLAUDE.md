# Unity CLI Context

This project uses Unity `6000.3.21f1`. The official `unity` CLI and the Unity-Technologies
skill set are synced under `.agents/skills/` (see `unity-cli/SKILL.md` for the full reference).
`com.unity.pipeline` — the Editor-side package the CLI talks to — is already installed in
`Packages/manifest.json`.

## Preferred workflow — drive the live Editor

**Before creating or modifying any GameObject, scene, prefab, or asset, run `unity status`
first** to check for a connected Editor (look for state `ready`). If one is reachable, drive it
live instead of touching project files:

```
unity status                       # is an Editor connected?
unity command                      # discover the commands THIS Editor exposes
unity command eval '...'           # run arbitrary C# against the live Editor
unity command save_scene           # persist the active scene
```

**Never hand-edit `.unity`, `.prefab`, or `.asset` YAML while a live Editor is reachable.**
fileIDs/GUIDs are hand-assigned and easy to get wrong, and changes are invisible to the running
Editor until a reimport. Only fall back to editing files directly when `unity status` shows no
reachable Editor — and say so explicitly.

**Safe Mode gotcha:** if an Editor is running for this project but won't connect, it may be in
Safe Mode from a C# compile error (the Pipeline package doesn't load there). Run
`unity pipeline list` to confirm, fix the compile error, and restart Unity — don't fall back to
raw file edits when this is the actual cause.

## Fallback — batch mode (no live Editor)

```
unity run "C:\Facultad\Stasis-6.3" --editor-version 6000.3.21f1 -- -executeMethod MyScript.Method -logFile build.log
```

Or a direct Editor invocation:

```
"C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe" -quit -batchmode -projectPath "." -executeMethod MyEditorScript.PerformBuild
```

## Project conventions

- Never edit YAML (`.unity` / `.prefab` / `.asset`) directly — use the CLI / live Editor. Only
  for a large volume of changes, write a temporary Editor script under
  `Editor/TemporaryGeneratedScripts` and delete it as the last step.
- Don't ask the user to do manual Editor setup — do it via the CLI.
- Use `[SerializeField]` references instead of `GetComponent`/`GetComponents`/`GetChild`.
- If a null check's failure would be a bad outcome, `Debug.LogError` — don't fail silently.
- Events use `= delegate { }` initialization to avoid null checks.
- Never suppress warnings with `#pragma warning disable`; fix the underlying issue instead.
- Say "done" as the last line when finishing a long process.

Full command reference: `.agents/skills/unity-cli/SKILL.md` and its `references/` folder
(auth/license/cloud, editors/install, projects/templates, build/run/test,
diagnostics/maintenance, MCP/skill/pipeline integration, collaboration).
