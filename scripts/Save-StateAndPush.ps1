<#  Save-StateAndPush.ps1
    Tek komutla:
      1) dotnet build (Release)
      2) _state klasörünü güncelle
      3) _export klasörünü güncelle (emit-project-files.ps1)
      4) (opsiyonel) demo paketini güncelle (make-demo-pkg.ps1 varsa)
      5) git add/commit/push

    Kullanım (repo kökünde):
      powershell -ExecutionPolicy Bypass -File .\scripts\Save-StateAndPush.ps1 -Message "S3: Trend UI tamam"

    Notlar:
    - Invoke-Sqlcmd yoksa DB şema/örnek veri kısmını atlar (uyarı yazar).
    - sqlcmd/Invoke-Sqlcmd kullanmadan da çalışır (opsiyonel).
#>

[CmdletBinding()]
param(
  [string]$Message = "auto: state snapshot + export + push",
  [string]$Configuration = "Release",
  [string]$WebAppCsproj = ".\src\WebApp\MyWeb.WebApp\MyWeb.WebApp.csproj",
  # DB bilgileri (opsiyonel – sadece DB_SCHEMA.txt/örnek veri için)
  [string]$SqlServerInstance = "localhost",
  [string]$Database = "MyWeb",
  [string]$SamplesSchemaTable = "hist.Samples",
  [int]$TopSampleRows = 5
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path ".").Path
$stateDir  = Join-Path $root "_state"
$exportDir = Join-Path $root "_export"
$scriptsDir = Join-Path $root "scripts"

function Write-Info($msg)  { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Write-Warn($msg)  { Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-Err ($msg)  { Write-Host "[ERR ] $msg" -ForegroundColor Red }

Write-Info "Çalışma dizini: $root"

# 0) Ön kontroller
if (-not (Test-Path $WebAppCsproj)) { Write-Err "Csproj bulunamadı: $WebAppCsproj"; exit 1 }
if (-not (Test-Path $scriptsDir))   { Write-Err "scripts klasörü yok: $scriptsDir";   exit 1 }

New-Item -ItemType Directory -Force -Path $stateDir  | Out-Null
New-Item -ItemType Directory -Force -Path $exportDir | Out-Null

# 1) Build (Release)
Write-Info "dotnet build ($Configuration) başlıyor…"
dotnet build $WebAppCsproj -c $Configuration | Out-Null
Write-Info "dotnet build OK."

# 2) GIT bilgileri (branch/sha/log)
function Git-Exec([string]$args) {
  $pinfo = New-Object System.Diagnostics.ProcessStartInfo
  $pinfo.FileName = "git"
  $pinfo.Arguments = $args
  $pinfo.WorkingDirectory = $root
  $pinfo.RedirectStandardOutput = $true
  $pinfo.RedirectStandardError = $true
  $pinfo.UseShellExecute = $false
  $p = New-Object System.Diagnostics.Process
  $p.StartInfo = $pinfo
  $p.Start() | Out-Null
  $stdout = $p.StandardOutput.ReadToEnd()
  $stderr = $p.StandardError.ReadToEnd()
  $p.WaitForExit()
  if ($p.ExitCode -ne 0) { throw "git $args hata: $stderr" }
  return $stdout.Trim()
}

try {
  $branch = Git-Exec "rev-parse --abbrev-ref HEAD"
  $sha    = Git-Exec "rev-parse HEAD"
  $last10 = Git-Exec "log --oneline -n 10"
  $last15 = Git-Exec "log --oneline -n 15"
} catch {
  Write-Warn "git bilgileri alınamadı: $($_.Exception.Message)"
  $branch = "<unknown>"
  $sha = "<unknown>"
  $last10 = ""
  $last15 = ""
}

# 3) _state dosyaları
$buildTxt   = Join-Path $stateDir "BUILD.txt"
$statusTxt  = Join-Path $stateDir "STATUS.txt"
$branchTxt  = Join-Path $stateDir "GIT_BRANCH.txt"
$shaTxt     = Join-Path $stateDir "GIT_SHA.txt"
$last10Txt  = Join-Path $stateDir "LAST10.txt"
$last15Txt  = Join-Path $stateDir "LAST15.txt"
$dbSchema   = Join-Path $stateDir "DB_SCHEMA.txt"
$logLast    = Join-Path $stateDir "LOG_LASTRUN.txt"

# BUILD.txt
"Build: $Configuration`nTime: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')" | Out-File -Encoding UTF8 $buildTxt

# STATUS.txt (kısa özet)
$shortStatus = @()
$shortStatus += "Branch: $branch"
$shortStatus += "SHA: $sha"
$shortStatus += "Last export: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$shortStatus -join "`n" | Out-File -Encoding UTF8 $statusTxt

# GIT_* ve LAST*
$branch | Out-File -Encoding UTF8 $branchTxt
$sha    | Out-File -Encoding UTF8 $shaTxt
$last10 | Out-File -Encoding UTF8 $last10Txt
$last15 | Out-File -Encoding UTF8 $last15Txt

# 4) DB şeması ve örnek veri (opsiyonel)
$haveInvokeSql = Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue
if ($haveInvokeSql) {
  Write-Info "DB şema ve örnek veri çekiliyor ($SqlServerInstance/$Database)…"
  $schemaSql = @"
EXEC sp_help '$SamplesSchemaTable';
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='hist' AND TABLE_NAME='Samples';
"@
  $sampleSql = "SELECT TOP($TopSampleRows) * FROM hist.Samples ORDER BY Id DESC;"

  try {
    "=== SCHEMA ===" | Out-File -Encoding UTF8 $dbSchema
    Invoke-Sqlcmd -ServerInstance $SqlServerInstance -Database $Database -Query $schemaSql | Out-File -Encoding UTF8 -Append $dbSchema
    "`n=== TOP($TopSampleRows) ROWS ===" | Out-File -Encoding UTF8 -Append $dbSchema
    Invoke-Sqlcmd -ServerInstance $SqlServerInstance -Database $Database -Query $sampleSql | Out-File -Encoding UTF8 -Append $dbSchema
  } catch {
    Write-Warn "DB şema/örnek veri alınamadı: $($_.Exception.Message)"
    "DB snapshot alınamadı: $($_.Exception.Message)" | Out-File -Encoding UTF8 $dbSchema
  }
} else {
  Write-Warn "Invoke-Sqlcmd yok; DB_SCHEMA.txt atlandı. (İstersen: Install-Module SqlServer)"
  "Invoke-Sqlcmd yok; atlandı." | Out-File -Encoding UTF8 $dbSchema
}

