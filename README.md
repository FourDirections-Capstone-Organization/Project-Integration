# Project Integration — Multi-System Architecture

A full-stack mini project demonstrating two independent systems with shared JWT authentication, deployed across Vercel (frontends) and Azure (backends + databases).

## Architecture

```
                         ┌──────────────────────┐
                         │    Auth Service       │
                         │  (.NET 9, PostgreSQL) │
                         └──────┬───────────────┘
                                │ Issues JWTs
                    ┌───────────┴───────────┐
                    │                       │
         ┌──────────▼──────────┐  ┌─────────▼──────────┐
         │ Operational System  │  │  Delivery System    │
         │ (.NET 9, PostgreSQL)│  │ (.NET 8, SQL Server)│
         │ Products CRUD       │  │ Orders CRUD         │
         │ Scalar API Docs     │  │ Swagger API Docs    │
         └──────────┬──────────┘  └─────────┬──────────┘
                    │                        │
                    └────── JWT (svc acct) ──┘
```

## Tech Stack

### Auth Service
| Layer | Technology |
|---|---|
| **Backend** | ASP.NET Core 9, C# |
| **Database** | PostgreSQL 16 |
| **ORM** | Entity Framework Core (Npgsql) |
| **API Documentation** | OpenAPI / Scalar |
| **Hosting** | Azure Container Apps |

### Operational System
| Layer | Technology |
|---|---|
| **Backend** | ASP.NET Core 9, C# |
| **Frontend** | React 18, TypeScript, Vite |
| **Database** | PostgreSQL 16 |
| **ORM** | Entity Framework Core (Npgsql) |
| **API Documentation** | OpenAPI / Scalar |
| **Hosting** | Azure Container Apps (API) + Vercel (Frontend) |

### Delivery System
| Layer | Technology |
|---|---|
| **Backend** | ASP.NET Core 8, C# |
| **Frontend** | React 18, TypeScript, Vite |
| **Database** | Microsoft SQL Server |
| **ORM** | Entity Framework Core (SqlServer) |
| **API Documentation** | OpenAPI / Swagger |
| **Hosting** | Azure Container Apps (API) + Vercel (Frontend) |

## Project Structure

```
Project-Integration/
├── auth-service/                   # Standalone auth service (.NET 9)
│   ├── AuthService.sln
│   └── AuthService.Api/
│       ├── Controllers/            # Auth, Admin
│       ├── Models/                 # UserAccount, DTOs
│       ├── Data/                   # AppDbContext (PostgreSQL)
│       ├── Services/               # AuthService, JwtService
│       └── Dockerfile
├── operational-system/             # Operational system (.NET 9)
│   ├── OperationalSystem.sln
│   ├── OperationalSystem.Api/      # Products CRUD + Integration
│   │   ├── Controllers/            # Products, Integration
│   │   ├── Models/                 # Product
│   │   ├── Data/                   # AppDbContext (PostgreSQL)
│   │   ├── Services/               # ProductService, DeliverySystemClient
│   │   └── Dockerfile
│   └── operational-frontend/       # React + Vite (Vercel)
├── delivery-system/                # Delivery system (.NET 8)
│   ├── DeliverySystem.sln
│   ├── DeliverySystem.Api/         # Orders CRUD + Integration
│   │   ├── Controllers/            # Orders, Integration
│   │   ├── Models/                 # Order
│   │   ├── Data/                   # AppDbContext (SQL Server)
│   │   ├── Services/               # OrderService, OperationalSystemClient
│   │   └── Dockerfile
│   └── delivery-frontend/          # React + Vite (Vercel)
├── .github/workflows/
│   ├── deploy-auth.yml             # Auth Service CI/CD
│   ├── deploy-operational.yml      # Operational System CI/CD
│   └── deploy-delivery.yml         # Delivery System CI/CD
├── IMPLEMENTATION_PLAN.md          # Implementation plan
├── DEPLOYMENT_GUIDE.md             # Deployment guide
└── centralized-auth-integration.md # Auth integration reference
```

## Shared JWT Configuration

All three systems share the same JWT configuration:

| Parameter | Value |
|---|---|
| `Jwt:Key` | Shared 32+ char secret |
| `Jwt:Issuer` | `CentralAuth` |
| `Jwt:Audience` | `InternalSystems` |

## Seed Accounts

| Employee Number | Password | Role | Description |
|---|---|---|---|
| `admin` | `admin123` | `SystemAdmin` | Full access to all systems |
| `SVC-OPERATIONAL` | `svc-operational-pwd` | `Operational.ExternalService` | Service account for Operational |
| `SVC-DELIVERY` | `svc-delivery-pwd` | `Delivery.ExternalService` | Service account for Delivery |

## Cross-System Integration

- **Operational → Delivery**: `DeliverySystemClient` logs in as `SVC-OPERATIONAL`, calls Delivery's `/api/integration/orders/{id}`
- **Delivery → Operational**: `OperationalSystemClient` logs in as `SVC-DELIVERY`, calls Operational's `/api/integration/products/{id}`

## Deployment

See [DEPLOYMENT_GUIDE.md](./DEPLOYMENT_GUIDE.md) for full step-by-step deployment instructions.

Quick overview:
1. Create Azure resources (3 databases, 3 container apps, 1 container registry)
2. Create Vercel projects (2 frontends)
3. Configure GitHub Actions secrets
4. Push to `main` → auto-deploys via CI/CD

## Local Development

### Auth Service
```bash
cd auth-service/AuthService.Api
dotnet run
```
URL: `http://localhost:5000`, Scalar: `/scalar/v1`

### Operational System
```bash
cd operational-system/OperationalSystem.Api
dotnet run
```
URL: `http://localhost:5001`, Scalar: `/scalar/v1`

### Delivery System
```bash
cd delivery-system/DeliverySystem.Api
dotnet run
```
URL: `http://localhost:5002`, Swagger: `/swagger`

### Frontends
```bash
cd operational-system/operational-frontend
npm run dev     # http://localhost:5173

cd delivery-system/delivery-frontend
npm run dev     # http://localhost:5174
```

Set `VITE_AUTH_URL` and `VITE_API_URL` in `.env` files to point to your local backend URLs.
