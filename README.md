# Tripflow

Seyahat acenteleri için tur operasyon yönetimi (belge toplama, QR yoklama, gezi rehberi, duyuru, anket) - Vue + .NET + PostgreSQL.

## Repo yapısı

- apps/
  - web/  (Vue 3 + Vite)
  - api/  (.NET 8 Web API)
- packages/
  - contracts/ (ileride OpenAPI client / ortak tipler)
- infra/ (opsiyonel: deploy dokümanları, terraform vs.)
- docs/  (toplantı notları, karar kayıtları)

## Gereksinimler

- Docker Desktop
- Node.js (LTS)
- .NET SDK 8

## Hızlı başlangıç (local)

1) Env dosyasını oluştur:

- `env.example` dosyasını `.env` olarak kopyala:
  - `cp env.example .env`

2) PostgreSQL + pgAdmin'i ayağa kaldır:

```bash
docker compose up -d
```

3) pgAdmin:

- [http://localhost:5050](http://localhost:5050)
- login: `.env` içindeki PGADMIN_DEFAULT_EMAIL / PGADMIN_DEFAULT_PASSWORD

4) PostgreSQL bağlantı bilgisi:

- Host: localhost
- Port: 5432 (değiştirmek istersen .env'de POSTGRES_PORT)
- DB: tripflow
- User: tripflow
- Password: tripflow_dev_password
