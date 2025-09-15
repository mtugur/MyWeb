Param(
  [Parameter(Mandatory=$true)]
  [string]$Message
)

$ErrorActionPreference = "Stop"

function Info($m){ Write-Host "[INFO] $m" }
function Warn($m){ Write-Host "[WARN] $m" -ForegroundColor Yellow }
function Err ($m){ Write-Host "[ERR ] $m"  -ForegroundColor Red }

# Repo kökü = bu scriptin üst klasörü
$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot  = Split-Path -Parent $ScriptDir
Set-Location $RepoRoot
Info "Çalışma dizini: $RepoRoot"

# 1) Build (Release)
Info "dotnet build (Release) başlıyor…"
dotnet build .\src\WebApp\MyWeb.WebApp\MyWeb.WebApp.csproj -c Release | Out-Null
Info "dotnet build OK."

# 2) Git bilgisi (fail olsa da devam)
$branch = "<unknown>"
$sha    = "<unknown>"
try {
  $branch = $(git rev-parse --abbrev-ref HEAD).Trim()
  $sha    = $(git rev-parse --short HEAD).Trim()
} catch {
  Warn "git bilgileri alınamadı: git  hata:"
}

# 3) _state klasörünü hazırla
$state = Join-Path $RepoRoot "_state"
if (-not (Test-Path $state)) { New-Item -ItemType Directory -Path $state | Out-Null }

# 4) DB şema + örnek veri
Info "DB şema ve örnek veri çekiliyor (localhost/MyWeb)…"
$schemaSql = @"
EXEC sp_help 'hist.Samples';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='hist' AND TABLE_NAME='Samples';
SELECT TOP(5) Id, ProjectId, TagId, Utc, ValueNumeric, ValueText, ValueBool, Quality, Source
FROM hist.Samples
ORDER BY Id DESC;
"@
$sqlFile = Join-Path $state "DB_SCHEMA.txt"
$sqlcmd = "sqlcmd -S localhost -d MyWeb -W -w 512 -Q ""$($schemaSql.Replace('"','\"'))"""
cmd /c $sqlcmd > $sqlFile 2>&1

# 5) Son log kopyası (varsa)
$logDir = Join-Path $RepoRoot "src\WebApp\MyWeb.WebApp\Logs"
if (Test-Path $logDir) {
  $latest = Get-ChildItem -Path $logDir -Filter "app-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($latest) {
    $dst = Join-Path $state "LOG_LASTRUN.txt"
    Copy-Item $latest.FullName $dst -Force
    Info "Log kopyalanıyor: $($latest.Name)"
  }
}

# 6) Snapshot seti
$snapList = @(
  "src\WebApp\MyWeb.WebApp\Program.cs",
  "src\Runtime\MyWeb.Runtime\ServiceCollectionExtensions.cs",
  "src\Runtime\MyWeb.Runtime\Services\TagSamplingService.cs",
  "src\Runtime\MyWeb.Runtime\History\HistoryWriterService.cs",
  "src\Runtime\MyWeb.Runtime\Services\RetentionCleanerService.cs",
  "src\Modules\Communication.Siemens\MyWeb.Communication.Siemens\SiemensCommunicationChannel.cs",
  "src\WebApp\MyWeb.WebApp\Controllers\HistoryUiController.cs",
  "src\WebApp\MyWeb.WebApp\Views\HistoryUi\Trend.cshtml",
  "src\WebApp\MyWeb.WebApp\wwwroot\js\history-trend.js",
  "src\WebApp\MyWeb.WebApp\appsettings.json",
  "src\WebApp\MyWeb.WebApp\appsettings.Development.json"
)
foreach($p in $snapList){
  $src = Join-Path $RepoRoot $p
  if(Test-Path $src){
    $safe = ($p -replace '[\\/:*?"<>| ]','_')
    $dst = Join-Path $state "$safe.snapshot.txt"
    Copy-Item $src $dst -Force
    Info "Snapshot: $p -> $((Split-Path -Leaf $dst))"
  }
}

# 7) _export üret (projeyi düz metin olarak dışa aktar)
Info "emit-project-files.ps1 çalıştırılıyor…"
& "$ScriptDir\emit-project-files.ps1"

# 8) DEMO paket tek yerden üret: .\_packages
$rootPackages = Join-Path $RepoRoot "_packages"
if (-not (Test-Path $rootPackages)) {
  New-Item -ItemType Directory -Path $rootPackages | Out-Null
}

# (Eski) scripts\_packages varsa taşı ve sil
$oldPkg = Join-Path $ScriptDir "_packages"
if (Test-Path $oldPkg) {
  Get-ChildItem $oldPkg -File | ForEach-Object {
    Move-Item -Force $_.FullName (Join-Path $rootPackages $_.Name)
  }
  Remove-Item -Recurse -Force $oldPkg
}

Info "make-demo-pkg.ps1 çalıştırılıyor…"
& "$ScriptDir\make-demo-pkg.ps1" -OutDir $rootPackages

# 9) _state meta
Set-Content -Path (Join-Path $state "BUILD.txt")     -Value "Release build: OK" -Encoding UTF8
Set-Content -Path (Join-Path $state "STATUS.txt")    -Value "Branch=$branch, SHA=$sha, When=$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8
Set-Content -Path (Join-Path $state "GIT_BRANCH.txt")-Value $branch -Encoding UTF8
Set-Content -Path (Join-Path $state "GIT_SHA.txt")   -Value $sha    -Encoding UTF8

# 10) Git add/commit/push
Info "git add ."
git add .

$stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$commitMsg = "[STATE] $Message | $stamp"
Info "git commit -m `"$commitMsg`""
git commit -m "$commitMsg" | Out-Null

Info "git push"
git push

# 11) Özet
Write-Host ""
Write-Host "==== ÖZET ===="
Write-Host "Branch : $branch"
Write-Host "SHA    : $sha"
Write-Host "_state  : güncellendi"
Write-Host "_export : güncellendi"
Write-Host "Paket  : $rootPackages"
Write-Host "Git    : push OK"
