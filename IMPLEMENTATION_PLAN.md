# Implementation Plan — Two-System Mini Project

## Architecture

```
                         ┌──────────────────────┐
                         │    Auth Service       │
                         │  (.NET 9, PostgreSQL) │
                         │  JWT Issuance         │
                         │  Account Management   │
                         └──────┬───────────────┘
                                │ Issues JWTs
                    ┌───────────┴───────────┐
                    │                       │
         ┌──────────▼──────────┐  ┌─────────▼──────────┐
         │ Operational System  │  │  Delivery System    │
         │ (.NET 9, PostgreSQL)│  │ (.NET 8, SQL Server)│
         │ Products CRUD       │  │ Orders CRUD         │
         │ Scalar API Docs     │  │ Swagger API Docs    │
         │ Integration Endpts  │  │ Integration Endpts  │
         └──────────┬──────────┘  └─────────┬──────────┘
                    │                        │
                    └────── JWT (svc acct) ──┘
```

## Project Structure

```
Project-Integration/
├── auth-service/
│   ├── AuthService.sln
│   └── AuthService.Api/                  # .NET 9 + PostgreSQL
│       ├── AuthService.Api.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Dockerfile
│       ├── Controllers/
│       │   ├── AuthController.cs         # POST /api/auth/login, /register
│       │   └── AdminController.cs        # POST /api/admin/accounts
│       ├── Models/
│       │   ├── Entities/
│       │   │   └── UserAccount.cs
│       │   └── DTOs/
│       │       ├── LoginRequest.cs
│       │       ├── LoginResponse.cs
│       │       ├── RegisterRequest.cs
│       │       └── CreateAccountRequest.cs
│       ├── Data/
│       │   └── AppDbContext.cs
│       └── Services/
│           ├── IAuthService.cs / AuthService.cs
│           └── IJwtService.cs / JwtService.cs
├── operational-system/
│   ├── OperationalSystem.sln
│   ├── OperationalSystem.Api/            # .NET 9 + PostgreSQL
│   │   ├── OperationalSystem.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Dockerfile
│   │   ├── Controllers/
│   │   │   ├── ProductsController.cs     # CRUD
│   │   │   └── IntegrationController.cs  # Cross-system endpoints
│   │   ├── Models/
│   │   │   ├── Entities/
│   │   │   │   └── Product.cs
│   │   │   └── DTOs/
│   │   │       ├── ProductDto.cs
│   │   │       └── IntegrationDtos.cs
│   │   ├── Data/
│   │   │   └── AppDbContext.cs
│   │   └── Services/
│   │       ├── IProductService.cs / ProductService.cs
│   │       ├── IDeliverySystemClient.cs / DeliverySystemClient.cs
│   │       └── ServiceAccountTokenStore.cs
│   └── operational-frontend/            # React 18 + TypeScript + Vite
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── index.html
│       ├── vercel.json
│       └── src/
│           ├── main.tsx
│           ├── App.tsx
│           ├── api/
│           │   ├── authApi.ts
│           │   └── operationalApi.ts
│           ├── components/
│           │   ├── LoginPage.tsx
│           │   ├── Layout.tsx
│           │   ├── ProductList.tsx
│           │   └── ProductForm.tsx
│           ├── context/
│           │   └── AuthContext.tsx
│           └── types/
│               ├── auth.ts
│               └── product.ts
├── delivery-system/
│   ├── DeliverySystem.sln
│   ├── DeliverySystem.Api/               # .NET 8 + SQL Server
│   │   ├── DeliverySystem.Api.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Dockerfile
│   │   ├── Controllers/
│   │   │   ├── OrdersController.cs       # CRUD
│   │   │   └── IntegrationController.cs  # Cross-system endpoints
│   │   ├── Models/
│   │   │   ├── Entities/
│   │   │   │   └── Order.cs
│   │   │   └── DTOs/
│   │   │       ├── OrderDto.cs
│   │   │       └── IntegrationDtos.cs
│   │   ├── Data/
│   │   │   └── AppDbContext.cs
│   │   └── Services/
│   │       ├── IOrderService.cs / OrderService.cs
│   │       ├── IOperationalSystemClient.cs / OperationalSystemClient.cs
│   │       └── ServiceAccountTokenStore.cs
│   └── delivery-frontend/               # React 18 + TypeScript + Vite
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── index.html
│       ├── vercel.json
│       └── src/
│           ├── main.tsx
│           ├── App.tsx
│           ├── api/
│           │   ├── authApi.ts
│           │   └── deliveryApi.ts
│           ├── components/
│           │   ├── LoginPage.tsx
│           │   ├── Layout.tsx
│           │   ├── OrderList.tsx
│           │   └── OrderForm.tsx
│           ├── context/
│           │   └── AuthContext.tsx
│           └── types/
│               ├── auth.ts
│               └── order.ts
├── .github/workflows/
│   ├── deploy-auth.yml
│   ├── deploy-operational.yml
│   └── deploy-delivery.yml
├── README.md
├── DEPLOYMENT_GUIDE.md
└── centralized-auth-integration.md
```

## Phase-by-Phase Implementation

### Phase 1: Auth Service (.NET 9 + PostgreSQL + Scalar)

**Entity: `UserAccount`**
| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| EmployeeNumber | string (20) | Unique |
| PasswordHash | string | BCrypt |
| Name | string (100) | |
| Email | string | |
| Role | string | e.g. "Operational.Employee", "SystemAdmin", "Operational.ExternalService" |

