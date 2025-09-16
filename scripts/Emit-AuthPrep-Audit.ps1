# ===================== Emit-AuthPrep-Audit.ps1 (v3.1) =====================
# 1) Paths
$root = "C:\Users\User\Desktop\Proje\MyWeb"
$out  = "C:\Users\User\Desktop\Proje\MyWeb_AuthPrep_Audit.txt"

if (Test-Path $out) { Remove-Item $out -Force -ErrorAction SilentlyContinue }
$null = New-Item -ItemType Directory -Path (Split-Path $out -Parent) -Force | Out-Null

# Helpers
function Add-Header($title) {
  "============================================================" | Out-File -FilePath $out -Append -Encoding utf8
  "###  $title" | Out-File -FilePath $out -Append -Encoding utf8
  "============================================================" | Out-File -FilePath $out -Append -Encoding utf8
}
function Add-Line($text="") { $text | Out-File -FilePath $out -Append -Encoding utf8 }
function Has-Tool($name) { return (Get-Command $name -ErrorAction SilentlyContinue) -ne $null }

function Add-File($relPath) {
  $full = Join-Path $root $relPath
  Add-Header ("FILE: " + $relPath)
  if (Test-Path $full) {
    Get-Content $full -Raw -ErrorAction SilentlyContinue | Out-File -FilePath $out -Append -Encoding utf8
  } else {
    Add-Line ("[MISSING] " + $relPath)
  }
  Add-Line
}

function Add-Command($title, $scriptBlock, $requireExe=$null) {
  Add-Header ("CMD: " + $title)
  try {
    if ($requireExe -and -not (Has-Tool $requireExe)) {
      Add-Line ("[SKIP] '" + $requireExe + "' not found (PATH).")
    } else {
      $prev = $ErrorActionPreference
      $ErrorActionPreference = 'Stop'
      & $scriptBlock 2>&1 | Out-File -FilePath $out -Append -Encoding utf8
      $ErrorActionPreference = $prev
    }
  } catch {
    Add-Line ("[ERROR] " + $_.Exception.Message)
  }
  Add-Line
}

# ---- Read appsettings to detect SQL Server/Data Source and Database ----
function Get-DbInfoFromAppSettings {
  $files = @(
    (Join-Path $root 'src\WebApp\MyWeb.WebApp\appsettings.Development.json')
    (Join-Path $root 'src\WebApp\MyWeb.WebApp\appsettings.json')
  )
  $server = $null
  $database = $null

  foreach ($f in $files) {
    if (Test-Path $f) {
      $raw = Get-Content $f -Raw -ErrorAction SilentlyContinue

      # Önce ConnectionStrings içinden tam DSN’i yakalamayı dene
      $m = [regex]::Matches($raw, "(?i)(?:Server|Data\s*Source)\s*=\s*([^;]+).*?(?:Database|Initial\s*Catalog)\s*=\s*([^;]+)")
      if ($m.Count -gt 0) {
        $server = $m[0].Groups[1].Value.Trim()
        $database = $m[0].Groups[2].Value.Trim()
        break
      }

      # Ayrık yakalama (Server ve Database ayrı satırlardaysa)
      $m1 = [regex]::Match($raw, "(?i)(?:Server|Data\s*Source)\s*=\s*([^;]+)")
      $m2 = [regex]::Match($raw, "(?i)(?:Database|Initial\s*Catalog)\s*=\s*([^;]+)")
      if ($m1.Success -and $m2.Success) {
        $server = $m1.Groups[1].Value.Trim()
        $database = $m2.Groups[1].Value.Trim()
        break
      }
    }
  }

  if (-not $server)   { $server = "localhost" }
  if (-not $database) { $database = "MyWeb" }
  return @{ Server = $server; Database = $database }
}

