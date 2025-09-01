# Create.ps1  (run from W:\Projects\CSharp\Conquest\Conversion)

$ErrorActionPreference = 'Stop'

# 1) Solution + projects
if (-not (Test-Path .\Conquest.sln)) { dotnet new sln -n Conquest }
if (-not (Test-Path .\Conquest.Core\Conquest.Core.csproj)) { dotnet new classlib -n Conquest.Core -f net8.0 }
if (-not (Test-Path .\Conquest.CLI\Conquest.CLI.csproj))  { dotnet new console  -n Conquest.CLI  -f net8.0 }
if (-not (Test-Path .\Conquest.Tests\Conquest.Tests.csproj)) { dotnet new xunit -n Conquest.Tests -f net8.0 }

# 2) Add to solution (idempotent)
dotnet sln .\Conquest.sln add .\Conquest.Core\Conquest.Core.csproj 2>$null
dotnet sln .\Conquest.sln add .\Conquest.CLI\Conquest.CLI.csproj  2>$null
dotnet sln .\Conquest.sln add .\Conquest.Tests\Conquest.Tests.csproj 2>$null

# 3) Add references (idempotent)
dotnet add .\Conquest.CLI\Conquest.CLI.csproj reference .\Conquest.Core\Conquest.Core.csproj 2>$null
dotnet add .\Conquest.Tests\Conquest.Tests.csproj reference .\Conquest.Core\Conquest.Core.csproj 2>$null

# 4) Show summary
Write-Host "`n=== Solution contents ==="
dotnet sln .\Conquest.sln list
Write-Host "`n=== CLI references ==="
dotnet list .\Conquest.CLI\Conquest.CLI.csproj reference
Write-Host "`n=== Tests references ==="
dotnet list .\Conquest.Tests\Conquest.Tests.csproj reference
