<#  emits _export bundle (PS 5.1 compatible)
    - Removes _export then recreates
    - Gathers key files into sectioned text docs
    - Writes 90-INDEX-MANIFEST.json with metadata
#>

Param(
  [string]$RepoRoot = $(Split-Path -Parent $PSScriptRoot),
  [string]$OutDir   = $null
)

$ErrorActionPreference = 'Stop'
function Info($m){ Write-Host "[emit] $m" }

if (-not $OutDir) { $OutDir = Join-Path $RepoRoot "_export" }

# fresh _export
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# helpers
function Write-TextFile {
  param([string]$Path,[string[]]$Lines)
  $dir = Split-Path -Parent $Path
  New-Item -ItemType Directory -Force -Path $dir | Out-Null
  Set-Content -Path $Path -Value $Lines -Encoding UTF8
}
function Read-TextOrEmpty {
  param([string]$Path)
  if (Test-Path $Path) { Get-Content $Path -Raw } else { "" }
}
function Section {
  param([string]$Title,[string]$Body)
  return ("===== " + $Title + " =====`r`n" + $Body + "`r`n")
}
function Rel { param([string]$p) return $p.Replace($RepoRoot,'').TrimStart('\') }

# git meta (opsiyonel)
$gitBranch = (git rev-parse --abbrev-ref HEAD 2>$null)
$gitSha    = (git rev-parse --short HEAD 2>$null)

# discover webapp urls from launchSettings
$launchPath = Join-Path $RepoRoot "src\WebApp\MyWeb.WebApp\Properties\launchSettings.json"
$swaggerHttp  = ""
$swaggerHttps = ""
if (Test-Path $launchPath) {
  $ls = Get-Content $launchPath -Raw | ConvertFrom-Json
  $profiles = $ls.PSObject.Properties | ForEach-Object { $_.Value }
  foreach($p in $profiles){
    if ($p.applicationUrl) {
      $urls = $p.applicationUrl -split ';'
      foreach($u in $urls){
        if ($u -match '^https://') { $swaggerHttps = $u }
        if ($u -match '^http://')  { $swaggerHttp  = $u }
      }
    }
  }
}

# read appsettings for conn strings
$appDev = Join-Path $RepoRoot "src\WebApp\MyWeb.WebApp\appsettings.Development.json"
$appPrd = Join-Path $RepoRoot "src\WebApp\MyWeb.WebApp\appsettings.json"
$cfg = $null
if (Test-Path $appDev) { $cfg = Get-Content $appDev -Raw | ConvertFrom-Json }
elseif (Test-Path $appPrd) { $cfg = Get-Content $appPrd -Raw | ConvertFrom-Json }

$histConn = ""
$catConn  = ""
if ($cfg -and $cfg.ConnectionStrings) {
  if ($cfg.ConnectionStrings.HistorianDb) { $histConn = $cfg.ConnectionStrings.HistorianDb }
  if ($cfg.ConnectionStrings.CatalogDb)   { $catConn  = $cfg.ConnectionStrings.CatalogDb }
}

# ----------------------------------------------------------------------------------------------------------------------
# 00 – TALIMATLAR (kısa referans)
$T00 = @()
$T00 += "MyWeb — SCADA/MES (Stage-1: Auth/RBAC + Trend UI)"
$T00 += "Bu klasör, AI’ye ve ekip içi incelemeye yönelik *_export* özet dosyalarını içerir."
$T00 += ""
$T00 += "Ritüel:"
$T00 += "  scripts\emit-project-files.ps1         # export’u güncelle"
$T00 += "  scripts\Save-StateAndPush.ps1 -Message \"...\"   # state+export+git"
$T00 += ""
$T00 += "Kilometre taşları: Stage-1 kilitli (Identity/RBAC + Basic Trend)."
Write-TextFile -Path (Join-Path $OutDir "00-PROJE-TALIMATLARI.txt") -Lines $T00

# ----------------------------------------------------------------------------------------------------------------------
# 01 – PROJE ÖZETİ (ağaç + önemli dosyalar)
$tree = & cmd /c "tree `"$RepoRoot\src`" /F" 2>$null
$T01 = @()
$T01 += "== PROJE AĞACI (src) =="
$T01 += $tree
$T01 += ""
$T01 += "== ÖNEMLİ DOSYALAR =="
$T01 += @(
  "WebApp\Program.cs",
  "WebApp\Infrastructure\AuthSetup.cs",
  "WebApp\Controllers\AccountController.cs",
  "WebApp\Controllers\HistoryUiController.cs",
  "WebApp\Controllers\Api\HistoryController.cs",
  "WebApp\Views\HistoryUi\Trend.cshtml",
  "WebApp\wwwroot\js\history-trend.js",
  "Infrastructure\Data\Identity\IdentityDbContext.cs",
  "Infrastructure\Data\Identity\PermissionModels.cs",
  "Runtime\ServiceCollectionExtensions.cs",
  "Runtime\Services\*.cs",
  "Modules\Communication.Siemens\...\SiemensCommunicationChannel.cs"
)
Write-TextFile -Path (Join-Path $OutDir "01-PROJE-OZETI.txt") -Lines $T01

# ----------------------------------------------------------------------------------------------------------------------
# 02 – SABITLER (connstr + swagger)
$T02 = @()
$T02 += "DB Name    : MyWeb"
$T02 += "Schemas    : catalog, hist, auth"
$T02 += ""
$T02 += "ConnStrings:"
$T02 += "  HistorianDb : $histConn"
$T02 += "  CatalogDb   : $catConn"
$T02 += ""
if ($swaggerHttp -ne "") {
  $T02 += ("Swagger(HTTP) : {0}" -f ($swaggerHttp + "/swagger"))
} else {
  $T02 += "Swagger(HTTP) : n/a"
}
if ($swaggerHttps -ne "") {
  $T02 += ("Swagger(HTTPS): {0}" -f ($swaggerHttps + "/swagger"))
} else {
  $T02 += "Swagger(HTTPS): n/a"
}
Write-TextFile -Path (Join-Path $OutDir "02-PROJE-SABITLERI.txt") -Lines $T02

# ----------------------------------------------------------------------------------------------------------------------
# 10 – CORE (seçilmiş)
$T10 = ""
$T10 += Section (Rel "$RepoRoot\src\Core\MyWeb.Core\MyWeb.Core.csproj") (Read-TextOrEmpty "$RepoRoot\src\Core\MyWeb.Core\MyWeb.Core.csproj")
Write-TextFile -Path (Join-Path $OutDir "10-CORE_ICERIGI.txt") -Lines $T10

# ----------------------------------------------------------------------------------------------------------------------
# 20 – INFRASTRUCTURE
$T20 = ""
$T20 += Section (Rel "$RepoRoot\src\Infrastructure\MyWeb.Infrastructure.Data\Identity\IdentityDbContext.cs") (Read-TextOrEmpty "$RepoRoot\src\Infrastructure\MyWeb.Infrastructure.Data\Identity\IdentityDbContext.cs")
$T20 += Section (Rel "$RepoRoot\src\Infrastructure\MyWeb.Infrastructure.Data\Identity\PermissionModels.cs") (Read-TextOrEmpty "$RepoRoot\src\Infrastructure\MyWeb.Infrastructure.Data\Identity\PermissionModels.cs")
Write-TextFile -Path (Join-Path $OutDir "20-INFRASTRUCTURE_ICERIGI.txt") -Lines $T20

# ----------------------------------------------------------------------------------------------------------------------
# 30 – MODULES
$T30 = ""
$T30 += Section (Rel "$RepoRoot\src\Modules\Communication.Siemens\MyWeb.Communication.Siemens\SiemensCommunicationChannel.cs") (Read-TextOrEmpty "$RepoRoot\src\Modules\Communication.Siemens\MyWeb.Communication.Siemens\SiemensCommunicationChannel.cs")
Write-TextFile -Path (Join-Path $OutDir "30-MODULES_ICERIGI.txt") -Lines $T30

# ----------------------------------------------------------------------------------------------------------------------
# 40 – RUNTIME
$T40 = ""
$T40 += Section (Rel "$RepoRoot\src\Runtime\MyWeb.Runtime\ServiceCollectionExtensions.cs") (Read-TextOrEmpty "$RepoRoot\src\Runtime\MyWeb.Runtime\ServiceCollectionExtensions.cs")
$T40 += Section (Rel "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\TagSamplingService.cs") (Read-TextOrEmpty "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\TagSamplingService.cs")
$T40 += Section (Rel "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\HistoryWriterService.cs") (Read-TextOrEmpty "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\HistoryWriterService.cs")
$T40 += Section (Rel "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\RetentionCleanerService.cs") (Read-TextOrEmpty "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\RetentionCleanerService.cs")
Write-TextFile -Path (Join-Path $OutDir "40-RUNTIME_ICERIGI.txt") -Lines $T40

# ----------------------------------------------------------------------------------------------------------------------
# 50 – WEBAPP
$T50 = ""
$T50 += Section (Rel "$RepoRoot\src\WebApp\MyWeb.WebApp\Program.cs") (Read-TextOrEmpty "$RepoRoot\src\WebApp\MyWeb.WebApp\Program.cs")
$T50 += Section (Rel "$RepoRoot\src\WebApp\MyWeb.WebApp\Infrastructure\AuthSetup.cs") (Read-TextOrEmpty "$RepoRoot\src\WebApp\MyWeb.WebApp\Infrastructure\AuthSetup.cs")
$T50 += Section (Rel "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\AccountController.cs") (Read-TextOrEmpty "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\AccountController.cs")
$T50 += Section (Rel "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\HistoryUiController.cs") (Read-TextOrEmpty "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\HistoryUiController.cs")
$T50 += Section (Rel "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\Api\HistoryController.cs") (Read-TextOrEmpty "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\Api\HistoryController.cs")
$T50 += Section (Rel "$RepoRoot\src\WebApp\MyWeb.WebApp\Views\HistoryUi\Trend.cshtml") (Read-TextOrEmpty "$RepoRoot\src\WebApp\MyWeb.WebApp\Views\HistoryUi\Trend.cshtml")
$T50 += Section (Rel "$RepoRoot\src\WebApp\MyWeb.WebApp\wwwroot\js\history-trend.js") (Read-TextOrEmpty "$RepoRoot\src\WebApp\MyWeb.WebApp\wwwroot\js\history-trend.js")
Write-TextFile -Path (Join-Path $OutDir "50-WEBAPP_ICERIGI.txt") -Lines $T50

# ----------------------------------------------------------------------------------------------------------------------
# 90 – INDEX MANIFEST
$manifest = [ordered]@{
  generatedAt = (Get-Date).ToString("s")
  repoRoot    = $RepoRoot
  outDir      = $OutDir
  gitBranch   = $gitBranch
  gitSha      = $gitSha
  swagger     = @{
    http  = $swaggerHttp
    https = $swaggerHttps
  }
  files = @(
    "00-PROJE-TALIMATLARI.txt",
    "01-PROJE-OZETI.txt",
    "02-PROJE-SABITLERI.txt",
    "10-CORE_ICERIGI.txt",
    "20-INFRASTRUCTURE_ICERIGI.txt",
    "30-MODULES_ICERIGI.txt",
    "40-RUNTIME_ICERIGI.txt",
    "50-WEBAPP_ICERIGI.txt"
  )
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutDir "90-INDEX-MANIFEST.json") -Encoding UTF8

# small convenience: state snapshot headline
$stateSnap = @(
  "EXPORT GENERATED",
  "Time   : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"),
  "OutDir : " + $OutDir,
  "Branch : " + $gitBranch,
  "SHA    : " + $gitSha
)
Write-TextFile -Path (Join-Path $OutDir "_STATE_SNAPSHOT.txt") -Lines $stateSnap

Info "done -> $OutDir"
