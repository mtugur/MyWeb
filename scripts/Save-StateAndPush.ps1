Param(
  [Parameter(Mandatory=$true)][string]$Message,
  [switch]$NoPush    # İstersen push'u kapatmak için: -NoPush
)

$ErrorActionPreference = "Stop"

function Info($m){ Write-Host "[INFO] $m" }
function Warn($m){ Write-Host "[WARN] $m" -ForegroundColor Yellow }
function Err ($m){ Write-Host "[ERR ] $m"  -ForegroundColor Red }

# === Kök ve klasörler ===
$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot  = Split-Path -Parent $ScriptDir
Set-Location $RepoRoot

$stateDir    = "$RepoRoot\_state"
$snapRoot    = "$stateDir\snapshots"
$exportDir   = "$RepoRoot\_export"
New-Item -ItemType Directory -Force -Path $stateDir  | Out-Null

# === 0) Razor/obj-temizliği (düzeltildi) ===
# -Filter tek değer alır, bu yüzden isim süzgecini Where-Object ile yapıyoruz.
Get-ChildItem -Path "$RepoRoot\src" -Recurse -Directory -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -in @('obj','bin') } |
  Remove-Item -Force -Recurse -ErrorAction SilentlyContinue

# === 1) Build (Release) ===
Info "dotnet build -c Release"
$buildOut = dotnet build .\MyWeb.sln -c Release 2>&1
$buildOut | Set-Content "$stateDir\BUILD.txt" -Encoding UTF8
if ($LASTEXITCODE -ne 0) { Err "Build FAILED ($LASTEXITCODE)"; throw "Build failed" }

