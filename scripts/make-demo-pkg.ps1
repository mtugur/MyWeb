param(
  [string]$Root = (Split-Path -Parent $PSCommandPath)
)
$pkgRoot = Join-Path $Root "_packages"
$demoDir = Join-Path $pkgRoot "demo"
$config  = Join-Path $demoDir "config"
$zip     = Join-Path $pkgRoot "demo.zip"
$pkg     = Join-Path $pkgRoot "demo.mywebpkg"

New-Item -ItemType Directory -Force -Path $config | Out-Null

# manifest.json
@"
{
  "projectKey": "Demo.Plant",
  "projectName": "Demo Plant",
  "projVersion": "1.0.0",
  "min_engine": ">=1.0"
}
"@ | Set-Content -Path (Join-Path $demoDir "manifest.json") -Encoding UTF8

# controllers.json
@"
[
  { "name": "PLC1", "type": "Siemens.S7", "address": "192.168.0.10", "settings": { "rack": 0, "slot": 1 } }
]
"@ | Set-Content -Path (Join-Path $config "controllers.json") -Encoding UTF8

# tags.json (2 örnek tag + arşiv)
@"
[
  {
    "path": "Area1/Motor1/Speed",
    "name": "Speed",
    "dataType": "Float",
    "unit": "rpm",
    "address": "DB1.DBD0",
    "archive": { "mode": "Deadband", "deadbandAbs": 5, "rollups": ["1m","1h"] }
  },
  {
    "path": "Area1/Motor1/Run",
    "name": "Run",
    "dataType": "Bool",
    "address": "DB1.DBX0.0",
    "archive": { "mode": "ChangeOnly" }
  }
]
"@ | Set-Content -Path (Join-Path $config "tags.json") -Encoding UTF8

if (Test-Path $zip) { Remove-Item $zip -Force }
if (Test-Path $pkg) { Remove-Item $pkg -Force }
Compress-Archive -Path "$demoDir\*" -DestinationPath $zip -Force
Rename-Item -Path $zip -NewName (Split-Path -Leaf $pkg)
Write-Host "OK: $pkg oluşturuldu." -ForegroundColor Green
