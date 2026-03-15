# Apply-Patch.ps1
param(
  [Parameter(Mandatory=$true)][string]$Patch,   # e.g. .\zero_based.patch
  [string]$Branch = "",                         # optional: create/switch first
  [switch]$ThreeWay,                            # try --3way merge
  [switch]$Commit                               # auto-commit if applied
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path .git)) { throw "Run from your repo root ('.git' not found)." }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "Git not in PATH." }
if (-not (Test-Path $Patch)) { throw "Patch not found: $Patch" }

if ($Branch) {
  $exists = (git branch --list $Branch) -ne ""
  if ($exists) { git checkout $Branch | Out-Null } else { git checkout -b $Branch | Out-Null }
}

# Dry-run: will throw if it can't apply
git apply --check --index -- "$Patch"

# Apply for real
$args = @("apply","--index","--whitespace=fix","--")
if ($ThreeWay) { $args = @("apply","--3way","--index","--whitespace=fix","--") }
$args += $Patch
git @args

Write-Host "✔ Patch applied."

if ($Commit) {
  git add -A
  if ((git diff --cached --name-only)) {
    git commit -m "Apply patch: $(Split-Path $Patch -Leaf)" | Out-Null
    Write-Host "✔ Changes committed."
  } else {
    Write-Host "ℹ Nothing to commit."
  }
}
