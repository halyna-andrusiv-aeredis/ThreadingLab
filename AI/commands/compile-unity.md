# Compile Unity

Run Unity batchmode compile and fail on compiler errors. `/build-feature` runs this between **implement** and **review** for code tasks (Gate G2).

## Arguments

Optional log path (default: timestamped file under `AI/artifacts/`):

```text
/compile-unity
/compile-unity AI/artifacts/unity-compile.log
```

## Run

From repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File AI/scripts/compile-unity.ps1
```

Flags:

- `-SkipIfUnavailable` — exit 0 with warning if Unity is missing or another editor instance has the project open (use only when the user confirms compile is clean in the open editor)

Unity path resolution:

1. `-UnityEditor` parameter
2. `$env:UNITY_EDITOR`
3. Unity Hub: `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe` (from `ProjectVersion.txt`)

## After running

Output pass/fail, compiler error lines (if any), log path.

On **FAIL**: fix code before `/review-task`. Do not approve review while compile is broken.

Recommend manual run when implementing outside `/build-feature`.

Do not modify code unless user asks to fix compile failures.
