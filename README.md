# Tripflow

Tripflow, seyahat acenteleri için tur operasyon yönetimi (belge toplama, QR yoklama, gezi rehberi, duyuru, anket) uygulamasıdır. Stack: Vue 3 + .NET 8 + PostgreSQL.

## Repo yapısı

- apps/
  - web/ (Vue 3 + Vite)
  - api/ (.NET 8 Web API)
- packages/
  - contracts/ (ileride OpenAPI client / ortak tipler)
- infra/ (opsiyonel: deploy dokümanları, terraform vs.)
- docs/ (toplantı notları, karar kayıtları)

## Gereksinimler

- Docker Desktop (Docker Compose dahil)
- Node.js (LTS) + npm
- .NET SDK 8

## Hızlı başlangıç (local)

### A) env.example -> .env (Docker Compose için)

Mac/Linux:

    cp env.example .env

Windows PowerShell:

    Copy-Item env.example .env

### B) PostgreSQL + pgAdmin'i çalıştır

    docker compose up -d

### C) pgAdmin erişimi ve login

- <http://localhost:5050>
- kullanıcı/şifre: .env içindeki PGADMIN_DEFAULT_EMAIL / PGADMIN_DEFAULT_PASSWORD
- Not: PGADMIN_DEFAULT_EMAIL geçerli e-posta formatında olmalı.

### D) PostgreSQL bağlantı bilgileri

- Host: localhost
- Port: 5432 (değiştirmek için .env içindeki POSTGRES_PORT)
- DB: tripflow
- User: tripflow
- Password: .env içindeki POSTGRES_PASSWORD

## Local Setup

1) Docker Compose

    docker compose up -d

2) API user-secrets (ConnectionStrings + JWT)

    cd apps/api/Tripflow.Api
    dotnet user-secrets init
    dotnet user-secrets set "ConnectionStrings:TripflowDb" "Host=localhost;Port=5432;Database=tripflow;Username=tripflow;Password=YOUR_PASSWORD"
    dotnet user-secrets set "JWT_ISSUER" "tripflow-dev"
    dotnet user-secrets set "JWT_AUDIENCE" "tripflow-dev"
    dotnet user-secrets set "JWT_SECRET" "YOUR_LONG_RANDOM_SECRET"

3) EF migration

    dotnet ef database update

4) Dev seed (Development only)

API'yi bir terminalde çalıştır:

    dotnet run

Başka bir terminalde seed at:

    curl -X POST http://localhost:5051/api/dev/seed

5) Web

    cd ../../..
    npm install
    npm --workspace apps/web run dev

Demo hesapları (dev seed):

- admin@demo.local / admin123
- guide@demo.local / guide123

## API (.NET 8) çalıştırma

Not: root .env dosyasını "source" etmek sadece o terminal için geçerlidir; dotnet run başka terminalde env değerlerini görmeyebilir. Bu yüzden user-secrets kullan.

Adımlar:

    cd apps/api/Tripflow.Api
    dotnet user-secrets init
    dotnet user-secrets set "ConnectionStrings:TripflowDb" "Host=localhost;Port=5432;Database=tripflow;Username=tripflow;Password=YOUR_PASSWORD"
    dotnet user-secrets set "JWT_ISSUER" "tripflow-dev"
    dotnet user-secrets set "JWT_AUDIENCE" "tripflow-dev"
    dotnet user-secrets set "JWT_SECRET" "YOUR_LONG_RANDOM_SECRET"

Projede farklı bir bağlantı adı kullanılıyorsa (ConnectionStrings:TripflowDb yerine), o anahtarı set et.

API'yi çalıştırma:

    dotnet run

Endpointler:

- Swagger: <http://localhost:5051/swagger>
- Health: <http://localhost:5051/health>

## Web (Vue) çalıştırma

    cd apps/web
    npm install
    npm run dev

API base URL için apps/web/.env.local (veya apps/web/.env) kullan:

    VITE_API_BASE_URL=http://localhost:5051

## Sık karşılaşılan sorunlar

- CONNECTION_STRING / ConnectionStrings bulunamadı: user-secrets doğru projede mi set edildi, key adı doğru mu?
- Port çakışmaları: 5432/5050 kullanılamıyorsa .env içinden değiştir.
- pgAdmin login sorunu: PGADMIN_DEFAULT_EMAIL geçerli e-posta formatında olmalı.

## Güvenlik notu

- appsettings.json içine secret koyulmuyor
- Local'de user-secrets, prod/CI tarafında environment variables tercih edilir.
