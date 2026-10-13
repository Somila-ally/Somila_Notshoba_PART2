

This repository contains the planning documents from **Part 1** and the working RESTful API built for **Part 2**
(PROG6212 PoE).

## Project Description

The API is an ASP.NET Core Web API (.NET 8) backed by SQL Server, using Entity Framework Core Code First.
Authentication is session-based: a successful login stores the user's `UserId` and `Role` in a server-side
session, and every protected endpoint checks that session on each request. Passwords are never stored in plain
text, only as a salted hash.

## User Roles

### Organiser
- Registers, logs in and views/updates their own profile.
- Creates, updates and deletes **their own** events; views the events they own.
- Creates, updates and deletes categories for **their own** events.
- Views the enrolments for their own events and changes an enrolment's status.
- Records finish times and finishing positions, and views the results of their own events.

### Participant
- Registers, logs in and views/updates their own profile.
- Browses all events and their categories.
- Enters an event by choosing a category, and views their own enrolments.
- Views their own race results.

| Capability | Organiser | Participant |
|---|:---:|:---:|
| Register / log in / log out | Yes | Yes |
| View and update own profile | Yes | Yes |
| View events and categories | Yes | Yes |
| Create / update / delete events (own only) | Yes | 403 |
| Create / update / delete categories (own events only) | Yes | 403 |
| Enter an event | 403 | Yes |
| View enrolments for an event / change status (own events only) | Yes | 403 |
| Record results (own events only) | Yes | 403 |
| View own results | 403 | Yes |

## Part 1

The planning/database documents are kept in [`docs/`](docs):

- `RaceDay_ERD.pdf` - supplied Part 1 entity relationship diagram
- `RaceDay_Database.sql` - preserved Part 1 SQL script
- `RaceDay_Part2_Database.sql` - Part 2 schema reference including the required event fields and event-enrolment relationship
- `RaceDay_API_Endpoint_Plan.md` - Part 2 working endpoint reference reconstructed from the current implementation

**Submission action required:** the supplied project ZIP did not contain the approved Part 1 API endpoint plan. Add that exact approved document to `docs/` and compare it with `RaceDay_API_Endpoint_Plan.md`. Do not submit the reconstructed reference as though it were the original approved Part 1 document.

### Differences from Part 1

The Part 2 brief requires a few things that the Part 1 ERD did not contain, so the implementation deliberately adds them:

