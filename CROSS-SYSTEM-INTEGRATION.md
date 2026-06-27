# Cross-System Integration — How It Works

## The Big Picture

Imagine two separate companies that need to work together:

- **Company A (Operational System)** — manages Products (what items exist, their prices)
- **Company B (Delivery System)** — manages Orders (who ordered what, shipping status)

They don't share a database. They don't share servers. They communicate over the internet using APIs (like how your browser talks to a website).

Between them sits a **guard (Auth Service)** that issues ID cards (JWT tokens). Both companies trust the same guard.

```
         ┌──────────────────┐
         │   Auth Service    │  ← The guard that issues ID cards
         │  (Issue JWTs)     │
         └──────┬───────────┘
                │
     ┌──────────┴──────────────────┐
     │                             │
┌────▼──────────┐          ┌──────▼───────────┐
│ Company A     │          │ Company B         │
│ (Operational) │◄────────►│ (Delivery)        │
│ Products      │  JWT     │ Orders            │
│ View Orders ◄─┘ call    └──► Products dropdown
└───────────────┘                          │
     │                                     │
     │  Integration:                       │
     │  • Operational → Delivery: fetch orders (Orders tab)
     │  • Delivery → Operational: fetch products (dropdown)
     └─────────────────────────────────────┘
```

**Two-way integration:**
- **Delivery fetches Products** from Operational to populate the order form dropdown
- **Operational fetches Orders** from Delivery to display in the "Orders" tab

---

## 1. Shared Authentication (The ID Card System)

### How users log in

Every user (admin, employee, etc.) is stored in the **Auth Service**, not in Company A or B. When you log in:

1. You type your username and password into the frontend
2. The frontend sends this to the **Auth Service** (`POST /api/auth/login`)
3. The Auth Service checks your credentials and issues a **JWT token** (a digital ID card)
4. Your browser stores this token and sends it with every request

### What's inside the JWT

```json
{
  "role": "SystemAdmin",
  "name": "System Admin",
  "employeeNumber": "admin"
}
```

Both Company A and Company B **validate** this token using the same secret key. If the token is valid, they trust whoever sent it. If the token is missing or invalid, they reject the request with **401 Unauthorized**.

**Analogy:** The JWT is like a company ID badge. Both buildings trust badges issued by HR (Auth Service) because they recognize the seal and signature.

---

## 2. Service Accounts — Why Systems Can't Use Human Logins

### The problem

When the Operational backend needs to call the Delivery backend, it can't use a human employee's JWT because:
- The employee might log out — their token expires and the system breaks
- The employee might not have the right permissions
- What if the call happens at 3 AM when no one is logged in?

### The solution: Service Accounts

A **service account** is a machine-only account stored in the Auth Service. It's not a real person — it's a bot.

| Account | Password | Role | Used By |
|---|---|---|---|
| `SVC-OPERATIONAL` | `svc-operational-pwd` | `Operational.ExternalService` | Operational's backend to call Delivery |
| `SVC-DELIVERY` | `svc-delivery-pwd` | `Delivery.ExternalService` | Delivery's backend to call Operational |

### Why log in at all? Why not just use an API key?

Because service accounts integrate with the **same JWT system** that humans use. There's no separate auth mechanism — the same `Authorization: Bearer <token>` header works for both. The only difference is the `role` claim:

| Who logs in | Role in JWT | Result |
|---|---|---|
| Human `admin` | `SystemAdmin` | Can access everything |
| Bot `SVC-DELIVERY` | `Delivery.ExternalService` | Can only call integration endpoints |
| Human `Operational.Employee` | `Operational.Employee` | Can access Operational but NOT integration endpoints |

### How the backend logs in as a service account

This is **not** a user typing in a browser. It's code that runs inside the backend:

```csharp
// Inside DeliverySystemClient.cs (Operational's backend)
private async Task EnsureTokenAsync()
{
    // 1. If we already have a valid token, use it (cache)
    if (_tokenStore.IsValid) return;

    // 2. Log in to Auth Service with the service account credentials
    var response = await _httpClient.PostAsync(
        $"{_configuration["AuthService:BaseUrl"]}/api/auth/login",
        new { employeeNumber = "SVC-OPERATIONAL", password = "..." });

    // 3. Store the JWT for reuse until it expires
    _tokenStore.SetToken(response.accessToken);
}
```

