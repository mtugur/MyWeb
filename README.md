# MyWeb — Sprint-1/2 Notları

## Çalıştırma
```ps1
dotnet build
dotnet run --project .\src\WebApp\MyWeb.WebApp\MyWeb.WebApp.csproj
Swagger: http://localhost:5113/swagger

Veritabanı: MyWeb (Schemas: catalog, hist)

Bootstrap paket yolu: _packages\demo.mywebpkg

API’ler
GET /api/projects

GET /api/tags?projectKey=Demo.Plant&q=&page=1&pageSize=100

GET /api/samples?tagPath=Area1/Motor1/Speed&projectKey=Demo.Plant&from=2025-08-12T00:00:00Z&to=2025-08-13T00:00:00Z&page=1&pageSize=100

PLC
Siemens PLC IP: 192.168.1.113

Manuel tag tanımları ileride kaldırılacak; paket/snapshot üzerinden gelecek.