# 5) Log son çalıştırma (varsa Logs/app-*.log al)
$logsDir = Join-Path (Split-Path $WebAppCsproj -Parent) "Logs"
if (Test-Path $logsDir) {
  $lastLog = Get-ChildItem $logsDir -File -Filter "app-*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($lastLog) {
    Write-Info "Log kopyalanıyor: $($lastLog.Name)"
    Get-Content -Path $lastLog.FullName -Tail 200 | Out-File -Encoding UTF8 $logLast
  } else {
    "Log bulunamadı (app-*.log yok)." | Out-File -Encoding UTF8 $logLast
  }
} else {
  "Logs klasörü yok." | Out-File -Encoding UTF8 $logLast
}

# 6) Önemli dosya snapshot’ları (_state içine)
function Snapshot-File($relPath) {
  $full = Join-Path $root $relPath
  if (Test-Path $full) {
    $nameOnly = ($relPath -replace "[:\\\/]", "_")
    $out = Join-Path $stateDir "$nameOnly.snapshot.txt"
    Write-Info "Snapshot: $relPath -> _state\$($nameOnly).snapshot.txt"
    Get-Content -Raw $full | Out-File -Encoding UTF8 $out
  } else {
    Write-Warn "Snapshot atlandı (yok): $relPath"
  }
}

# S3 kapsamında kritik saydıklarımız:
$toSnap = @(
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
$toSnap | ForEach-Object { Snapshot-File $_ }

# 7) _export güncelle (emit-project-files.ps1)
$emit = Join-Path $scriptsDir "emit-project-files.ps1"
if (Test-Path $emit) {
  Write-Info "emit-project-files.ps1 çalıştırılıyor…"
  powershell -ExecutionPolicy Bypass -File $emit | Out-Null
} else {
  Write-Warn "emit-project-files.ps1 bulunamadı; _export güncellenmedi."
}

# 8) (Opsiyonel) demo paket güncelle (make-demo-pkg.ps1)
$pkg = Join-Path $scriptsDir "make-demo-pkg.ps1"
if (Test-Path $pkg) {
  Write-Info "make-demo-pkg.ps1 çalıştırılıyor…"
  try {
    powershell -ExecutionPolicy Bypass -File $pkg | Out-Null
  } catch {
    Write-Warn "demo paket oluşturulamadı: $($_.Exception.Message)"
  }
} else {
  Write-Info "make-demo-pkg.ps1 yok; paket adımı atlandı."
}

# 9) git add/commit/push
Write-Info "git add ."
git add . | Out-Null

# Commit mesajına kısa özet ekleyelim
$stamp = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
$commitMsg = "[STATE] $Message | $stamp"
Write-Info "git commit -m `"$commitMsg`""
git commit -m "$commitMsg" | Out-Null

Write-Info "git push"
git push | Out-Null

Write-Host ""
Write-Host "==== ÖZET ====" -ForegroundColor Green
Write-Host "Branch : $branch"
Write-Host "SHA    : $sha"
Write-Host "_state  : güncellendi"
Write-Host "_export : güncellendi"
Write-Host "Paket  : $(if (Test-Path $pkg) { 'denendi' } else { 'atlandı' })"
Write-Host "Git    : push OK"