**Endpoints**
| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | /api/auth/login | AllowAnonymous | Login, returns JWT |
| POST | /api/auth/register | AllowAnonymous | Create account (dev/testing) |
| POST | /api/admin/accounts | SystemAdmin | Create service accounts |
| GET | /api/admin/accounts | SystemAdmin | List accounts |

**JWT Config**
```
Jwt:Key = <shared-secret-32+chars>
Jwt:Issuer = CentralAuth
Jwt:Audience = InternalSystems
```

**Seed data**: SystemAdmin (admin/admin123), SVC-OPERATIONAL, SVC-DELIVERY

**API Docs**: Scalar (`app.UseScalar()`)

### Phase 2: Operational System (.NET 9 + PostgreSQL + Scalar)

**Entity: `Product`**
| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| Name | string (100) | Required |
| Description | string (500) | Optional |
| Price | decimal | Required |
| CreatedAt | DateTime | Auto-set |
| UpdatedAt | DateTime | Auto-set |

**CRUD Endpoints**
| Method | Route | Auth |
|---|---|---|
| GET | /api/products | Authorize |
| GET | /api/products/{id} | Authorize |
| POST | /api/products | Authorize |
| PUT | /api/products/{id} | Authorize |
| DELETE | /api/products/{id} | Authorize |

**Integration Endpoints**
| Method | Route | Allowed Roles |
|---|---|---|
| GET | /api/integration/products/{id} | *.ExternalService, SystemAdmin |
| GET | /api/integration/products | *.ExternalService, SystemAdmin |

**JWT validation**: Same Jwt:Key/Issuer/Audience as Auth Service — validates tokens issued by Auth Service.

**Service: `DeliverySystemClient`**: Gets service account JWT from Auth Service → calls Delivery's `/api/integration/orders/{id}`.

**API Docs**: Scalar

### Phase 3: Delivery System (.NET 8 + SQL Server + Swagger)

**Entity: `Order`**
| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| ProductId | Guid | References Operational product |
| ProductName | string (100) | Denormalized |
| Quantity | int | |
| Status | string (20) | Pending / Shipped / Delivered |
| CustomerName | string (100) | |
| CreatedAt | DateTime | Auto-set |
| UpdatedAt | DateTime | Auto-set |

**CRUD Endpoints**
| Method | Route | Auth |
|---|---|---|
| GET | /api/orders | Authorize |
| GET | /api/orders/{id} | Authorize |
| POST | /api/orders | Authorize |
| PUT | /api/orders/{id} | Authorize |
| DELETE | /api/orders/{id} | Authorize |

**Integration Endpoints**
| Method | Route | Allowed Roles |
|---|---|---|
| GET | /api/integration/orders/{id} | *.ExternalService, SystemAdmin |
| GET | /api/integration/orders | *.ExternalService, SystemAdmin |

**JWT validation**: Same shared JWT config.

**Service: `OperationalSystemClient`**: Gets service account JWT from Auth Service → calls Operational's `/api/integration/products/{id}`.

**API Docs**: Swagger (`app.UseSwagger()` + `app.UseSwaggerUI()`)

### Phase 4: Operational Frontend (React + Vite)

- **LoginPage**: calls `POST /api/auth/login` on Auth Service
- **AuthContext**: manages token storage, auto-refresh
- **Axios interceptor**: attaches Bearer token to all requests
- **ProductList**: table with all products, edit/delete buttons
- **ProductForm**: create/edit form
- **VITE_API_URL** = Operational's Azure backend URL
- **VITE_AUTH_URL** = Auth Service's Azure backend URL

### Phase 5: Delivery Frontend (React + Vite)

- **LoginPage**: calls `POST /api/auth/login` on Auth Service (same auth URL)
- **AuthContext**: same pattern
- **OrderList**: table with all orders, edit/delete buttons
- **OrderForm**: create/edit form
- **VITE_API_URL** = Delivery's Azure backend URL
- **VITE_AUTH_URL** = Auth Service's Azure backend URL

### Phase 6: Cross-System Integration

- **Service account flow**:
  1. `ServiceAccountTokenStore` logs in with `SVC-OPERATIONAL` / `SVC-DELIVERY` credentials
  2. Caches the JWT
  3. Uses it for all cross-system HTTP calls
- **Operational → Delivery**: verify order exists by calling Delivery's `/api/integration/orders/{id}`
- **Delivery → Operational**: verify product exists by calling Operational's `/api/integration/products/{id}`

### Phase 7: Docker & CI/CD

**Dockerfiles**: Multi-stage build, expose port 8080, `ASPNETCORE_URLS=http://0.0.0.0:8080`

**GitHub Actions** (3 workflows):
- `deploy-auth.yml`: build/push Auth image → deploy to ACA → no frontend
- `deploy-operational.yml`: build/push Operational image → deploy to ACA → deploy frontend to Vercel
- `deploy-delivery.yml`: build/push Delivery image → deploy to ACA → deploy frontend to Vercel

### Phase 8: Documentation

**DEPLOYMENT_GUIDE.md**: End-to-end steps for:
1. Azure resource creation (Resource Group, PostgreSQL, SQL Server, ACR ×3, ACA ×3)
2. Vercel project setup (operational-frontend, delivery-frontend)
3. GitHub Actions secrets configuration
4. Environment variables per system
5. Testing the full flow (login → CRUD → cross-system calls)
