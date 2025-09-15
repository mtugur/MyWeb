# MyWeb — Modüler SCADA/MES (ASP.NET + SQL Server)

Ignition benzeri, modüler SCADA/MES temelli bir .NET çözümü.

1) Hızlı Başlangıç

	```powershell
	# 1) Bağımlılıklar
		# - .NET SDK 9
		# - SQL Server (localdb veya instance)
		# - SSMS (seed SQL çalıştırmak için)

	# 2) Veritabanı ve tablo
		# SSMS: MyWeb DB oluşturun, aşağıdaki seed dosyasını çalıştırın:
		#   scripts/sql/seed-demo.sql  (projeyi/etiketleri ve hist.Samples V2 şemasını kurar)

	# 3) Çalıştır
		dotnet run --project .\src\WebApp\MyWeb.WebApp\MyWeb.WebApp.csproj
		# http://localhost:5113/history/trend
2) Mimari

	Core: domain tipleri, Hist.DataType vs.

	Infrastructure: CatalogDbContext (catalog şema), HistorianDbContext (hist şema)

	Modules: Siemens S7 haberleşme (S7.NetPlus)
	
	Runtime: Hosted Services (Sampling, HistoryWriter V2, Retention)

	WebApp: API + MVC (Trend view, Chart.js)

3) Trend UI

	Sayfa: /history/trend

	Script: wwwroot/js/history-trend.js

	Zaman dilimi: UI datetime-local değerleri yerel’dir; JS bunları UTC ISO'ya çevirip API’ye yollar.

	Eksende tarih+saat (TR), tooltipte saniye dahil.

4) Seed/Bootstrap

	Demo proje: Demo.Plant

	Demo tag: tDInt (2 günlük sinüs seed örnek script’i: scripts/sql/seed-samples-dint.sql)

5) Export & State

	_export/: proje özeti, dosya ağaçları ve içerikler (ChatGPT “Dosyalar” için)

	_state/: GIT ve son çalıştırma durum anlık görüntüleri

	Tek komut: .\scripts\Save-StateAndPush.ps1

6) Geliştirme

	.gitignore güncel; Logs/ ve build çıktıları repo dışı.

	EF Core: tek DB, iki şema (catalog, hist).

	V2 hist.Samples: TagId + Utc clustered index, MonthKey computed.

7) Yol Haritası (kısa vade)

	S4: Alarm/Event tablosu + servis

	Sx: Rollup/aggregate (saatlik/günlük)

	Güvenlik/Yetkilendirme (Identity)