# 2) ENV
Add-Header "ENV SUMMARY"
Add-Line ("Timestamp: " + (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"))
Add-Line ("PSVersion: " + $PSVersionTable.PSVersion.ToString())
Add-Line ("Machine: " + $env:COMPUTERNAME)
Add-Line ("User: " + $env:USERNAME)
Add-Line ("Root: " + $root)
Add-Line

# 3) Git / dotnet
Add-Command "git status (short)" { git -C $root status } "git"
Add-Command "git log --oneline -n 5" { git -C $root log --oneline -n 5 } "git"
Add-Command "dotnet --info" { dotnet --info } "dotnet"

# 4) Tree + packages
Add-Header "TREE /F /A"
Push-Location $root
cmd /c "tree /F /A" | Out-File -FilePath $out -Append -Encoding utf8
Pop-Location
Add-Line

$projects = @(
  "src\Core\MyWeb.Core\MyWeb.Core.csproj",
  "src\Infrastructure\MyWeb.Infrastructure.Data\MyWeb.Persistence.csproj",
  "src\Runtime\MyWeb.Runtime\MyWeb.Runtime.csproj",
  "src\WebApp\MyWeb.WebApp\MyWeb.WebApp.csproj"
)
foreach ($p in $projects) {
  $projPath = Join-Path $root $p
  if (Test-Path $projPath) {
    Add-Command ("dotnet list package :: " + $p) { dotnet list $projPath package } "dotnet"
  } else {
    Add-Header ("dotnet list package :: " + $p)
    Add-Line ("[MISSING] " + $p)
    Add-Line
  }
}

# 5) Critical files
Add-File "src\WebApp\MyWeb.WebApp\Program.cs"
Add-File "src\WebApp\MyWeb.WebApp\MyWeb.WebApp.csproj"
Add-File "src\WebApp\MyWeb.WebApp\appsettings.json"
Add-File "src\WebApp\MyWeb.WebApp\appsettings.Development.json"
Add-File "src\WebApp\MyWeb.WebApp\Properties\launchSettings.json"

Add-File "src\WebApp\MyWeb.WebApp\Controllers\Api\DiagnosticsController.cs"
Add-File "src\WebApp\MyWeb.WebApp\Controllers\Api\HistoryController.cs"
Add-File "src\WebApp\MyWeb.WebApp\Controllers\Api\ProjectsController.cs"
Add-File "src\WebApp\MyWeb.WebApp\Controllers\Api\SamplesController.cs"
Add-File "src\WebApp\MyWeb.WebApp\Controllers\Api\TagsController.cs"
Add-File "src\WebApp\MyWeb.WebApp\Controllers\HomeController.cs"
Add-File "src\WebApp\MyWeb.WebApp\Controllers\PlcController.cs"
Add-File "src\WebApp\MyWeb.WebApp\Controllers\HistoryUiController.cs"

Add-File "src\Runtime\MyWeb.Runtime\ServiceCollectionExtensions.cs"
Add-File "src\Runtime\MyWeb.Runtime\RuntimeOptions.cs"
Add-File "src\Runtime\MyWeb.Runtime\DbConnOptions.cs"
Add-File "src\Runtime\MyWeb.Runtime\HistoryOptions.cs"
Add-File "src\Runtime\MyWeb.Runtime\SamplingOptions.cs"
Add-File "src\Runtime\MyWeb.Runtime\History\HistoryWriterService.cs"
Add-File "src\Runtime\MyWeb.Runtime\Services\TagSamplingService.cs"
Add-File "src\Runtime\MyWeb.Runtime\Services\RetentionCleanerService.cs"
Add-File "src\Runtime\MyWeb.Runtime\Services\PlcConnectionWatchdog.cs"

Add-File "src\Modules\Communication.Siemens\MyWeb.Communication.Siemens\SiemensCommunicationChannel.cs"
Add-File "src\Modules\Communication.Siemens\MyWeb.Communication.Siemens\PlcConnectionSettings.cs"

Add-File "src\Core\MyWeb.Core\Communication\ICommunicationChannel.cs"
Add-File "src\Core\MyWeb.Core\Communication\TagDefinition.cs"
Add-File "src\Core\MyWeb.Core\Communication\ReadResult.cs"
Add-File "src\Core\MyWeb.Core\Communication\WriteResult.cs"
Add-File "src\Core\MyWeb.Core\Communication\CommunicationOptions.cs"
Add-File "src\Core\MyWeb.Core\Communication\ResilientCommunicationChannelDecorator.cs"
Add-File "src\Core\MyWeb.Core\Communication\ChannelState.cs"
Add-File "src\Core\MyWeb.Core\Communication\TagQuality.cs"

Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Catalog\CatalogDbContext.cs"
Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Catalog\Entities\Project.cs"
Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Catalog\Entities\ProjectVersion.cs"
Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Catalog\Entities\Tag.cs"
Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Catalog\Entities\Controller.cs"
Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Catalog\Entities\TagArchiveConfig.cs"

Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Historian\HistorianDbContext.cs"
Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Historian\Entities\Sample.cs"
Add-File "src\Infrastructure\MyWeb.Infrastructure.Data\Common\UtcDateTimeConverter.cs"

Add-Header "MIGRATIONS - LIST (Catalog)"
Get-ChildItem (Join-Path $root "src\Infrastructure\MyWeb.Infrastructure.Data\Migrations\Catalog") -ErrorAction SilentlyContinue |
  Sort-Object Name | ForEach-Object { $_.FullName } | Out-File -FilePath $out -Append -Encoding utf8
Add-Line

Add-Header "MIGRATIONS - LIST (Historian)"
Get-ChildItem (Join-Path $root "src\Infrastructure\MyWeb.Infrastructure.Data\Migrations\Historian") -ErrorAction SilentlyContinue |
  Sort-Object Name | ForEach-Object { $_.FullName } | Out-File -FilePath $out -Append -Encoding utf8
Add-Line

# 6) Logs
$logs = Get-ChildItem (Join-Path $root "src\WebApp\MyWeb.WebApp\Logs") -Filter "*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
if ($logs -and $logs.Count -gt 0) {
  $latest = $logs[0].FullName
  Add-Header ("LOG TAIL (last 200) :: " + (Split-Path $latest -Leaf))
  Get-Content $latest -Tail 200 | Out-File -FilePath $out -Append -Encoding utf8
  Add-Line
} else {
  Add-Header "LOG TAIL (last 200)"
  Add-Line "[SKIP] No logs found."
  Add-Line
}

# 7) SQL snapshot (auto-detect from appsettings, only DB=MyWeb on localhost/instance)
$SqlInfo   = Get-DbInfoFromAppSettings
$SqlServer = $SqlInfo.Server
$Database  = $SqlInfo.Database
$SqlTimeout = 3

# Yalnızca MyWeb ile ilgileniyoruz
if ($Database -ne "MyWeb") { $Database = "MyWeb" }

# İlk deneme: appsettings’teki Server
$connected = $false
if (Has-Tool "sqlcmd") {
  & sqlcmd -S $SqlServer -d $Database -E -l $SqlTimeout -Q "SELECT 1" 2>$null
  if ($LASTEXITCODE -eq 0) { $connected = $true }

  # Fallback: MSSQL$WINCC çalışıyorsa localhost\WINCC dene (tek deneme)
  if (-not $connected) {
    $svc = Get-Service -Name 'MSSQL$WINCC' -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -eq 'Running') {
      $SqlServer = "localhost\WINCC"
      & sqlcmd -S $SqlServer -d $Database -E -l $SqlTimeout -Q "SELECT 1" 2>$null
      if ($LASTEXITCODE -eq 0) { $connected = $true }
    }
  }
}

Add-Header "SQL CONNECTION INFO (effective)"
Add-Line ("Server: " + $SqlServer)
Add-Line ("Database: " + $Database)
Add-Line

if (Has-Tool "sqlcmd") {
  if ($connected) {
    Add-Header "SQL SNAPSHOT (Tables in DB)"
    sqlcmd -S $SqlServer -d $Database -E -Q "SET NOCOUNT ON; SELECT TABLE_SCHEMA+'.'+TABLE_NAME FROM INFORMATION_SCHEMA.TABLES ORDER BY 1;" |
      Out-File -FilePath $out -Append -Encoding utf8
    Add-Line

    Add-Header "SQL SNAPSHOT (hist.Samples TOP 5)"
    sqlcmd -S $SqlServer -d $Database -E -Q "SET NOCOUNT ON; SELECT TOP (5) * FROM hist.Samples ORDER BY Utc DESC;" |
      Out-File -FilePath $out -Append -Encoding utf8
    Add-Line
  } else {
    Add-Header "SQL SNAPSHOT"
    Add-Line "[SKIP] Unable to connect with sqlcmd (timeout or wrong instance)."
    Add-Line
  }
} else {
  Add-Header "SQL SNAPSHOT"
  Add-Line "[SKIP] 'sqlcmd' not found; skipped."
  Add-Line
}

# 8) Done
Add-Header "DONE"
Add-Line ("Report: " + $out)
Write-Host "Ready: $out"
# ===================== (end) =====================