| Change | Reason |
|---|---|
| `Events.DistanceKm` and `Events.EventType` (Run / Walk / Cycle) added | The Part 2 brief states every event must capture name, description, date, location, **distance and event type**. |
| `Enrolments.EventId` added (FK to Events) | The Part 2 brief states an enrolment links the **Participant, Event and Category**, and organisers view enrolments per event. The unique constraint stays `(ParticipantId, CategoryId)`, so a participant can enter several categories of one event but never the same category twice. |
| Registration accepts `FirstName` + `LastName` | The brief's `RegisterRequest` has separate names; they are combined into the ERD's single `Users.FullName` column. |
| `CreateCategoryRequest.DistanceKm` is optional | Categories can be age groups (e.g. "Senior") with no distance of their own; it then defaults to the event's distance, so the ERD's `Categories.DistanceKm NOT NULL` still holds. |
| C# property `Event.Name` maps to the ERD column `EventName` | The brief's Event model lists `Name`; `[Column("EventName")]` keeps the database identical to the ERD. |
| `Description` is required when creating or updating an event | The brief states every event must capture name, description, date, location, distance and event type. The ERD column stays nullable. |
| New enrolments start as `Pending`; statuses are Pending, Confirmed, Cancelled | These are the statuses named in the Part 2 brief (the ERD's default text `Registered` is no longer used). The `Status` column itself is unchanged. |

Everything else (Roles lookup table, Users, Events, Categories, Enrolments with `Status`, Results with `Position` and
`RecordedAt`, no cascade deletes) follows the Part 1 ERD.

## Part 2

### Technologies Used
- ASP.NET Core Web API, .NET 8
- Entity Framework Core 8 (Code First) with SQL Server
- Session-based authentication and a custom `[RequireRole]` authorization filter
- `PasswordHasher<T>` (salted PBKDF2) for password hashing
- Swashbuckle (Swagger / OpenAPI) with annotations
- xUnit, `WebApplicationFactory` and the EF Core InMemory provider for tests
- GitHub Actions for CI

### Project Structure

```
RaceDay
|-- docs/                     Part 1 artifacts, Part 2 schema/route references and checklist
|-- RaceDay.Api/              The Web API
|   |-- Controllers/          Auth, Profile, Events, Categories, Enrolments, Results
|   |-- Data/                 RaceDayDbContext
|   |-- Migrations/           InitialRaceDayDatabase (EF Core Code First migration + model snapshot)
|   |-- Filters/              RequireRoleAttribute and SessionKeys (session + role enforcement)
|   |-- Models/               Role, AppUser, Event, Category, Enrolment, Result
|   |-- appsettings.json
|   `-- Program.cs
|-- RaceDay.Contracts/        Shared request/response DTOs (Dtos.cs)
|-- RaceDay.Api.Tests/        xUnit tests
|-- .github/workflows/ci.yml  GitHub Actions workflow
`-- RaceDay.sln
```

Relationships: one Role has many Users; one Organiser has many Events; one Event has many Categories and many
Enrolments; one Participant has many Enrolments; one Category has many Enrolments; one Enrolment has zero or one Result.

### Database Setup

1. Install .NET 8 SDK, SQL Server (Express or LocalDB) and Visual Studio 2022.
2. Open `RaceDay.sln`.
3. In `RaceDay.Api/appsettings.json` set the connection string for your SQL Server instance. The default is
   `Server=.\SQLEXPRESS;Database=RaceDayDb;Trusted_Connection=True;TrustServerCertificate=True;`
4. Create the database (Entity Framework Core Code First). The initial migration `InitialRaceDayDatabase` is already
   in `RaceDay.Api/Migrations`, so you only need to apply it. In **Tools > NuGet Package Manager > Package Manager
   Console**, with `RaceDay.Api` as the default project:
   ```
   Update-Database
   ```
   or from a terminal in the repository root:
   ```
   dotnet ef database update --project RaceDay.Api
   ```
   To regenerate the migration from the models instead, delete the `RaceDay.Api/Migrations` folder and run
   `Add-Migration InitialRaceDayDatabase` followed by `Update-Database`.
5. Open SQL Server Management Studio and confirm `RaceDayDb` contains Roles, Users, Events, Categories,
   Enrolments and Results. The Roles table is seeded with `Organiser` and `Participant`.

The original Part 1 SQL script is retained for traceability. Part 2 requires event distance/type and a direct event link for
an enrolment; these changes are documented above and represented in `docs/RaceDay_Part2_Database.sql` and the EF Core
migration. Use EF Core migrations as the primary Code-First setup path.

### How to Run the API

```
dotnet run --project RaceDay.Api
```

Swagger opens at `https://localhost:7100/swagger`.

### Authentication

- **Register** validates the request, checks the email is unused and the role is valid, hashes the password with
  `PasswordHasher` and saves the user. The plain-text password is never stored.
- **Login** finds the user by email, verifies the password against the stored hash and creates a server-side session
  containing `UserId` and `Role`.
- **Logout** clears the session.
- **Role enforcement**: `[RequireRole]` returns **401** when nobody is logged in and **403** when the logged-in
  user has the wrong role. Ownership is also enforced: an Organiser can only change events, categories,
  enrolments and results that belong to their own events.

### API Endpoints

| Method | Route | Access | Success |
|---|---|---|---|
| POST | `/api/auth/register` | Public | 201 |
| POST | `/api/auth/login` | Public | 200 |
| POST | `/api/auth/logout` | Logged in | 200 |
| GET | `/api/profile/me` | Both | 200 |
| PUT | `/api/profile/me` | Both | 200 |
| GET | `/api/events` | Both | 200 |
| GET | `/api/events/{id}` | Both | 200 |
| GET | `/api/events/mine` | Organiser | 200 |
| POST | `/api/events` | Organiser | 201 |
| PUT | `/api/events/{id}` | Owning Organiser | 200 |
| DELETE | `/api/events/{id}` | Owning Organiser | 200 |
| GET | `/api/events/{eventId}/categories` | Both | 200 |
| POST | `/api/events/{eventId}/categories` | Owning Organiser | 201 |
| PUT | `/api/categories/{id}` | Owning Organiser | 200 |
| DELETE | `/api/categories/{id}` | Owning Organiser | 200 |
| POST | `/api/enrolments` | Participant | 201 |
| GET | `/api/enrolments/mine` | Participant | 200 |
| GET | `/api/enrolments/event/{eventId}` | Owning Organiser | 200 |
| PUT | `/api/enrolments/{id}/status` | Owning Organiser | 200 |
| POST | `/api/results` | Owning Organiser | 201 |
| GET | `/api/results/mine` | Participant | 200 |
| GET | `/api/results/event/{eventId}` | Owning Organiser | 200 |

The API uses the status codes from the Part 2 brief: **200** OK, **201** Created, **400** Bad Request (invalid data, including a duplicate email, already enrolled, category full, result already recorded, and deleting an event or category that already has enrolments), **401** Unauthorized (not logged in), **403** Forbidden (wrong role, or not the owner of the event) and **404** Not Found.

Enrolment statuses: `Pending` (default), `Confirmed`, `Cancelled`. Event types: `Run`, `Walk`, `Cycle`.

### Swagger

Swagger UI lists every endpoint grouped as **Auth, Profile, Events, Categories, Enrolments, Results**.
To try a full flow:

1. `POST /api/auth/register` an Organiser, then `POST /api/auth/login`.
2. `POST /api/events`, then `POST /api/events/{eventId}/categories`.
3. `POST /api/auth/logout`, register and log in as a Participant.
4. `POST /api/enrolments` with the event and category ids.
5. Log in as the Organiser again: `GET /api/enrolments/event/{eventId}`, then `POST /api/results` with the enrolment id.
6. Log in as the Participant: `GET /api/results/mine`.

Swagger keeps the session cookie, so log out and in again to switch roles. Trying an Organiser-only endpoint as a
Participant returns 403.

### Unit Testing

```
dotnet test
```

The tests boot the real API in memory (`WebApplicationFactory`) against an EF Core InMemory database, so no SQL Server
is needed. They cover:

- **Authentication**: valid registration (both roles), invalid role, duplicate email, short password, invalid email,
  password stored hashed, valid login, wrong password, unknown email, logout clearing the session.
- **Profile**: unauthenticated access (401), view own profile, update only own profile.
- **Events**: both roles can view, Organisers can create (201), Participants cannot (403), unauthenticated (401),
  validation failures, `mine`, owner-only update/delete, 404 handling, delete blocked when enrolments exist.
- **Categories**: owner-only create/update/delete, Participant rejected, default distance, delete blocked when enrolled.
- **Enrolments**: Participant can enter (201), duplicate (400), two categories of one event, Organiser rejected (403),
  category from another event (400), full category (400), owner-only viewing and status changes.
- **Results**: owner-only recording, Participant rejected (403), duplicate (400), invalid time, cancelled enrolment,
  Participant views own results, owner-only event results.

### CI/CD

`.github/workflows/ci.yml` runs on every push and pull request: checkout, install .NET 8, restore, build the solution
and run all unit tests.

### GitHub Actions Screenshot

**Before submission:** after pushing this repository, open the latest successful GitHub Actions run, take a screenshot
showing the green successful status, save it as `docs/ci-green.png`, and include it below. A genuine screenshot cannot
be created until GitHub has actually run the workflow.

<!-- After saving the real screenshot, add: ![GitHub Actions successful build](docs/ci-green.png) -->

### Video Presentation

**Before submission:** replace the placeholder with the shareable URL of your unlisted YouTube demonstration.

`[ADD YOUR UNLISTED YOUTUBE VIDEO LINK HERE]`


## Verification status and submission reminders

The source package has been reviewed and updated against the lecturer's guide. See [`docs/LECTURER_GUIDE_FINAL_CHECKLIST.md`](docs/LECTURER_GUIDE_FINAL_CHECKLIST.md) for the remaining submission actions. The .NET SDK was not installed in the
editing environment, so the solution could not be compiled or its tests executed here. Before submission, run `dotnet restore RaceDay.sln`, `dotnet build RaceDay.sln --configuration Release`, and `dotnet test RaceDay.sln --configuration Release` locally and verify the same
results in GitHub Actions. Also add the approved Part 1 endpoint-plan document, a genuine screenshot of a successful green
CI run, your unlisted YouTube link, and ensure your own GitHub history contains at least 20 meaningful Part 2 commits.


## Local SQL Server setup (Windows LocalDB)

The API connection string in `RaceDay.Api/appsettings.json` uses `(localdb)\\MSSQLLocalDB`, Windows Authentication, and the database name `RaceDayDb`. Keep the existing `RaceDay.Api/Migrations` folder.

From a terminal opened at the folder containing `RaceDay.sln`, run:

```powershell
sqllocaldb info
sqllocaldb start MSSQLLocalDB
dotnet restore RaceDay.sln
dotnet build RaceDay.sln
dotnet ef database update --project .\RaceDay.Api\RaceDay.Api.csproj --startup-project .\RaceDay.Api\RaceDay.Api.csproj
dotnet test RaceDay.sln --configuration Release
```

If `dotnet ef` is unavailable, install/update the EF Core 8 tool: `dotnet tool install --global dotnet-ef --version 8.*` (or `dotnet tool update --global dotnet-ef --version 8.*`). Do not paste stack-trace lines beginning with `at ...` into the terminal; run commands only. Integration tests use an InMemory database and do not require LocalDB.
