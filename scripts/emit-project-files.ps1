# scripts\emit-project-files.ps1
param(
  [string]$RepoRoot = (Split-Path -Parent (Split-Path -Parent $PSCommandPath)), # ...\MyWeb
  [string]$OutDir   = "$PWD\_export",
  [string]$DateTag  = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
)

$ErrorActionPreference = "Stop"
New-Item -Force -ItemType Directory -Path $OutDir | Out-Null

function Write-Lines {
  param([string]$Path,[string[]]$Lines)
  $dir = Split-Path -Parent $Path
  if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  Set-Content -Path $Path -Value $Lines -Encoding UTF8
}

function Get-Tree {
  param([string]$Base,[string[]]$IncludeExt=@())
  $files = Get-ChildItem -Path $Base -Recurse -File -ErrorAction SilentlyContinue
  if ($IncludeExt.Count -gt 0) {
    $files = $files | Where-Object { $IncludeExt -contains $_.Extension }
  }
  $files | Sort-Object FullName | ForEach-Object {
    $_.FullName.Replace($RepoRoot, '').TrimStart('\')
  }
}

# ---------- 01: PROJE ÖZETİ ----------
$ozet = @()
$ozet += "PROJE OZETI - $DateTag"
$ozet += "------------------------------------------------------------"
$ozet += "Amaç: PLC verisi → arşiv → sorgu → trend UI (SCADA/MES)."
$ozet += "Katmanlar: Core, Infrastructure(Persistence/Identity), Modules(Comm), Runtime, WebApp."
$ozet += "DB: SQL Server (MyWeb). Şemalar: catalog, hist, auth."
$ozet += ""
$ozet += "AĞAÇ:"
$ozet += Get-Tree -Base "$RepoRoot\src" -IncludeExt @(".sln",".cs",".csproj",".json",".cshtml",".js")
Write-Lines -Path "$OutDir\01-PROJE-OZETI.txt" -Lines $ozet

# ---------- 02: PROJE SABITLERI ----------
$launch = "$RepoRoot\src\WebApp\MyWeb.WebApp\Properties\launchSettings.json"
$urls = @()
if (Test-Path $launch) {
  $j = Get-Content $launch -Raw | ConvertFrom-Json
  $urls = ($j.profiles.'MyWeb.WebApp'.applicationUrl -split ';')
}
$swaggerHttp  = ($urls | Where-Object {$_ -like 'http://*'})  | Select-Object -First 1
$swaggerHttps = ($urls | Where-Object {$_ -like 'https://*'}) | Select-Object -First 1
$sabit = @()
$sa = "$RepoRoot\src\WebApp\MyWeb.WebApp\appsettings.json"
$sad = "$RepoRoot\src\WebApp\MyWeb.WebApp\appsettings.Development.json"
$cs = @()
foreach($p in @($sa,$sad)) {
  if(Test-Path $p){ $cs += (Get-Content $p -Raw) }
}
$sabit += "PROJE SABITLERI - $DateTag"
$sepline = "------------------------------------------------------------"
$sabit += $sepline
$sabit += "Swagger(HTTP):  " + ($swaggerHttp  ? "$swaggerHttp/swagger"  : "n/a")
$sabit += "Swagger(HTTPS): " + ($swaggerHttps ? "$swaggerHttps/swagger" : "n/a")
$sabit += $sepline
$sabit += "ConnectionStrings (raw json):"
$sabit += $cs
Write-Lines "$OutDir\02-PROJE-SABITLERI.txt" $sabit

# ---------- Katman içeriği yardımcı ----------
function Emit-Layer {
  param(
    [string]$Title,
    [string[]]$Paths
  )
  $lines = @()
  $lines += "$Title - $DateTag"
  $lines += "----------------------------------------------"
  foreach($path in $Paths){
    if(Test-Path $path){
      $lines += ""
      $lines += "===== FILE: " + $path.Replace($RepoRoot,'').TrimStart('\') + " ====="
      $lines += Get-Content $path -Raw
    }
  }
  return ,$lines
}

# ---------- 10-CORE ----------
$coreFiles = Get-ChildItem "$RepoRoot\src\Core\MyWeb.Core" -Recurse -File -Include *.cs,*.csproj -ErrorAction SilentlyContinue | % FullName
Write-Lines "$OutDir\10-CORE_ICERIGI.txt" (Emit-Layer -Title "CORE" -Paths $coreFiles)

# ---------- 20-INFRASTRUCTURE (Catalog/Hist + Identity/Auth) ----------
$infraFiles = @()
$infraFiles += Get-ChildItem "$RepoRoot\src\Infrastructure\MyWeb.Infrastructure.Data" -Recurse -File -Include *.cs,*.csproj -ErrorAction SilentlyContinue | % FullName
Write-Lines "$OutDir\20-INFRASTRUCTURE_ICERIGI.txt" (Emit-Layer -Title "INFRASTRUCTURE" -Paths $infraFiles)

# ---------- 30-MODULES (Siemens) ----------
$modFiles = Get-ChildItem "$RepoRoot\src\Modules\Communication.Siemens\MyWeb.Communication.Siemens" -Recurse -File -Include *.cs,*.csproj -ErrorAction SilentlyContinue | % FullName
Write-Lines "$OutDir\30-MODULES_ICERIGI.txt" (Emit-Layer -Title "MODULES" -Paths $modFiles)

# ---------- 40-RUNTIME ----------
$rtFiles = Get-ChildItem "$RepoRoot\src\Runtime\MyWeb.Runtime" -Recurse -File -Include *.cs,*.csproj -ErrorAction SilentlyContinue | % FullName
Write-Lines "$OutDir\40-RUNTIME_ICERIGI.txt" (Emit-Layer -Title "RUNTIME" -Paths $rtFiles)

# ---------- 50-WEBAPP (Program/AuthSetup/Controllers/Views/wwwroot/js) ----------
$webList = @(
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Program.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Infrastructure\AuthSetup.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\AccountController.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\HistoryUiController.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Controllers\Api\HistoryController.cs",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\Views\HistoryUi\Trend.cshtml",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\wwwroot\js\history-trend.js",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\appsettings.json",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\appsettings.Development.json",
  "$RepoRoot\src\WebApp\MyWeb.WebApp\MyWeb.WebApp.csproj"
) + (Get-ChildItem "$RepoRoot\src\WebApp\MyWeb.WebApp" -Recurse -File -Include *.cs,*.cshtml -ErrorAction SilentlyContinue | % FullName)
Write-Lines "$OutDir\50-WEBAPP_ICERIGI.txt" (Emit-Layer -Title "WEBAPP" -Paths ($webList | Select-Object -Unique))

# ---------- 90-INDEX-MANIFEST.json ----------
$manifest = [ordered]@{
  generatedAt = $DateTag
  files = @{}
}
Get-ChildItem $OutDir -File | ForEach-Object {
  $h = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
  $manifest.files[$_.Name] = @{ size=$_.Length; sha256=$h }
}
$manifestJson = ($manifest | ConvertTo-Json -Depth 5)
Write-Lines "$OutDir\90-INDEX-MANIFEST.json" $manifestJson

Write-Host "[OK] _export üretildi → $OutDir"
