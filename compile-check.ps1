# Assembly-CSharp syntax/type verification without a Unity license.
#
# WHY THIS EXISTS
#   Unity batchmode (-batchmode -quit -nographics) refuses to run when no editor
#   license is active. Worse, PowerShell reports $LASTEXITCODE 0 even though the
#   log says "No valid Unity Editor license found ... return code 198", so the
#   failure looks like a success. This script type-checks the same sources with
#   Roslyn instead.
#
# WHAT IT DOES NOT DO
#   No IL generation you should ship, no scene/prefab validation, no playmode.
#   It only answers "does Assembly-CSharp still compile?".
#
# BASELINE (2026-08-14)
#   0 errors. Any error at all is a regression - fix it, do not raise the baseline.
#
#   History: the baseline was 20x CS0012 from 2026-07-29 to 2026-08-14, confined to
#   BattleGlitchTransition.cs and TransitionUIController.cs. Both use
#   `using DG.Tweening`; DOTween.dll carries typerefs to the old mono mscorlib 2.0
#   that the reference set did not satisfy. Those were never project errors - Unity
#   compiled the code fine. Adding the netfx compat shims (see the reference section
#   below) resolved all 20 without touching a single .cs file.
#
# USAGE
#   pwsh -File compile-check.ps1        (or: powershell -File compile-check.ps1)
#   Outputs csc-output.txt next to this script.

$ErrorActionPreference = 'Stop'

$editor = 'C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Data'
$proj   = Split-Path $PSCommandPath
$csc    = "$editor\DotNetSdkRoslyn\csc.dll"
$out    = $proj

if (-not (Test-Path $csc)) {
    throw "Roslyn csc not found at $csc - check the Unity version in `$editor."
}
if (-not (Test-Path "$proj\Library\ScriptAssemblies")) {
    throw "Library\ScriptAssemblies missing. Open the project in Unity once so package assemblies exist."
}

# --- Sources: Assembly-CSharp scope only -----------------------------------
#   exclude \Editor\   -> Assembly-CSharp-Editor
#   exclude \Plugins\  -> Assembly-CSharp-firstpass (referenced as dll below)
$sources = Get-ChildItem "$proj\Assets" -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '\\Editor\\' -and $_.FullName -notmatch '\\Plugins\\' } |
    ForEach-Object { $_.FullName }
Write-Output "source files: $($sources.Count)"

# --- References ------------------------------------------------------------
$refs = @()
$refs += Get-ChildItem "$editor\Managed\UnityEngine" -Filter *.dll -File |
    ForEach-Object { $_.FullName }
$refs += Get-ChildItem "$proj\Library\ScriptAssemblies" -Filter *.dll -File |
    Where-Object { $_.Name -notmatch 'Editor' -and $_.Name -ne 'Assembly-CSharp.dll' } |
    ForEach-Object { $_.FullName }
$refs += Get-ChildItem "$proj\Assets\Plugins" -Recurse -Filter *.dll -File |
    Where-Object { $_.FullName -notmatch '\\Editor\\' } |
    ForEach-Object { $_.FullName }
$refs += Get-ChildItem "$editor\NetStandard\ref\2.1.0" -Filter *.dll -File -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName }
# Package dlls outside ScriptAssemblies (YarnSpinner core, etc.)
$refs += Get-ChildItem "$proj\Library\PackageCache" -Recurse -Filter *.dll -File -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\Runtime\\DLLs\\' -and $_.FullName -notmatch '\\Editor\\' } |
    ForEach-Object { $_.FullName }
#
# DO NOT add mono's mscorlib.dll here (MonoBleedingEdge\lib\mono\*\mscorlib.dll).
# It collides with netstandard 2.1 and produces CS0433 (duplicate
# SerializableAttribute) plus a cascade of CS0518 "predefined type System.Void is
# not defined" - i.e. core type resolution dies. Tried and reverted 2026-07-29.
#
# DO add the netfx compat shims (2026-08-14). These are NOT mono's mscorlib: they
# are ~47KB pure type-forwarding facades with no type definitions of their own, so
# they cannot collide. Unity adds the same shims when compiling netstandard code
# against legacy .NET Framework DLLs. Without them, DOTween.dll's typerefs to
# mscorlib 2.0 surface as 20x CS0012 in BattleGlitchTransition/TransitionUIController
# even though Unity itself compiles the project fine.
$refs += Get-ChildItem "$editor\NetStandard\compat\2.1.0\shims\netfx" -Filter *.dll -File -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName }
Write-Output "reference assemblies: $($refs.Count)"

# --- Invoke ----------------------------------------------------------------
# Too many files for a command line; use an rsp response file.
$rsp = "$out\csc-args.rsp"
$lines = @(
    '-target:library'
    "-out:$out\AssemblyCSharp-check.dll"
    '-nostdlib+'
    '-noconfig'
    '-langversion:9.0'
    '-define:UNITY_2022_1_OR_NEWER;UNITY_6000_0_OR_NEWER;UNITY_EDITOR;UNITY_STANDALONE_WIN;UNITY_STANDALONE'
    '-nowarn:0169,0414,0649,0067'
)
$lines += $refs    | ForEach-Object { '-r:"' + $_ + '"' }
$lines += $sources | ForEach-Object { '"' + $_ + '"' }
Set-Content -Path $rsp -Value $lines -Encoding utf8

& dotnet $csc "@$rsp" 2>&1 | Tee-Object -FilePath "$out\csc-output.txt" | Out-Null

$errors = Select-String -Path "$out\csc-output.txt" -Pattern 'error CS'
Write-Output ''
Write-Output ('total errors: ' + $errors.Count + '   (baseline: 0)')
if ($errors.Count -eq 0) {
    Write-Output 'PASS - clean.'
    exit 0
}

Write-Output ''
Write-Output 'errors by file:'
$errors |
    ForEach-Object { if ($_.Line -match '^(.+?\.cs)\(') { $Matches[1] } else { '(no file)' } } |
    Group-Object | Sort-Object Count -Descending |
    Format-Table Count, Name -AutoSize

Write-Output 'first errors:'
$errors | Select-Object -First 5 | ForEach-Object { '  ' + $_.Line.Trim() }

Write-Output ''
Write-Output 'REGRESSION: baseline is 0. Fix the cause - do not raise the baseline.'
exit 1