The JWT is cached in memory and reused for multiple calls until it expires (after 55 minutes), then a new one is fetched.

---

## 3. How SystemClients Work (The Integration Bridge)

### What is a SystemClient?

A **SystemClient** is a C# class that acts as a **bridge** between two systems. Each system has one:

| System | Its SystemClient | What it calls |
|---|---|---|
| **Operational** | `DeliverySystemClient` | Delivery's `/api/integration/orders` |
| **Delivery** | `OperationalSystemClient` | Operational's `/api/integration/products` |

### The anatomy of a SystemClient

Every SystemClient follows the same pattern:

```
┌─────────────────────────────────────────┐
│            SystemClient                  │
│                                         │
│  1. EnsureTokenAsync()                   │
│     → Login to Auth Service as SVC-???   │
│     → Cache the JWT                      │
│                                         │
│  2. CallApiAsync()                       │
│     → Attach JWT as Bearer token        │
│     → Call the other system's endpoint   │
│     → Return the response               │
│                                         │
│  3. Error handling                      │
│     → Return null or empty list on fail │
└─────────────────────────────────────────┘
```

### End-to-end walkthrough: Operational fetches Orders from Delivery

Here's exactly what happens when you click the **"Orders (from Delivery)"** tab in the Operational frontend:

```
Step 1: User clicks "Orders" tab in Operational Frontend
         │
         ▼
Step 2: Operational Frontend makes HTTP request:
         GET https://operational-api.azure/.../api/orders
         Authorization: Bearer <user's JWT>
         │
         ▼
Step 3: Operational Backend's OrdersController receives the request
         - Validates the user's JWT ✅
         - Calls _deliveryClient.GetAllOrdersAsync()
         │
         ▼
Step 4: DeliverySystemClient.EnsureTokenAsync():
         - Checks if SVC-OPERATIONAL's JWT is cached → No
         - POST https://auth-api.azure/.../api/auth/login
           { employeeNumber: "SVC-OPERATIONAL", password: "..." }
         - Auth Service returns JWT with role "Operational.ExternalService"
         - Caches the JWT
         │
         ▼
Step 5: DeliverySystemClient.GetAllOrdersAsync():
         - GET https://delivery-api.azure/.../api/integration/orders
           Authorization: Bearer <SVC-OPERATIONAL's JWT>
         │
         ▼
Step 6: Delivery's IntegrationController checks the JWT:
         - Role is "Operational.ExternalService" ✅
         - Endpoint requires "Operational.ExternalService or SystemAdmin"
         - Matches! ✅
         │
         ▼
Step 7: Delivery's OrderService queries SQL Server:
         SELECT * FROM Orders
         │
         ▼
Step 8: Order data flows back:
         Delivery SQL Server → Delivery Controller → HTTP Response
         → Operational's DeliverySystemClient → Operational Controller
         → Operational Frontend → Table in browser
```

### End-to-end walkthrough: Delivery fetches Products from Operational

Here's what happens when you open the **"New Order"** form in the Delivery frontend:

```
Step 1: User clicks "+ New Order" in Delivery Frontend
         │
         ▼
Step 2: Delivery Frontend makes HTTP request:
         GET https://delivery-api.azure/.../api/products
         Authorization: Bearer <user's JWT>
         │
         ▼
Step 3: Delivery Backend's ProductsController receives the request
         - Validates the user's JWT ✅
         - Calls _operationalClient.GetAllProductsAsync()
         │
         ▼
Step 4: OperationalSystemClient.EnsureTokenAsync():
         - POST https://auth-api.azure/.../api/auth/login
           { employeeNumber: "SVC-DELIVERY", password: "..." }
         - Auth Service returns JWT with role "Delivery.ExternalService"
         │
         ▼
Step 5: OperationalSystemClient.GetAllProductsAsync():
         - GET https://operational-api.azure/.../api/integration/products
           Authorization: Bearer <SVC-DELIVERY's JWT>
         │
         ▼
Step 6: Operational's IntegrationController checks the JWT:
         - Role is "Delivery.ExternalService" ✅
         │
         ▼
Step 7: Operational's ProductService queries PostgreSQL:
         SELECT * FROM Products
         │
         ▼
Step 8: Product data flows back:
         Operational PostgreSQL → Operational Controller → HTTP Response
         → Delivery's OperationalSystemClient → Delivery Controller
         → Delivery Frontend → Dropdown list
```

