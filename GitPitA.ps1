# GitPitA.ps1
<#
Usage:
  .\GitPitA.ps1 -RepoPath "W:\Projects\CSharp\Conquest\Conversion" -RemoteUrl "https://github.com/<YOUR-USER>/Conquest-Neo.git"

Notes:
- If the remote repo doesn't exist yet on GitHub, create it first (empty) in your account, then run this.
- Safe to re-run: it won't clobber existing commits or overwrite .gitignore if it already exists.
#>

param(
  [Parameter(Mandatory=$true)]
  [string]$RepoPath,

  [Parameter(Mandatory=$true)]
  [string]$RemoteUrl,

  [string]$DefaultBranch = "main"
)

function Write-Note($msg) { Write-Host $msg -ForegroundColor Cyan }
function Write-Do($msg)   { Write-Host $msg -ForegroundColor Yellow }
function Write-Ok($msg)   { Write-Host $msg -ForegroundColor Green }
function Write-Warn($msg) { Write-Host $msg -ForegroundColor Magenta }

# 1) Move to repo path
if (-not (Test-Path $RepoPath)) { throw "RepoPath not found: $RepoPath" }
Set-Location $RepoPath

# 2) Trust this directory for Git (fixes 'dubious ownership')
Write-Do "Adding to git safe.directory: $RepoPath"
git config --global --add safe.directory ($RepoPath -replace '\\','/') | Out-Null

# 3) Initialize if needed
$gitDir = (& git rev-parse --git-dir 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gitDir)) {
  Write-Do "git init"
  git init | Out-Null
} else {
  Write-Note "Git repo already initialized."
}

# 4) Create a .gitignore if missing (keeps repo clean)
$ignorePath = Join-Path $RepoPath ".gitignore"
if (-not (Test-Path $ignorePath)) {
    Write-Do "Creating .gitignore (modern .NET / Visual Studio)"
@"
# Build output
[Bb]in/
[Oo]bj/
[Ll]og/
*.log

# Visual Studio
.vs/
*.user
*.suo
*.userosscache
*.sln.docstates

# Test results
TestResult*/
*.trx

# Perf / profiling
*.psess
*.vsp
*.vspx

# Rider / VS Code (optional to ignore)
.idea/
.vscode/

# NuGet
*.nupkg
project.lock.json
artifacts/

# Others
_ReSharper*/
*.opendb
"@ | Set-Content -Encoding UTF8 $ignorePath
    git add .gitignore
    git commit -m "chore: add .gitignore" | Out-Null
} else {
    Write-Note ".gitignore already exists; leaving it as-is."
}

# 5) Ensure at least one commit (baseline) if repo has no commits
git rev-parse HEAD 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
  Write-Do "Committing existing source as baseline"
  git add -A
  git commit -m "baseline: initial import of Conquest-Neo source" | Out-Null
} else {
  # If there are staged/unstaged changes, commit them
  $status = git status --porcelain
  if ($status) {
    Write-Do "Committing current changes"
    git add -A
    git commit -m "chore: sync working tree before first push" | Out-Null
  } else {
    Write-Note "Working tree clean; no new changes to commit."
  }
}

# 6) Set default branch
Write-Do "Setting default branch to $DefaultBranch"
git branch -M $DefaultBranch | Out-Null

# 7) Add or update remote 'origin'
$hasOrigin = git remote | Select-String -SimpleMatch "origin"
if ($hasOrigin) {
  Write-Do "Updating remote 'origin' -> $RemoteUrl"
  git remote set-url origin $RemoteUrl
} else {
  Write-Do "Adding remote 'origin' -> $RemoteUrl"
  git remote add origin $RemoteUrl
}

# 8) Push
Write-Do "Pushing to $RemoteUrl ($DefaultBranch)"
git push -u origin $DefaultBranch

if ($LASTEXITCODE -eq 0) {
  Write-Ok "All set! Remote is linked and your code is on GitHub."
  Write-Ok "Remote: $(git remote -v)"
} else {
  Write-Warn "Push failed. Common causes:"
  Write-Warn " - Remote repo doesn't exist yet (create it on GitHub first)"
  Write-Warn " - Not signed in / insufficient permissions"
  Write-Warn " - 2FA/token issues if using HTTPS without credential manager"
}
