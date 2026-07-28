#Requires -Version 5.1
<#
.SYNOPSIS
  Lint AI feature folder invariants (status, reviews, CR sync, REQ/AC warnings).

.PARAMETER FeatureId
  Feature slug, e.g. fishing-flow-ab-test

.PARAMETER Strict
  Treat warnings as errors (exit 1).

.EXAMPLE
  .\lint-feature.ps1 -FeatureId fishing-flow-ab-test
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $FeatureId,

    [switch] $Strict
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..')
$featureRoot = Join-Path $repoRoot "AI\features\$FeatureId"

$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Add-Error([string]$Message) { $errors.Add($Message) }
function Add-Warning([string]$Message) { $warnings.Add($Message) }

function Get-PaddedTaskId([string]$Id) {
    $n = [int]$Id
    return '{0:D2}' -f $n
}

function Read-StatusYaml([string]$Path) {
    $lines = Get-Content -Path $Path
    $result = @{
        last_processed_change = 'none'
        tasks               = @()
    }

    $current = $null
    foreach ($line in $lines) {
        if ($line -match '^last_processed_change:\s*(.+)$') {
            $result.last_processed_change = $Matches[1].Trim().Trim('"').Trim("'")
            continue
        }
        if ($line -match '^\s+-\s+id:\s*"?([^"\s]+)"?') {
            if ($current) { $result.tasks += $current }
            $current = [pscustomobject]@{ id = $Matches[1]; type = 'code'; status = 'pending'; review = $null }
            continue
        }
        if ($null -eq $current) { continue }
        if ($line -match '^\s+type:\s*(\S+)') { $current.type = $Matches[1]; continue }
        if ($line -match '^\s+status:\s*(\S+)') { $current.status = $Matches[1]; continue }
        if ($line -match '^\s+review:\s*(\S+)') { $current.review = $Matches[1]; continue }
    }
    if ($current) { $result.tasks += $current }

    return $result
}

function Get-SpecIds([string]$Path, [string]$Pattern) {
    if (-not (Test-Path $Path)) { return @() }
    $content = Get-Content -Path $Path -Raw
    $matches = [regex]::Matches($content, $Pattern)
    $ids = foreach ($m in $matches) { $m.Groups[1].Value }
    return $ids | Sort-Object -Unique
}

function Get-TaskTraceabilityReqs([string]$TaskPath) {
    if (-not (Test-Path $TaskPath)) { return @() }
    $content = Get-Content -Path $TaskPath -Raw
    if ($content -notmatch '(?ms)## Traceability\s*\r?\n.*?\*\*requirements:\*\*\s*([^\r\n]+)') {
        return @()
    }
    $line = $Matches[1]
    $reqs = [regex]::Matches($line, 'REQ-\d+') | ForEach-Object { $_.Value }
    return $reqs
}

