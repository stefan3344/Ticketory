# Assignment requirements → implementation

| Requirement | Status | Implementation | How to demonstrate |
|---|---|---|---|
| UI connected to a database with full CRUD | Done | ASP.NET Core MVC views and controllers for events, venues, ticket types, orders and reviews. | Log in as Organizer/Admin, create/edit/delete an event, add ticket types, browse events, place an order. |
| Whole solution starts with Docker Compose | Done | `docker-compose.yml` with 15 services and health checks. | `docker compose up -d --build`. |
| Web application (ASP.NET) | Done | `Ticketory.Api` — MVC UI + REST API, .NET 10. | <https://localhost>. |
| Database (PostgreSQL) | Done | `Ticketory.Infrastructure/Persistence/AppDbContext.cs`, EF Core migrations. | Data persists in the `postgres-data` volume. |
| Second data store (Redis) | Done | Cache-aside, distributed locks and Data Protection keys. | Event pages are served from cache; stock is protected by a lock. |
| Load balancer in front of at least two replicas | Done | Nginx `least_conn` upstream with `api-1` and `api-2`. | Response header `X-Served-By` alternates between `api-1` and `api-2`. |
| Monitoring | Done | Prometheus + Grafana + node-exporter + cAdvisor + PostgreSQL/Redis/RabbitMQ exporters + Dozzle. | Grafana dashboard, `/metrics`, Dozzle logs, health checks. |
| HTTPS with a self-signed certificate | Done | `scripts/gen-certs.sh`, Nginx TLS termination, HTTP→HTTPS redirect, HSTS. | Open <https://localhost> and accept the certificate. |
| Keepalived (optional) | Deliberately not implemented | Host-level VRRP is unreliable in Docker Desktop and redundant with two replicas + Nginx. Documented in `docs/https-and-ha.md`. | Explain the decision and the VM-based extension. |
| Corosync + Pacemaker (optional) | Deliberately not implemented | Requires multiple Linux VMs, quorum and fencing; documented in `docs/https-and-ha.md`. | Explain the design and the extension plan. |
| Message exchange (RabbitMQ) | Done | Topic exchange, outbox publisher, six consumers (payment, tickets, cancellation/refund e-mails), retry queue, dead-letter queue. | Create an order, watch RabbitMQ management, Mailpit and the issued tickets. |
| REST API | Done | `/api/events`, `/api/orders`, `/api/tickets`, `/api/auth`, `/api/events/{id}/reviews`, `/api/dashboard`. | Swagger UI at <https://localhost/swagger>. |
| Automated tests | Done | 37 xUnit tests: domain rules, `FakeTimeProvider`, refund/reopen, QR generation, outbox filtering + API integration tests with `WebApplicationFactory` and Testcontainers PostgreSQL (real migrations). | `dotnet test Ticketory.slnx` (Docker must be running). |
| Cancellation and refund flow | Done | Pending orders are cancelled; paid orders are refunded with payment/ticket updates and `order.cancelled`/`order.refunded` e-mails. | Cancel an event as Organizer/Admin and inspect Mailpit, orders and tickets. |
| Event reopen | Done | `POST /api/events/{id}/reopen` returns a cancelled future event to `Draft`. | Reopen a cancelled event and publish it again. |
| QR in ticket e-mail | Done | Inline PNG QR codes are embedded via CID in the ticket e-mail. | Buy a ticket and open the e-mail in Mailpit. |
| Outbox message audit UI | Done | Admin → Messages lists outbox messages with type/status filters, retry state and payload JSON. | Log in as Admin and open Admin → Messages. |
| Deterministic time handling | Done | Domain methods accept `DateTime utcNow`; Application/Worker inject `TimeProvider`; tests use `FakeTimeProvider`. | Inspect `TimeProvider` usages and `Ticketory.Tests`. |
| Configuration from appsettings | Done | `appsettings.json` + `appsettings.Production.json` + typed options; secrets only in `.env` / user-secrets. | Inspect `appsettings*.json`, `src/*/Options/`, and `.env.example`. |
| UML class diagram | Done | `docs/class-diagram.svg` (source: `docs/class-diagram.puml`). | Open the SVG. |
| Documentation of components and configuration | Done | `README.md`, `docs/architecture.md`, this file. | Read the docs. |
| No production secrets in code; separate secrets file | Done | `.env` (git-ignored), `.env.example` with demo placeholders, empty secrets in `appsettings*.json`, `dotnet user-secrets` for local development. | Inspect the repository; no production credentials are committed. |
| Presentation (5–10 slides) | Done | `docs/presentation.md` (Marp source), `docs/presentation.html` (standalone HTML slides; loads the two SVG diagrams from `docs/`) and `docs/presentation.pdf`. | Open `presentation.html` or `presentation.pdf`. |

## Bonuses

- Two API replicas with health checks and Nginx failover.
- Redis distributed lock and shared Data Protection keys (real multi-replica concerns).
- Outbox pattern, idempotent consumers, retry queue and dead-letter queue.
- QR code generation for tickets.
- Mailpit integration for a visible e-mail flow.
- Prometheus alert rules and a provisioned Grafana dashboard.
- Admin dashboard and role-based authorization.
- Swagger/OpenAPI documentation and automated API tests.
- Domain unit tests plus API integration tests.
