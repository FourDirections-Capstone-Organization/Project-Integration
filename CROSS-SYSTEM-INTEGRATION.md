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
     ┌──────────┴──────────┐
     │                     │
┌────▼────┐         ┌─────▼─────┐
│Company A│◄────────┤ Company B │
│Products │ JWT     │  Orders   │
└─────────┘ call    └───────────┘
```

---

## 1. Shared Authentication (The ID Card System)

### How users log in

Every user (admin, employee, etc.) is stored in the **Auth Service**, not in Company A or B. When you log in:

1. You type your username and password into the frontend
2. The frontend sends this to the **Auth Service** (`POST /api/auth/login`)
3. The Auth Service checks your credentials and issues a **JWT token** (a digital ID card)
4. Your browser stores this token and sends it with every request

### What's inside the JWT

The token contains your identity claims:

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

## 2. Cross-System API Calls (The Service Account Pattern)

Sometimes Company A needs information from Company B, and vice versa. But they can't just use a human's login — they need a **machine account** (service account) that represents the company itself.

### Service accounts

| Account Name | Password | Role | Purpose |
|---|---|---|---|
| `SVC-OPERATIONAL` | `svc-operational-pwd` | `Operational.ExternalService` | Used by Company A to call Company B |
| `SVC-DELIVERY` | `svc-delivery-pwd` | `Delivery.ExternalService` | Used by Company B to call Company A |

These are not real people — they are **bots** stored in the Auth Service.

### How a cross-system call works

Here's what happens when **Company B (Delivery)** needs a list of products from **Company A (Operational)**:

```
1. Delivery Frontend needs to show a product dropdown
          │
2. Delivery Frontend calls: GET /api/products
          │                    (its own backend)
          ▼
3. Delivery Backend logs into Auth Service as "SVC-DELIVERY"
          │  POST /api/auth/login { employeeNumber: "SVC-DELIVERY", password: "..." }
          ▼
4. Auth Service returns a JWT with role "Delivery.ExternalService"
          │
5. Delivery Backend calls Operational's integration endpoint:
          │  GET https://operational-api.azure/.../api/integration/products
          │  Authorization: Bearer <JWT>
          ▼
6. Operational Backend checks the JWT role:
   - Is it "Delivery.ExternalService"? → Yes → ✅ Allowed
   - Would reject if role was "Operational.Employee" → ❌ Denied
          │
7. Operational returns the product list
          │
8. Delivery Frontend shows products in a dropdown
```

### Why the role prefix matters

The role `Delivery.ExternalService` means: *"I am an external service coming from the Delivery system."* 

Operational's integration endpoints are configured to only accept:

| Allowed Roles | Meaning |
|---|---|
| `Delivery.ExternalService` | Delivery's bot can call us |
| `SystemAdmin` | Human admins can also call us |

If a regular employee tried to call this endpoint, their role would be `Operational.Employee` or `Delivery.Dispatcher` — neither of which matches `Delivery.ExternalService` or `SystemAdmin`. The request would be rejected.

This prevents:
- Regular users from accidentally calling system-to-system APIs
- One company's users from accessing another company's internal data without permission

---

## 3. JWT Sharing Across Different Technologies

This project uses **three different database technologies**:

| System | Framework | Database |
|---|---|---|
| Auth Service | .NET 9 | PostgreSQL |
| Operational System | .NET 9 | PostgreSQL |
| Delivery System | .NET 8 | Microsoft SQL Server |

Yet they all work together seamlessly. Why?

- **JWT is a text format** (base64-encoded JSON with a signature). It doesn't care about .NET versions or database types.
- **HTTP and JSON** are universal standards. A .NET 8 app sending JSON to a .NET 9 app works identically to same-version communication.
- **The JWT signing algorithm** (HMAC-SHA256) is the same in any .NET version.

**Analogy:** It doesn't matter if one office uses Windows and the other uses Mac. When they send an email (HTTP) with an attachment (JWT), both can read it because email is a standard format.

---

## 4. The Product Dropdown — Integration in Action

The most visible example of cross-system integration is the **product dropdown** in the Delivery frontend.

### Before integration
When creating an order, you had to **manually type** the product name:
```
Product Name: [_________________]  ← Typed by hand, error-prone
```

### After integration
When creating an order, the delivery system **fetches** products from the operational system:

```
Product: [Widget ($19.99)  ▼]     ← Dropdown, fetched in real-time
         Widget ($19.99)
         Gadget ($9.99)
         Super Widget ($29.99)
```

When you select a product:
- `productId` is set automatically (e.g., `3a4b5c6d-...`)
- `productName` is set automatically (e.g., "Widget")

The order is then saved in Delivery's SQL Server database with the correct product reference. If you later view the order in the Operational system, it can look up the product details using the shared `productId`.

### The full flow step-by-step

```
[User clicks "New Order" in Delivery Frontend]
        │
        ▼
[Delivery Frontend calls Delivery Backend] → GET /api/products
        │
        ▼
[Delivery Backend calls Auth Service] → Login as SVC-DELIVERY
        │
        ▼
[Auth Service returns JWT with role "Delivery.ExternalService"]
        │
        ▼
[Delivery Backend calls Operational Backend] → GET /api/integration/products
        │  Authorization: Bearer <JWT from SVC-DELIVERY>
        ▼
[Operational checks role = "Delivery.ExternalService"] → ✅ Allowed
        │
        ▼
[Operational queries PostgreSQL database] → SELECT * FROM Products
        │
        ▼
[Product list flows back: Operational → Delivery Backend → Delivery Frontend]
        │
        ▼
[User sees dropdown with product names and prices]
```

---

## 5. Summary

| Concept | What it is | Why it matters |
|---|---|---|
| **JWT Token** | Digital ID card issued by Auth Service | Proves who you are across all systems |
| **Service Account** | A bot account for system-to-system calls | Lets machines talk without human login |
| **Role Prefix** | e.g. `Operational.ExternalService` | Controls which systems can call which APIs |
| **Integration Endpoint** | `/api/integration/*` | Special endpoints for cross-system communication only |
| **CORS** | Allows frontend on Vercel to call backend on Azure | Without it, browsers block cross-domain requests |
| **Shared Key** | Same `Jwt:Key` in all 3 backends | Ensures all systems trust the same tokens |

The key insight: **each system is completely independent**. They have their own:
- Database (PostgreSQL vs SQL Server)
- Deployment (Azure Container Apps)
- Codebase (.NET 9 vs .NET 8)

The integration happens purely through **API calls + shared JWT tokens**. No shared databases, no shared code — just agreed-upon contracts (the JSON format of the API requests/responses).
