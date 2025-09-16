param(
  [string]$RepoRoot = (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
)

$ErrorActionPreference = "Stop"

$pkgRoot = "$RepoRoot\_packages"
$outDir  = "$RepoRoot\_export"
New-Item -ItemType Directory -Force -Path $pkgRoot | Out-Null

# _export yoksa üret
if (-not (Test-Path $outDir)) {
  powershell -ExecutionPolicy Bypass -File ".\scripts\emit-project-files.ps1" -RepoRoot $RepoRoot -OutDir $outDir | Out-Null
}

# Proje/Tag snapshot (demo)
$projJson = "$pkgRoot\projects.json"
$tagsJson = "$pkgRoot\tags.json"

# Basit JSON: WebApp endpoints’ı tüketen bir istemci için minimal metadata
$projects = @()
$tags     = @()

try {
  $cs = "Server=.;Database=MyWeb;Trusted_Connection=True;Encrypt=False"
  $projects = sqlcmd -S "." -d "MyWeb" -E -l 5 -W -h -1 `
      -Q "SELECT Id, Key, Name FROM catalog.Projects ORDER BY Id" 2>$null | 
      ForEach-Object { if($_){ $p=$_ -split '\s{2,}'; [pscustomobject]@{ Id=$p[0]; Key=$p[1]; Name=($p[2..($p.length-1)] -join ' ') } }
  $tags = sqlcmd -S "." -d "MyWeb" -E -l 5 -W -h -1 `
      -Q "SELECT TOP 500 Id, ProjectId, Path, Name, DataType FROM catalog.Tags ORDER BY Path" 2>$null | 
      ForEach-Object { if($_){ $t=$_ -split '\s{2,}'; [pscustomobject]@{ Id=$t[0]; ProjectId=$t[1]; Path=$t[2]; Name=$t[3]; DataType=$t[4] } }
} catch { }

$projects | ConvertTo-Json | Set-Content $projJson -Encoding UTF8
$tags     | ConvertTo-Json | Set-Content $tagsJson -Encoding UTF8

# Manifest
$manifest = [ordered]@{
  name        = "MyWeb-Stage1-Demo"
  generatedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
  endpoints   = @{
    whoami   = "/api/hist/whoami"
    projects = "/api/hist/projects"
    tags     = "/api/hist/tags?projectId={id}"
    samples  = "/api/hist/samples?tagId={id}&from={iso}&to={iso}&take=1000"
  }
  auth        = @{
    type = "Cookie"
    loginUrl = "/account/login"
    policy = "CanUseHistorian"
    roles  = @("Admin","Operator")
  }
  files = @(
    "_export/01-PROJE-OZETI.txt",
    "_export/02-PROJE-SABITLERI.txt",
    "_export/10-CORE_ICERIGI.txt",
    "_export/20-INFRASTRUCTURE_ICERIGI.txt",
    "_export/30-MODULES_ICERIGI.txt",
    "_export/40-RUNTIME_ICERIGI.txt",
    "_export/50-WEBAPP_ICERIGI.txt",
    "_export/90-INDEX-MANIFEST.json",
    "projects.json",
    "tags.json"
  )
}
$manifestPath = "$pkgRoot\manifest.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding UTF8

# Paketle
$zipPath = "$pkgRoot\MyWeb-Stage1-Demo.mywebpkg"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem

$temp = Join-Path $pkgRoot "_tmp_pkg"
if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
New-Item -ItemType Directory -Path $temp | Out-Null

Copy-Item -Recurse -Force $outDir "$temp\_export"
Copy-Item $projJson  "$temp\projects.json"
Copy-Item $tagsJson  "$temp\tags.json"
Copy-Item $manifestPath "$temp\manifest.json"

[System.IO.Compression.ZipFile]::CreateFromDirectory($temp, $zipPath)
Remove-Item $temp -Recurse -Force

Write-Host "[OK] Demo paket üretildi: $zipPath"
