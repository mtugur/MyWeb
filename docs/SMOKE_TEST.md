# Smoke Test Checklist

- [ ] `dotnet run` ile uygulama açılıyor, logda:
      - "Bootstrap: paket okunuyor" ✓
      - "snapshot tamam" ✓
      - "HistoryWriter başladı" ✓
- [ ] SSMS: `catalog.Projects`, `catalog.Tags`, `catalog.TagArchiveConfigs` tablolarında kayıtlar var
- [ ] SSMS: `hist.Samples` satır sayısı birkaç saniyede bir artıyor
- [ ] `http://localhost:5113/swagger` açılıyor
- [ ] `GET /api/projects` 200 OK ve en az 1 kayıt dönüyor
- [ ] Paket versiyon yükseltilince `catalog.ProjectVersions` tablosuna yeni satır geliyor
