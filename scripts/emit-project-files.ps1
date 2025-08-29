# tools\emit-project-files.ps1
param(
  [string]$RepoRoot = "$PWD",              # Proje kökü
  [string]$OutDir   = "$PWD\_export",      # Geçici üretim klasörü
  [string]$DateTag  = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
)

New-Item -Force -ItemType Directory -Path $OutDir | Out-Null

# --- 01: PROJE ÖZETİ (kısa metin + ağaç) ---
$ozet = @()
$ozet += "PROJE OZETI - $DateTag"
$ozet += "------------------------------------------------------------"
$ozet += "Amaç: PLC verisini okuma → arşivleme → sorgu → trend UI."
$ozet += "Katmanlar: Core, Infrastructure(Persistence), Modules(Comm), Runtime, WebApp."
$ozet += "DB: SQL Server (MyWeb), Şemalar: catalog, hist."
$ozet += "Ana URL: http://localhost:5113  | Swagger: /swagger"
$ozet += ""
$ozet | Set-Content "$OutDir\01-PROJE-OZETI.txt" -Encoding UTF8
cmd /c "tree /F /A > `"$OutDir\_tree.txt`""
Add-Content "$OutDir\01-PROJE-OZETI.txt" "`n--- KLASOR AGACI ---`n"
Add-Content "$OutDir\01-PROJE-OZETI.txt" (Get-Content "$OutDir\_tree.txt")
Remove-Item "$OutDir\_tree.txt" -Force

# --- 02: PROJE SABITLERI ---
$sabit = @()
$sabit += "PROJE SABITLERI - $DateTag"
$sabit += "------------------------------------------------------------"
$sabit += "Repo: (GitHub linkini buraya yaz)"
$sabit += "Lokal Kök: $RepoRoot"
$sabit += "PLC: IP=192.168.1.113, Rack=0, Slot=1, CPU=S71500 (örnek)"
$sabit += "DB: Server=localhost; Database=MyWeb; Trusted_Connection=True"
$sabit += "Swagger: http://localhost:5113/swagger"
$sabit | Set-Content "$OutDir\02-PROJE-SABITLERI.txt" -Encoding UTF8

# --- Yardımcı: bir katman içeriğini üret (özet + ağaç + seçili dosya içerikleri) ---
function Emit-Layer {
  param(
    [string]$LayerName,
    [string]$SourceDir,
    [string[]]$IncludeExt = @(".cs",".csproj",".json",".sql",".md"),
    [string[]]$MustHave   = @()  # daima eklenecek önemli dosyalar (göreli yol)
  )
  $outFile = "{0}\{1}" -f $OutDir, $LayerName
  $head = @()
  $head += "$LayerName - $DateTag"
  $head += "------------------------------------------------------------"
  $head += "Kaynak: $SourceDir"
  $head += ""
  $head | Set-Content $outFile -Encoding UTF8

  # Klasör ağacı
  $tmpTree = Join-Path $OutDir "_tree_$LayerName.txt"
  cmd /c "tree /F /A `"$SourceDir`" > `"$tmpTree`""
  Add-Content $outFile "`n--- KLASOR AGACI ---`n"
  Add-Content $outFile (Get-Content $tmpTree)
  Remove-Item $tmpTree -Force

  # Önemli dosyalar (MustHave) + kritik uzantılar
  $files = Get-ChildItem -Path $SourceDir -Recurse -File |
           Where-Object { $IncludeExt -contains $_.Extension } |
           Sort-Object FullName

  # MustHave öne
  $must = @()
  foreach($rel in $MustHave){
    $f = Join-Path $SourceDir $rel
    if (Test-Path $f){ $must += Get-Item $f }
  }
  $files = $must + ($files | Where-Object { $must -notcontains $_ })

  # İçerikleri ekle (boyut limitine yaklaşırsa parçalara bölebilirsin)
  foreach($f in $files){
    Add-Content $outFile "`n=================================================="
    Add-Content $outFile "Dosya: $($f.FullName.Replace($RepoRoot + '\', ''))"
    Add-Content $outFile "==================================================`n"
    # Büyük dosyaları kısmen almak istersen burada kısıt koyabilirsin.
    Add-Content $outFile (Get-Content -Raw $f.FullName)
  }

  return $outFile
}

# Katman başlıkları ve kaynak klasörleri (projene göre güncel)
$L_CORE  = Emit-Layer -LayerName "10-CORE_ICERIGI.txt"            -SourceDir "$RepoRoot\src\Core\MyWeb.Core" `
                      -MustHave @("MyWeb.Core.csproj")
$L_INFRA = Emit-Layer -LayerName "20-INFRASTRUCTURE_ICERIGI.txt"   -SourceDir "$RepoRoot\src\Infrastructure\MyWeb.Infrastructure.Data" `
                      -MustHave @("MyWeb.Persistence.csproj","Historian\HistorianDbContext.cs","Historian\Entities\Sample.cs","Catalog\CatalogDbContext.cs")
$L_MOD   = Emit-Layer -LayerName "30-MODULES_ICERIGI.txt"          -SourceDir "$RepoRoot\src\Modules" `
                      -MustHave @("Communication.Siemens\MyWeb.Communication.Siemens\MyWeb.Communication.Siemens.csproj")
$L_RUN   = Emit-Layer -LayerName "40-RUNTIME_ICERIGI.txt"          -SourceDir "$RepoRoot\src\Runtime\MyWeb.Runtime" `
                      -MustHave @("MyWeb.Runtime.csproj","ServiceCollectionExtensions.cs","History\HistoryWriterOptions.cs","Services\RetentionCleanerService.cs")
$L_WEB   = Emit-Layer -LayerName "50-WEBAPP_ICERIGI.txt"           -SourceDir "$RepoRoot\src\WebApp\MyWeb.WebApp" `
                      -MustHave @("MyWeb.WebApp.csproj","Program.cs","Controllers\Api\HistoryController.cs","Views\Shared\_Layout.cshtml")

# --- STATE (tek dosya) ---
$branch = (git rev-parse --abbrev-ref HEAD) 2>$null
$sha    = (git rev-parse --short HEAD) 2>$null
$last10 = (git log --oneline -10) 2>$null
$state  = @()
$state += "_STATE_SNAPSHOT - $DateTag"
$state += "Branch: $branch"
$state += "SHA:    $sha"
$state += ""
$state += "--- LAST 10 COMMITS ---"
$state += $last10
$state | Set-Content "$OutDir\_STATE_SNAPSHOT.txt" -Encoding UTF8

# --- MANIFEST ---
$manifest = @{
  generatedAt = $DateTag
  branch      = "$branch"
  sha         = "$sha"
  files = @(
    "01-PROJE-OZETI.txt","02-PROJE-SABITLERI.txt",
    "10-CORE_ICERIGI.txt","20-INFRASTRUCTURE_ICERIGI.txt",
    "30-MODULES_ICERIGI.txt","40-RUNTIME_ICERIGI.txt","50-WEBAPP_ICERIGI.txt",
    "_STATE_SNAPSHOT.txt"
  )
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content "$OutDir\90-INDEX-MANIFEST.json" -Encoding UTF8

Write-Host "Hazır: $OutDir (Project Files'a yükleyebilirsin)"
