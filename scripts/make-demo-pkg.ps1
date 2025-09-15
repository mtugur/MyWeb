Param(
  [string]$OutDir = "$(Split-Path -Path $PSScriptRoot -Parent)\_packages"
)

$ErrorActionPreference = "Stop"

# Repo kökü
$RepoRoot = Resolve-Path "$(Split-Path -Path $PSScriptRoot -Parent)"

# Çıktı klasörünü hazırla
if (-not (Test-Path $OutDir)) {
  New-Item -ItemType Directory -Path $OutDir | Out-Null
}

# Basit demo paket içeriği (örnek)
$pkgName = "demo.mywebpkg"
$pkgPath = Join-Path $OutDir $pkgName

# Geçici çalışma klasörü
$temp = Join-Path $OutDir "_tmp_pkg"
if (Test-Path $temp) { Remove-Item -Recurse -Force $temp }
New-Item -ItemType Directory -Path $temp | Out-Null

# Minimal manifest ve içerikler
$manifest = @{
  Id        = "Demo.Plant"
  Name      = "Demo Plant"
  Version   = "1.0.0"
  CreatedUtc= (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Depth 5

$controllersJson = @(
  @{ Name="HistoryController"; Route="/api/hist"; Version="1.0.0" }
) | ConvertTo-Json -Depth 5

$tagsJson = @(
  @{ Name="tBool";   Path="Demo/tBool";   DataType=0; Address="DB1000.DBX0.0"; LongString=$false }
  @{ Name="tInt";    Path="Demo/tInt";    DataType=1; Address="DB1000.DBW16";  LongString=$false }
  @{ Name="tDInt";   Path="Demo/tDInt";   DataType=1; Address="DB1000.DBD30";  LongString=$false }
  @{ Name="tReal";   Path="Demo/tReal";   DataType=2; Address="DB1000.DBD42";  LongString=$false }
  @{ Name="tString"; Path="Demo/tString"; DataType=3; Address="DB1000.DBB54";  LongString=$false }
) | ConvertTo-Json -Depth 5

Set-Content -Path (Join-Path $temp "manifest.json") -Value $manifest -Encoding UTF8
Set-Content -Path (Join-Path $temp "controllers.json") -Value $controllersJson -Encoding UTF8
Set-Content -Path (Join-Path $temp "tags.json") -Value $tagsJson -Encoding UTF8

# Paketle (zip uzantısız mywebpkg)
if (Test-Path $pkgPath) { Remove-Item $pkgPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($temp, $pkgPath)

# Temizle
Remove-Item -Recurse -Force $temp

Write-Host "[INFO] Demo paket üretildi: $pkgPath"
