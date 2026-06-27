# Centralized Authentication & System Integration Guide

## Table of Contents

- [1. Overview](#1-overview)
- [2. Architecture Diagram](#2-architecture-diagram)
- [3. Tech Stack Compatibility](#3-tech-stack-compatibility)
- [4. The Shared JWT Contract](#4-the-shared-jwt-contract)
- [5. The Auth Service](#5-the-auth-service)
  - [What it is](#what-it-is)
  - [Endpoints](#endpoints)
  - [How employees are created](#how-employees-are-created)
- [6. How Users Log In](#6-how-users-log-in)
- [7. How Systems Communicate (System-to-System)](#7-how-systems-communicate-system-to-system)
  - [What is a Service Account?](#what-is-a-service-account)
  - [Example: Operational calls Delivery](#example-operational-calls-delivery)
  - [Example: Delivery calls Operational](#example-delivery-calls-operational)
- [8. Setting Up Service Accounts Between Systems](#8-setting-up-service-accounts-between-systems)
- [9. Role-Based Access Per System](#9-role-based-access-per-system)
- [10. Integration Endpoints (Data Contract)](#10-integration-endpoints-data-contract)
  - [Operational System exposes](#operational-system-exposes)
  - [Delivery System exposes](#delivery-system-exposes)
  - [Finance System exposes](#finance-system-exposes)
- [11. Step-by-Step: What Each Group Must Do](#11-step-by-step-what-each-group-must-do)
  - [Step 1: Agree on the shared JWT key and claim names](#step-1-agree-on-the-shared-jwt-key-and-claim-names)
  - [Step 2: Each team builds or configures JWT validation](#step-2-each-team-builds-or-configures-jwt-validation)
  - [Step 3: Create service accounts for cross-system communication](#step-3-create-service-accounts-for-cross-system-communication)
  - [Step 4: Build integration endpoints](#step-4-build-integration-endpoints)
  - [Step 5: Test end-to-end](#step-5-test-end-to-end)
- [12. FAQ](#12-faq)

---

## 1. Overview

This project uses a **centralized Authentication System** that all independent systems share. Instead of each system having its own login page and user database, there is one Auth Service that handles authentication for everyone. Once a user logs in, their JWT token works across all systems that are part of the integration.

The architecture involves four systems:

| System | Role | Built by |
|---|---|---|
| **Auth Service** | Centralized authentication — login, account management, JWT issuance | Your team |
| **Operational System** | Main operational task management (your existing system) | Your team |
| **Delivery Management System** | Delivery routing, dispatching, tracking | Other group |
| **Finance System** | Invoicing, payments, billing | Other group |

Each system is **independently deployed** with its own database. They communicate over HTTPS via REST APIs. No shared database.

---

## 2. Architecture Diagram

```
User's Browser (Operational)
     │
     │  POST /api/authentication/login
     │  { employeeNumber, password }
     ▼
┌──────────────────────────────────────────────────────┐
│                AUTH SERVICE (standalone)               │
│                auth.speedex.com                        │
│                                                        │
│  ┌─────────────────────────────────────────────────┐  │
│  │  Its own PostgreSQL DB                          │  │
│  │  ├─ Employees (ALL users across all systems)    │  │
│  │  │  ├─ John (SystemAdmin)                       │  │
│  │  │  ├─ Jane (Operational.Employee)              │  │
│  │  │  ├─ Mike (Delivery.Dispatcher)               │  │
│  │  │  └─ Anna (Finance.Accountant)                │  │
│  │  ├─ SVC-OPERATIONAL (service account)           │  │
│  │  ├─ SVC-DELIVERY (service account)              │  │
│  │  └─ SVC-FINANCE (service account)               │  │
│  └─────────────────────────────────────────────────┘  │
│                                                        │
│  Issues JWT signed with SHARED KEY                     │
└────────────────────────┬───────────────────────────────┘
                         │
              Same JWT works everywhere
                         │
         ┌───────────────┼───────────────┐
         │               │               │
         ▼               ▼               ▼
┌────────────────┐ ┌────────────┐ ┌────────────┐
│ Operational    │ │ Delivery   │ │ Finance    │
│ System         │ │ System     │ │ System     │
│ ops.speedex.com │ │ deliv.speedex│ │ fin.speedex │
│                │ │            │ │            │
│ Its own DB     │ │ Its own DB │ │ Its own DB │
│ (PostgreSQL)   │ │ (SQL Srvr) │ │ (SQL Srvr) │
│                │ │            │ │            │
│ Validates JWT  │ │ Validates  │ │ Validates  │
│ with shared key│ │ JWT w/     │ │ JWT w/     │
│                │ │ shared key │ │ shared key │
│ Role prefix:   │ │ Role prefix│ │ Role prefix│
│ "Operational." │ │ "Delivery."│ │ "Finance." │
│                │ │            │ │            │
│ Service acct:  │ │ Service    │ │ Service    │
│ SVC-OPERATIONS │ │ acct:      │ │ acct:      │
│                │ │ SVC-DELIVERY│ │ SVC-FINANCE│
└───────┬────────┘ └──────┬─────┘ └──────┬─────┘
        │                 │              │
        │   They call each other's APIs  │
        │   using service account JWTs   │
        └────────────────►◄──────────────┘
```

---

## 3. Tech Stack Compatibility

The systems may use different tech stacks. **This is not a problem.**

| Concern | Why it works |
|---|---|
| **Different .NET versions** (.NET 9 vs .NET 8) | HTTP, JSON, and JWT are **standard protocols**, not framework features. A .NET 8 server calling a .NET 9 API works identically to .NET 9→.NET 9. The `System.IdentityModel.Tokens.Jwt` library exists in both versions and produces the same JWT format. |
| **Different databases** (PostgreSQL vs SQL Server) | Each system has its own database. They **never access each other's databases directly**. Database choice is internal to each system. Communication happens only through REST API calls. |
| **JWT signing and validation** | All systems use HMAC-SHA256, which is the same algorithm regardless of framework version. As long as the secret key (`Jwt:Key`) is identical, a JWT signed by the Auth Service (.NET 9) can be validated by Delivery System (.NET 8) without any issues. |
| **JSON serialization** | `System.Text.Json` in .NET 8 and .NET 9 are compatible. JSON is a language-agnostic format. Even a Python or Node.js system could participate. |

**The only requirement:** All systems must agree on:
- The exact **Jwt:Key** value
- The **claim names** in the JWT (e.g., `role`, `email`, `employeeId`)
- The **endpoint contracts** (request/response JSON shapes)

These are documented in this guide and must be coordinated during the integration phase.

---

## 4. The Shared JWT Contract

Every JWT issued by the Auth Service contains these claims:

| Claim | Type | Example | Purpose |
|---|---|---|---|
| `sub` | string (GUID) | `"3a4b5c6d-7e8f-..."` | Unique account identifier |
| `email` | string | `"jane@speedex.com"` | Employee's email |
| `role` | string | `"Operational.Employee"` | Role **with system prefix** (see Section 9) |
| `employeeId` | string | `"EMP-001"` | Employee number for display |
| `name` | string | `"Jane Doe"` | Display name |
| `employeeNumber` | string | `"EMP-001"` | Same as employeeId, for login reference |
| `exp` | number (Unix) | `1750000000` | Expiration timestamp |
| `iss` | string | `"CentralAuth"` | Issuer — must be **same across all systems** |
| `aud` | string | `"InternalSystems"` | Audience — must be **same across all systems** |

### Configuration all systems must set

```json
{
  "Jwt": {
    "Key": "<same-key-for-all-systems>",
    "Issuer": "CentralAuth",
    "Audience": "InternalSystems",
    "AccessTokenExpirationMinutes": 15
  }
}
```

Any system that receives a JWT validates it against these same values. If the `iss` or `aud` don't match, validation fails.

---

## 5. The Auth Service

### What it is

A standalone ASP.NET Core Web API that your team builds. Its **only job** is authentication and account management. It has its own PostgreSQL database containing:

| Table | Purpose |
|---|---|
| `Employees` | All users across all systems (Operational, Delivery, Finance) + service accounts |
| `Accounts` | Login credentials (password hash, role) linked to each employee |
| `RefreshTokens` | JWT refresh tokens |

### Endpoints

| Method | Route | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/authentication/login` | AllowAnonymous | Login with employeeNumber + password, returns JWT |
| `POST` | `/api/authentication/refresh` | AllowAnonymous | Exchange refresh token for new JWT pair |
| `POST` | `/api/admin/accounts` | SystemAdmin only | Create employee or service account |
| `GET` | `/api/admin/accounts` | SystemAdmin only | List all accounts |
| `PUT` | `/api/admin/accounts/{id}/deactivate` | SystemAdmin only | Deactivate an account |

### How employees are created

Employees are **not** self-registered. A System Admin creates accounts through the admin endpoint:

```json
POST /api/admin/accounts
Authorization: Bearer <admin-jwt>

{
  "firstName": "Mike",
  "lastName": "Santos",
  "email": "mike@delivery.com",
  "employeeID": "DEL-001",
  "role": "Delivery.Dispatcher",
  "password": "temporary-password-change-me"
}
```

The Auth Service stores the employee record and returns the details. The admin then shares the credentials with the employee securely.

---

## 6. How Users Log In

The frontend of any system calls the **Auth Service directly** — not through its own backend.

```
User visits Operational System's login page
        │
        │  Frontend calls:
        ▼
POST https://auth.speedex.com/api/authentication/login
{ "employeeNumber": "EMP-001", "password": "..." }
        │
        ▼
Auth Service validates credentials
        │
        ▼
Response 200:
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "...",
  "role": "Operational.Employee",
  "employeeName": "Jane Doe",
  "employeeID": "EMP-001",
  ...
}
        │
        ▼
Frontend stores JWT in localStorage

        │
        ▼
Frontend calls Operational System's API:
GET https://ops.speedex.com/api/shipments
Authorization: Bearer <jwt>
        │
        ▼
Operational System validates JWT with shared key
→ role prefix is "Operational." → allowed
→ Returns shipment data
```

### What each system's frontend needs to do

Each system's login page must point to the Auth Service's URL, not its own backend:

```javascript
// Instead of: fetch('/api/authentication/login', ...)
// Use:
const response = await fetch('https://auth.speedex.com/api/authentication/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ employeeNumber, password })
});
```

The rest (storing token, attaching to requests, auto-refresh) works the same as described in the [JWT Authentication Guide](./jwt-authentication-guide.md).

---

## 7. How Systems Communicate (System-to-System)

### What is a Service Account?

A **service account** is a non-human account stored in the Auth Service, just like any employee. But instead of a real person logging in, it's a **backend system** that logs in programmatically to get a JWT for calling another system's API.

| Human account | Service account |
|---|---|
| `employeeID: "EMP-001"` | `employeeID: "SVC-OPERATIONS"` |
| `role: "Operational.Employee"` | `role: "Operational.ExternalService"` |
| Logs in via browser | Logs in via backend code |
| Has a real name and email | Name describes the system (e.g., "Operations System Service Account") |
| Can access dashboards and UI | Can ONLY call integration endpoints |

### Example: Operational calls Delivery

```
Operational System needs to notify Delivery that a shipment is ready
        │
        ▼
Operational's backend calls Auth Service:
POST https://auth.speedex.com/api/authentication/login
{ "employeeNumber": "SVC-OPERATIONS", "password": "<stored-securely>" }
        │
        ▼
Auth Service returns JWT:
{ "role": "Operational.ExternalService", ... }
        │
        ▼
Operational's backend calls Delivery's API:
POST https://deliv.speedex.com/api/integration/deliveries
Authorization: Bearer <jwt-from-above>
{
  "trackingNumber": "SPX-001",
  "pickupAddress": "123 Main St",
  "deliveryAddress": "456 Oak Ave",
  "status": "ready-for-dispatch"
}
        │
        ▼
Delivery System validates JWT with shared key
→ role is "Operational.ExternalService"
→ Role prefix is "Operational." → allowed (Delivery trusts Operational)
→ Processes the request, assigns a driver
```

### Example: Delivery calls Operational

```
Delivery System has completed a delivery
        │
        ▼
Delivery's backend calls Auth Service:
POST https://auth.speedex.com/api/authentication/login
{ "employeeNumber": "SVC-DELIVERY", "password": "<stored-securely>" }
        │
        ▼
Auth Service returns JWT:
{ "role": "Delivery.ExternalService", ... }
        │
        ▼
Delivery's backend calls Operational's API:
POST https://ops.speedex.com/api/integration/shipments/status
Authorization: Bearer <jwt-from-above>
{
  "trackingNumber": "SPX-001",
  "status": "delivered",
  "deliveredAt": "2026-06-25T14:30:00Z",
  "recipientName": "Juan Dela Cruz"
}
        │
        ▼
Operational System validates JWT with shared key
→ role is "Delivery.ExternalService"
→ Role prefix is "Delivery." → allowed (Operational trusts Delivery)
→ Updates shipment status, triggers notifications
```

---

## 8. Setting Up Service Accounts Between Systems

Service account credentials are exchanged **between system admins**.

### Step-by-step

```
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│  1. Operational Admin creates a service account for Delivery:    │
│     POST https://auth.speedex.com/api/admin/accounts             │
│     {                                                            │
│       "employeeID": "SVC-DELIVERY",                              │
│       "role": "Operational.ExternalService",                     │
│       "password": "generated-secure-password-abc123",            │
│       ...                                                        │
│     }                                                            │
│                                                                  │
│  2. Operational Admin shares with Delivery Admin:                │
│     "We have created a service account for your system:          │
│      EmployeeID: SVC-DELIVERY, Password: generated-secure-..."   │
│     "Your system will use this to call our APIs."                │
│                                                                  │
│  3. Delivery Admin creates a service account for Operational:    │
│     POST https://auth.speedex.com/api/admin/accounts             │
│     {                                                            │
│       "employeeID": "SVC-OPERATIONS",                            │
│       "role": "Delivery.ExternalService",                        │
│       ...                                                         │
│     }                                                            │
│                                                                  │
│  4. Delivery Admin shares with Operational Admin:                │
│     "We have created a service account for your system:          │
│      EmployeeID: SVC-OPERATIONS, Password: ..."                  │
│     "Your system will use this to call our APIs."                │
│                                                                  │
│  5. Both teams configure these credentials in their appsettings  │
│     (encrypted or via environment variables)                     │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### What each system configures

```json
{
  "AuthService": {
    "BaseUrl": "https://auth.speedex.com"
  },
  "ExternalSystems": {
    "Delivery": {
      "BaseUrl": "https://deliv.speedex.com",
      "ServiceAccountEmployeeID": "SVC-OPERATIONS",
      "ServiceAccountPassword": "<encrypted-or-env-var>"
    },
    "Finance": {
      "BaseUrl": "https://fin.speedex.com",
      "ServiceAccountEmployeeID": "SVC-OPERATIONS",
      "ServiceAccountPassword": "<encrypted-or-env-var>"
    }
  }
}
```

---

## 9. Role-Based Access Per System

Each system checks the `role` claim in the JWT to decide if a request is allowed.

### Role naming convention

Roles follow the format: **`<System>.<Privilege>`**

| Role | Who has it | Can access |
|---|---|---|
| `SystemAdmin` | System admins | **Everything** — all systems |
| `Operational.Employee` | Regular employees | Operational System only |
| `Operational.Manager` | Managers | Operational System + elevated actions |
| `Operational.ExternalService` | Service accounts | Operational System's integration endpoints only |
| `Delivery.Dispatcher` | Delivery dispatchers | Delivery System only |
| `Delivery.Driver` | Delivery drivers | Delivery System (limited view) |
| `Delivery.ExternalService` | Service accounts | Delivery System's integration endpoints only |
| `Finance.Accountant` | Finance employees | Finance System only |
| `Finance.ExternalService` | Service accounts | Finance System's integration endpoints only |

### How a system validates

When Operational System receives an API request, it checks:

```csharp
[Authorize] // Must have a valid JWT first
public class IntegrationController : ControllerBase
{
    [HttpPost("shipments/status")]
    public IActionResult UpdateShipmentStatus(...)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

        // Only allow service accounts or SystemAdmin
        if (role != "Operational.ExternalService" && role != "SystemAdmin")
            return Forbid();

        // Process the request...
    }
}
```

Or more simply at the controller level:

```csharp
[Authorize(Roles = "Operational.ExternalService,SystemAdmin")]
[Route("api/integration")]
public class IntegrationController : ControllerBase
{
    // All endpoints here require Operational.ExternalService or SystemAdmin
}
```

### The `SystemAdmin` exception

The `SystemAdmin` role has access to **all systems**. This allows:
- Cross-system troubleshooting
- Admin UI that shows data from multiple systems
- Creating accounts for employees in any system

---

## 10. Integration Endpoints (Data Contract)

These endpoints are defined for each system. All systems must agree on the JSON format.

### Operational System exposes

| Method | Route | Allowed roles | Description |
|---|---|---|---|
| `GET` | `/api/integration/shipments` | `*.ExternalService`, `SystemAdmin` | Fetch shipments with optional filters |
| `POST` | `/api/integration/shipments/status` | `*.ExternalService`, `SystemAdmin` | Update shipment status (e.g., delivered) |
| `GET` | `/api/integration/employees/{employeeId}` | `*.ExternalService`, `SystemAdmin` | Verify an employee exists |
| `POST` | `/api/integration/delivery/assign` | `*.ExternalService`, `SystemAdmin` | Assign a courier to a delivery |

**GET /api/integration/shipments**
```
Request:  ?status=pending&page=1&pageSize=10
Response: {
  "isSuccess": true,
  "data": {
    "items": [
      {
        "trackingNumber": "SPX-001",
        "status": "pending",
        "pickupAddress": "123 Main St",
        "deliveryAddress": "456 Oak Ave",
        "createdAt": "2026-06-25T10:00:00Z"
      }
    ],
    "totalCount": 42,
    "page": 1,
    "pageSize": 10
  }
}
```

**POST /api/integration/shipments/status**
```
Request: {
  "trackingNumber": "SPX-001",
  "status": "delivered",
  "deliveredAt": "2026-06-25T14:30:00Z",
  "recipientName": "Juan Dela Cruz",
  "signature": "data:image/png;base64,..."
}
Response: { "isSuccess": true, "message": "Status updated" }
```

### Delivery System exposes

| Method | Route | Allowed roles | Description |
|---|---|---|---|
| `GET` | `/api/integration/deliveries/{id}` | `*.ExternalService`, `SystemAdmin` | Get delivery status and driver info |
| `POST` | `/api/integration/deliveries/complete` | `*.ExternalService`, `SystemAdmin` | Notify that delivery is completed |
| `GET` | `/api/integration/tracking/{trackingNumber}` | `*.ExternalService`, `SystemAdmin` | Real-time tracking data |

### Finance System exposes

| Method | Route | Allowed roles | Description |
|---|---|---|---|
| `POST` | `/api/integration/invoices` | `*.ExternalService`, `SystemAdmin` | Create an invoice for a shipment |
| `GET` | `/api/integration/payments/{invoiceId}` | `*.ExternalService`, `SystemAdmin` | Check payment status |

---

## 11. Step-by-Step: What Each Group Must Do

### Step 1: Agree on the shared JWT key and claim names

All teams meet and agree on:

- The **Jwt:Key** value (a single secure string shared across all systems)
- The **Jwt:Issuer** (e.g., `"CentralAuth"`)
- The **Jwt:Audience** (e.g., `"InternalSystems"`)
- The **role naming convention** (e.g., `Delivery.ExternalService`)

> **How to share the key:** Do not put it in a chat or email. Use a secure meeting, encrypted document shared only with team leads, or configure it in CI/CD secrets and Azure environment variables directly.

### Step 2: Each team builds or configures JWT validation

**If using ASP.NET Core (any version):**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "CentralAuth",
            ValidAudience = "InternalSystems",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("the-agreed-shared-key"))
        };
    });
```

- Replace `"CentralAuth"` and `"InternalSystems"` with the agreed values
- Replace `"the-agreed-shared-key"` with the actual shared key (from env var)

### Step 3: Create service accounts for cross-system communication

Each team's System Admin creates service accounts in the Auth Service for every other system they need to communicate with. See [Section 8](#8-setting-up-service-accounts-between-systems).

### Step 4: Build integration endpoints

Each team builds the integration endpoints listed in [Section 10](#10-integration-endpoints-data-contract) for their system. The JSON format must match exactly.

### Step 5: Test end-to-end

Test each cross-system flow:

1. **Login flow:** Can a user from any system log in via the Auth Service?
2. **JWT validation:** Can each system validate the JWT?
3. **Role check:** Does each system correctly grant/deny based on the role prefix?
4. **Data exchange:** Does Operational → Delivery → Operational round-trip work?

---

## 12. FAQ

### Can different .NET versions work together?

**Yes.** JWT, HTTP, and JSON are open standards. A .NET 8 system can validate a JWT issued by a .NET 9 system as long as the secret key and claim names are the same. The `System.IdentityModel.Tokens.Jwt` library is compatible across versions.

### Can we use different databases?

**Yes.** Each system has its own database. Communication is through API calls only. Operational can use PostgreSQL while Delivery uses SQL Server — they never touch each other's databases.

### What happens if the Auth Service goes down?

Users cannot log in until it recovers. However, existing JWTs remain valid until they expire (default 15 minutes). Service accounts that have cached JWTs can still make calls. For production, consider deploying at least two instances of the Auth Service behind a load balancer.

### How do we revoke a service account?

The System Admin deactivates it through the Auth Service:
```
PUT /api/admin/accounts/{id}/deactivate
Authorization: Bearer <admin-jwt>
```

After deactivation, the service account can no longer log in to get new JWTs. Existing JWTs expire on their own within 15 minutes.

### Can a service account access dashboards or UI?

**No.** Service accounts have roles like `Operational.ExternalService`. Each system's integration endpoints are protected to only allow `*.ExternalService` or `SystemAdmin` roles. Dashboard and UI endpoints check for human roles like `Operational.Employee`. A service account's JWT will fail those checks.

### How does a user access multiple systems?

They log in once via the Auth Service, then use the same JWT to call any system's API. For example, a System Admin could open Operational's dashboard in one tab and Finance's dashboard in another tab, both using the same JWT (as long as it hasn't expired). When the JWT expires, they refresh it using the refresh token.

### Does each system need its own copy of the employee database?

**Yes.** Each system keeps a local copy of employee profile data (name, contact info, department, etc.) for its own use. However, the **authentication** data (password hash, role) lives only in the Auth Service. When an employee's profile is updated in the Auth Service, the change does not automatically sync to each system's local copy unless a sync mechanism is built.