---

## 4. The Two Integration Flows Side by Side

```
┌──────────────────────────────────────────────────────────────────────┐
│                        Auth Service                                  │
│                    (issues JWTs to everyone)                         │
└────┬──────────┬──────────────────────────────┬──────────┬────────────┘
     │          │                              │          │
     │   ┌──────▼──────┐              ┌───────▼──────┐   │
     │   │ SVC-OPERAT. │              │ SVC-DELIVERY  │   │
     │   │ logs in     │              │ logs in       │   │
     │   └──────┬──────┘              └───────┬──────┘   │
     │          │                              │          │
┌────▼──────────▼────┐              ┌─────────▼──────────▼──┐
│  Operational System │              │  Delivery System      │
│                     │              │                       │
│  Products CRUD      │  ◄────────── │  Products dropdown    │
│  (PostgreSQL)       │  JWT call    │  (fetches from Ops)   │
│                     │              │                       │
│  Orders viewer      │  ──────────► │  Orders CRUD          │
│  (fetches from Del) │  JWT call    │  (SQL Server)         │
│                     │              │                       │
│  DeliverySystemCl.  │              │  OperationalSystemCl. │
└─────────────────────┘              └───────────────────────┘
```

---

## 5. The Role Prefix Security Model

Each integration endpoint checks the JWT's `role` claim before allowing access.

### Operational's `/api/integration/products`

| Allowed Roles | Who can call |
|---|---|
| `Delivery.ExternalService` | Delivery's backend (via SVC-DELIVERY) |
| `SystemAdmin` | Human admin |

### Delivery's `/api/integration/orders`

| Allowed Roles | Who can call |
|---|---|
| `Operational.ExternalService` | Operational's backend (via SVC-OPERATIONAL) |
| `SystemAdmin` | Human admin |

### What happens with wrong roles?

| Caller | Role | Allowed? |
|---|---|---|
| Delivery backend calling Operational | `Delivery.ExternalService` | ✅ Yes |
| Delivery's product dropdown | `Operational.Employee` | ❌ No (would be 403 Forbidden) |
| SVC-OPERATIONAL calling Delivery | `Operational.ExternalService` | ✅ Yes |
| SVC-OPERATIONAL calling Operational | `Operational.ExternalService` | ❌ No (Operational only allows `Delivery.ExternalService`) |

The role prefix **prevents reverse-calling**: SVC-OPERATIONAL can only call Delivery's APIs, not Operational's own integration APIs.

---

## 6. JWT Sharing Across Different Technologies

| System | Framework | Database |
|---|---|---|
| Auth Service | .NET 9 | PostgreSQL |
| Operational System | .NET 9 | PostgreSQL |
| Delivery System | .NET 8 | Microsoft SQL Server |

Yet they all work together seamlessly because:

- **JWT is a text format** — base64-encoded JSON with a signature. It doesn't care about .NET versions.
- **HTTP and JSON** are universal. A .NET 8 app sending JSON to a .NET 9 app works identically.
- **HMAC-SHA256** is the same algorithm in any .NET version.

**Analogy:** It doesn't matter if one office uses Windows and the other uses Mac. When they send an email (HTTP) with an attachment (JWT), both can read it because email is a standard format.

---

## 7. Summary

| Concept | What it is | Why it matters |
|---|---|---|
| **JWT Token** | Digital ID card issued by Auth Service | Proves who you are across all systems |
| **Service Account** | A bot (`SVC-*`) for machine-to-machine calls | Systems can talk without a human logged in |
| **Why login as SVC?** | Same JWT system as humans, different role | No separate auth mechanism needed; role controls access |
| **SystemClient** | A C# class that logs in as SVC and calls APIs | Clean abstraction — controllers just call methods |
| **Role Prefix** | e.g. `Operational.ExternalService` | Prevents systems from accessing their own integration APIs |
| **Integration Endpoint** | `/api/integration/*` | Only accessible by service accounts and admins |
| **CORS** | Allows frontend on Vercel to call backend on Azure | Without it, browsers block cross-domain requests |
| **Shared Key** | Same `Jwt:Key` in all 3 backends | Ensures all systems trust the same tokens |

The key insight: **each system is completely independent** with its own database, deployment, and codebase. Integration happens purely through **API calls + shared JWT tokens** — no shared databases, no shared code, just agreed-upon contracts (the JSON format of requests/responses).