# --- Feature root ---
if (-not (Test-Path $featureRoot)) {
    Add-Error "Feature folder not found: AI/features/$FeatureId"
    Write-Host "FAIL" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  ERROR: $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Lint: $FeatureId ($featureRoot)" -ForegroundColor Cyan

# --- Required files ---
foreach ($file in @('spec.md', 'plan.md', 'status.yaml', 'feature.yaml')) {
    $p = Join-Path $featureRoot $file
    if (-not (Test-Path $p)) { Add-Error "Missing required file: $file" }
}

$statusPath = Join-Path $featureRoot 'status.yaml'
$specPath = Join-Path $featureRoot 'spec.md'
$tasksDir = Join-Path $featureRoot 'tasks'
$reviewsDir = Join-Path $featureRoot 'reviews'
$decisionsDir = Join-Path $featureRoot 'decisions'

if (-not (Test-Path $statusPath)) {
    Write-Host "FAIL" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  ERROR: $_" -ForegroundColor Red }
    exit 1
}

$status = Read-StatusYaml $statusPath

# --- Task files <-> status.yaml ---
$taskFiles = @()
if (Test-Path $tasksDir) {
    $taskFiles = Get-ChildItem -Path $tasksDir -Filter 'TASK_*.md' -File | ForEach-Object {
        if ($_.Name -match 'TASK_(\d+)\.md') {
            [pscustomobject]@{ id = $Matches[1]; path = $_.FullName; name = $_.Name }
        }
    }
}

$statusIds = $status.tasks | ForEach-Object { [string]$_.id }

foreach ($tf in $taskFiles) {
    $normalized = [string]([int]$tf.id)
    if ($statusIds -notcontains $tf.id -and $statusIds -notcontains $normalized) {
        Add-Error "Task file $($tf.name) has no entry in status.yaml"
    }
}

foreach ($st in $status.tasks) {
    $padded = Get-PaddedTaskId $st.id
    $taskPath = Join-Path $tasksDir "TASK_$padded.md"
    if (-not (Test-Path $taskPath)) {
        Add-Error "status.yaml task $($st.id) missing file tasks/TASK_$padded.md"
    }

    if ($st.type -eq 'code' -and $st.status -eq 'done') {
        $reviewRel = if ($st.review) { $st.review } else { "reviews/REVIEW_TASK_$padded.md" }
        $reviewPath = Join-Path $featureRoot ($reviewRel -replace '/', '\')
        if (-not (Test-Path $reviewPath)) {
            Add-Error "Task $($st.id) is done but review missing: $reviewRel"
        }
    }
}

# --- Change request sync ---
if (Test-Path $decisionsDir) {
    $crFiles = Get-ChildItem -Path $decisionsDir -Filter 'CR-*.md' -File | Sort-Object Name
    if ($crFiles.Count -gt 0) {
        $latestCr = [System.IO.Path]::GetFileNameWithoutExtension($crFiles[-1].Name)
        $processed = $status.last_processed_change
        if ($processed -ne $latestCr -and $processed -ne 'none') {
            # If processed doesn't match latest, might be behind
            $processedIndex = -1
            $latestIndex = -1
            for ($i = 0; $i -lt $crFiles.Count; $i++) {
                $base = [System.IO.Path]::GetFileNameWithoutExtension($crFiles[$i].Name)
                if ($base -eq $processed) { $processedIndex = $i }
                if ($base -eq $latestCr) { $latestIndex = $i }
            }
            if ($processedIndex -ge 0 -and $latestIndex -gt $processedIndex) {
                Add-Error "last_processed_change is '$processed' but newer CR exists: $latestCr (run change-request pipeline or update status.yaml)"
            }
            elseif ($processedIndex -lt 0 -and $processed -ne 'none') {
                Add-Warning "last_processed_change '$processed' not found among decisions/CR-*.md files"
            }
        }
        elseif ($processed -eq 'none' -and $crFiles.Count -gt 0) {
            Add-Warning "decisions/ has CR files but last_processed_change is 'none'"
        }
    }
}

# --- REQ / AC warnings (phase 2, non-blocking) ---
$reqIds = Get-SpecIds $specPath 'REQ-(\d+)'
$acIds = Get-SpecIds $specPath 'AC-(\d+)'

if ($reqIds.Count -eq 0 -and (Test-Path $specPath)) {
    Add-Warning "spec.md has no REQ-* IDs (use AI/templates/spec.template.md)"
}
if ($acIds.Count -eq 0 -and (Test-Path $specPath)) {
    Add-Warning "spec.md has no AC-* IDs"
}

$referencedReqs = [System.Collections.Generic.HashSet[string]]::new()
foreach ($tf in $taskFiles) {
    foreach ($r in (Get-TaskTraceabilityReqs $tf.path)) { [void]$referencedReqs.Add($r) }
}

foreach ($req in $reqIds) {
    $reqFull = "REQ-$req"
    if (-not $referencedReqs.Contains($reqFull)) {
        Add-Warning "$reqFull not referenced in any task ## Traceability (optional for legacy tasks)"
    }
}

# --- Report ---
$hasErrors = $errors.Count -gt 0
$hasWarnings = $warnings.Count -gt 0

if ($hasErrors) {
    Write-Host "`nFAIL ($($errors.Count) error(s))" -ForegroundColor Red
    foreach ($e in $errors) { Write-Host "  ERROR: $e" -ForegroundColor Red }
}
else {
    Write-Host "`nPASS - structural checks OK" -ForegroundColor Green
}

if ($hasWarnings) {
    Write-Host "WARNINGS ($($warnings.Count)):" -ForegroundColor Yellow
    foreach ($w in $warnings) { Write-Host "  WARN: $w" -ForegroundColor Yellow }
}

if (-not $hasErrors -and -not $hasWarnings) {
    Write-Host "  Tasks: $($taskFiles.Count) files, $($status.tasks.Count) status entries"
    Write-Host "  REQs: $($reqIds.Count), ACs: $($acIds.Count)"
}

if ($hasErrors -or ($Strict -and $hasWarnings)) { exit 1 }
exit 0