# === 2) _state temizlik: kökte dağınık snapshot'ları sil ===
Info "Temizlik: kökteki *.snapshot.txt dosyaları siliniyor"
Get-ChildItem $stateDir -File -Filter *.snapshot.txt -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# snapshots/ klasörünü sıfırla
if (Test-Path $snapRoot) {
  Info "Temizlik: snapshots/ reset"
  Remove-Item $snapRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $snapRoot | Out-Null

# === 3) Önemli dosyaların snapshot'ları ===
$Important = @(
  # WEBAPP
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Program.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Infrastructure\AuthSetup.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\AccountController.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\HistoryUiController.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\Api\HistoryController.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Views\HistoryUi\Trend.cshtml",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\wwwroot\js\history-trend.js",

  # RUNTIME
  "$RepoRoot\src\Runtime\MyWeb.Runtime\ServiceCollectionExtensions.cs",
  "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\TagSamplingService.cs",
  "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\HistoryWriterService.cs",
  "$RepoRoot\src\Runtime\MyWeb.Runtime\Services\RetentionCleanerService.cs",

  # MODULES
  "$RepoRoot\src\Modules\Communication.Siemens\MyWeb.Communication.Siemens\SiemensCommunicationChannel.cs"
)

foreach($src in $Important){
  if (-not (Test-Path $src)) { continue }
  $rel     = $src.Replace($RepoRoot, '').TrimStart('\')
  $destDir = Join-Path $snapRoot (Split-Path $rel -Parent)
  New-Item -ItemType Directory -Force -Path $destDir | Out-Null
  $dest    = Join-Path $snapRoot ($rel + ".snapshot.txt")

  $header = @()
  $header += "===== SNAPSHOT: $rel ====="
  $header += (Get-Date -Format "yyyy-MM-dd HH:mm:ss 'UTC'zzz")
  $header += "----------------------------------------"
  $content = Get-Content $src -Raw
  ($header -join "`r`n") + "`r`n" + $content | Set-Content $dest -Encoding UTF8
}

# === 4) LOG kopyası (varsa) ===
$logDir = "$RepoRoot\src\WebApp\MyWeb.WebApp\Logs"
if (Test-Path $logDir) {
  $lastLog = Get-ChildItem $logDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($lastLog) { Copy-Item $lastLog.FullName "$stateDir\LOG_LASTRUN.txt" -Force }
}

# === 5) DB snapshot (auth/catalog/hist) ===
function Invoke-Sql {
  param([string]$Server,[string]$Db,[string]$Query)
  sqlcmd -S $Server -d $Db -E -l 5 -h -1 -W -Q $Query 2>$null
}

$DbName = "MyWeb"
$server = $null
foreach($s in @(".", "(localdb)\MSSQLLocalDB", "localhost,1433")){
  try{
    $r = Invoke-Sql -Server $s -Db "master" -Query "SELECT 1"
    if ($LASTEXITCODE -eq 0 -and $r -match '1'){ $server = $s; break }
  } catch {}
}

if ($server) {
  Info "SQL Server: $server / DB: $DbName"
  $dbInfo = @()
  $dbInfo += "SERVER=$server DB=$DbName"
  $dbInfo += "---- Şemalar ----"
  $dbInfo += (Invoke-Sql -Server $server -Db $DbName -Query "SELECT s.name FROM sys.schemas s ORDER BY s.name")
  $dbInfo += ""
  $dbInfo += "---- auth tabloları ----"
  $dbInfo += (Invoke-Sql -Server $server -Db $DbName -Query "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='auth' ORDER BY TABLE_NAME")
  $dbInfo += ""
  $dbInfo += "---- catalog tabloları ----"
  $dbInfo += (Invoke-Sql -Server $server -Db $DbName -Query "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='catalog' ORDER BY TABLE_NAME")
  $dbInfo += ""
  $dbInfo += "---- hist tabloları ----"
  $dbInfo += (Invoke-Sql -Server $server -Db $DbName -Query "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='hist' ORDER BY TABLE_NAME")
  $dbInfo += ""
  $dbInfo += "---- örnek veriler ----"
  $dbInfo += "[auth.Users] TOP 5:"
  $dbInfo += (Invoke-Sql -Server $server -Db $DbName -Query "SELECT TOP 5 Id,UserName,Email,EmailConfirmed,AccessFailedCount FROM auth.Users ORDER BY Id")
  $dbInfo += ""
  $dbInfo += "[catalog.Projects] TOP 5:"
  $dbInfo += (Invoke-Sql -Server $server -Db $DbName -Query "SELECT TOP 5 Id,Key,Name FROM catalog.Projects ORDER BY Id")
  $dbInfo += ""
  $dbInfo += "[catalog.Tags] TOP 5:"
  $dbInfo += (Invoke-Sql -Server $server -Db $DbName -Query "SELECT TOP 5 Id,ProjectId,Path,Name,DataType FROM catalog.Tags ORDER BY Path")

  Set-Content -Path "$stateDir\DB_SCHEMA.txt" -Value $dbInfo -Encoding UTF8
}
else {
  Warn "SQL Server bulunamadı; DB snapshot atlandı."
}

# === 6) _export üret ===
Info "emit-project-files.ps1 çalıştırılıyor"
powershell -ExecutionPolicy Bypass -File ".\scripts\emit-project-files.ps1" -RepoRoot $RepoRoot -OutDir $exportDir | Out-Null

# === 7) Özet / STATUS ===
$branch = (git rev-parse --abbrev-ref HEAD 2>$null)
$sha    = (git rev-parse --short HEAD 2>$null)

$state = @()
$state += "STATE SNAPSHOT"
$state += "Date   : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
$state += "Branch : $branch"
$state += "SHA    : $sha"
$state += ""
$state += "Build  : PASS"
$state += "Export : $exportDir"
Set-Content "$stateDir\_STATE_SNAPSHOT.txt" -Value $state -Encoding UTF8

$stat = @()
$stat += "Aşama: Stage-1 (Auth/RBAC + UI/API)"
$stat += "Build: PASS"
$stat += "Notes: snapshots klasörü temiz ve güncel."
Set-Content "$stateDir\STATUS.txt" -Value $stat -Encoding UTF8

# === 8) Git ===
Info "git add"
git add -A

Info "git commit"
git commit -m $Message | Out-Null

if (-not $NoPush) {
  Info "git push"
  git push
} else {
  Warn "git push atlandı (-NoPush)"
}

Write-Host ""
Write-Host "==== ÖZET ===="
Write-Host "_state  : düzenlendi (snapshots reset)"
Write-Host "_export : üretildi"
Write-Host "git     : commit $(if($NoPush){'(no push)'}else{'& push'})"
