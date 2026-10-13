# RaceDay API Endpoint Plan — Part 2 Working Reference

> **Important:** This is a working endpoint reference reconstructed from the current Part 2 implementation and lecturer's guide. It is **not a substitute for the approved Part 1 endpoint plan**. Before submission, compare every route with the approved Part 1 plan and retain the approved document in `docs/`. Explain any deliberate differences in the README.

## Authentication

| Method | Route | Access | Purpose | Success |
|---|---|---|---|---|
| POST | `/api/auth/register` | Public | Register an Organiser or Participant | 201 Created |
| POST | `/api/auth/login` | Public | Verify credentials and start a session | 200 OK |
| POST | `/api/auth/logout` | Authenticated | Clear the current session | 200 OK |

## Profile

| Method | Route | Access | Purpose | Success |
|---|---|---|---|---|
| GET | `/api/profile/me` | Both roles | View the current user's own profile | 200 OK |
| PUT | `/api/profile/me` | Both roles | Update the current user's own name | 200 OK |

## Events

| Method | Route | Access | Purpose | Success |
|---|---|---|---|---|
| GET | `/api/events` | Both roles | List events | 200 OK |
| GET | `/api/events/{id}` | Both roles | View one event | 200 OK |
| GET | `/api/events/mine` | Organiser | List events owned by the current Organiser | 200 OK |
| POST | `/api/events` | Organiser | Create an event | 201 Created |
| PUT | `/api/events/{id}` | Owning Organiser | Update an owned event | 200 OK |
| DELETE | `/api/events/{id}` | Owning Organiser | Delete an event if no dependent records prevent deletion | 200 OK |

Event creation captures `Name`, `Description`, `Date`, `Location`, `DistanceKm`, and `EventType` (`Run`, `Walk`, or `Cycle`).

## Categories

| Method | Route | Access | Purpose | Success |
|---|---|---|---|---|
| GET | `/api/events/{eventId}/categories` | Both roles | List categories for an event | 200 OK |
| POST | `/api/events/{eventId}/categories` | Owning Organiser | Create a category | 201 Created |
| PUT | `/api/categories/{id}` | Owning Organiser | Update a category | 200 OK |
| DELETE | `/api/categories/{id}` | Owning Organiser | Delete a category if no enrolments depend on it | 200 OK |

## Event enrolments

| Method | Route | Access | Purpose | Success |
|---|---|---|---|---|
| POST | `/api/enrolments` | Participant | Enter an event using a category belonging to that event | 201 Created |
| GET | `/api/enrolments/mine` | Participant | View the current Participant's enrolments | 200 OK |
| GET | `/api/enrolments/event/{eventId}` | Owning Organiser | View enrolments for an owned event | 200 OK |
| PUT | `/api/enrolments/{id}/status` | Owning Organiser | Change status (`Pending`, `Confirmed`, `Cancelled`) | 200 OK |

## Results

| Method | Route | Access | Purpose | Success |
|---|---|---|---|---|
| POST | `/api/results` | Owning Organiser | Record finish time and position for an enrolment in an owned event | 201 Created |
| GET | `/api/results/mine` | Participant | View the current Participant's results | 200 OK |
| GET | `/api/results/event/{eventId}` | Owning Organiser | View results for an owned event | 200 OK |

## Standard failure responses

- `400 Bad Request`: validation failure or invalid business operation.
- `401 Unauthorized`: no valid logged-in session.
- `403 Forbidden`: authenticated user has the wrong role or does not own the relevant event.
- `404 Not Found`: requested resource does not exist.

## Session behaviour

Login creates a server-side ASP.NET Core session with `UserId`, `Role`, and `Email`. The browser sends the session cookie on subsequent requests. Logout clears the session. Use Swagger to test the complete flow, including switching between Organiser and Participant accounts.
