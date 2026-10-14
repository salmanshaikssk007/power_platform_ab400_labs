# AB-400 Labs 1-3: Work Summary

Exam: AB-400, Extending Microsoft Power Platform Solutions with Code and AI (exam date Sun Oct 25, 2026).
Environment: `salman-dev` (Dataverse developer environment). Solution prefix: `cr36f`.
Repo: `power_platform_ab400_labs` (plug-in code in `plugins/CasePlugins/`).
Last updated: Wed Oct 14, 2026.

> Status key: [x] done and evidenced, [~] built but not yet proven, [ ] not done.
> Items marked [~] need their "Prove it" evidence before you count the lab as complete.

## Data model (shared by all labs)

| Item | Name |
|---|---|
| Tables | `cr36f_case`, `cr36f_worker` |
| Lookup on Case | `cr36f_assignedworker` (points to Worker) |
| Column on Worker | `cr36f_caseloadcount` (number of cases assigned) |
| Custom API (Lab 3) | `cr36f_AssignWorker` |

---

## Lab 1: First plug-in

**Goal:** write a plug-in, understand the execution context and the pipeline stages, and log with the trace service.

**What was built**
- `PluginBase.cs`: a base class that every plug-in inherits. It builds a local context, wraps tracing, and catches `OrganizationServiceFault`. `LocalTracingService` prefixes each trace line with the milliseconds since the previous one.
- `CaseCreateDefaults.cs`: a plug-in on Case Create. It traces `MessageName`, `PrimaryEntityName`, `Stage`, `Mode`, `Depth`, `UserId` and `InitiatingUserId`, and checks that `Target` exists before using it.

**Concepts learned**
- The execution context describes what is happening when the plug-in fires (message, table, stage, mode, depth, users).
- Stages: PreValidation (10, outside the database transaction), PreOperation (20, inside), PostOperation (40, inside).
- `UserId` is the user the step runs as. `InitiatingUserId` is the user who triggered it.
- Trace logs are read in Settings > Plug-in trace log.

**Status**
- [x] Plug-in code written and in the repo
- [~] Registered with the Plug-in Registration Tool (not confirmed)
- [~] Same step registered at stages 10, 20 and 40, trace lines compared
- [ ] Business logic that sets the defaults (the file only reads `Target` so far)

**To finish (about 10 min):** register the step on Create of Case at all three stages, create one Case, and paste the three trace lines (`Stage=10`, `20`, `40`) into a `NOTES.md`.

---

## Lab 2: Caseload plug-in, pre-image, alternate key, column security

**Goal:** when a Case's Assigned Worker changes from A to B, A's caseload goes down by 1 and B's goes up by 1.

**What was built**
- A plug-in on Case Update that uses a pre-image to read the old Assigned Worker, then uses the Organization service to adjust `cr36f_caseloadcount` on both Workers.
- Part A: alternate key on Case (Case Number).
- Part B: column-level security on Priority, with a field security profile.

**Concepts learned**
- **Pre-image:** a snapshot of the row before the change. Without it, the old value is not available in the plug-in.
- **Filtering attributes:** the step only fires when the chosen columns change. Without them, the plug-in runs on every update.
- **Missing vs null in `Target`:** a column missing from `Target` means "not changed". A column present with null means "cleared". The `Contains` check prevents an unrelated update from decrementing a caseload.
- **Registration is data:** assembly, plug-in type, step and image are rows in Dataverse. Steps point to the type, the type points to the assembly, so updating an assembly keeps its steps.
- **Alternate keys:** up to 10 per table, unique values, built as an index in the background (Pending, then Active, or Failed if data has duplicates).
- **Column security:** hides a column until a field security profile grants Read, Create or Update. Profiles go to users or teams, not roles.
- **Console testing:** `Xrm.WebApi` from the browser console, `@odata.bind` for relationships, `_name_value` for lookup values.

**Mistakes that taught the most**
1. Stale build: editing code does nothing until you build and upload the assembly.
2. Wrong logical name (`cr36f_caseworker` compiles but returns null). Always copy logical names from the table.
3. Missing `Contains` check on `Target`.
4. Console issues (debugger paused, `Xrm` not defined, `window.b64` lost after refresh).

**Status**
- [x] Plug-in code written and uploaded
- [~] Step registered with filtering attribute on the Assigned Worker column
- [~] Pre-image created and named correctly
- [~] Trace log shows A decremented and B incremented
- [~] Alternate key status is Active
- [~] Column security profile applied and tested with a second user

**Known weak points**
- Read, add, write is not safe under concurrent updates (ETag and optimistic concurrency come in Lab 4).
- Handles Update only, not Create or Delete.

---

## Lab 3: Custom API with its plug-in

**Goal:** create a reusable server-side operation `cr36f_AssignWorker` with a defined contract, backed by plug-in code.

**What was built or set up**
- Custom API `cr36f_AssignWorker` (action) with a request parameter for the Case id and a response parameter for the worker name.
- A plug-in class set as the plug-in type on the Custom API record.
- Postman request: `POST https://<yourorg>.crm.dynamics.com/api/data/v9.2/cr36f_AssignWorker` with body `{ "CaseId": "<case-guid>" }`, headers `Content-Type`, `OData-Version: 4.0`, `Accept: application/json`, and a Bearer token.
- Token for testing: `az login --allow-no-subscriptions`, then `az account get-access-token --resource <environment URL>`. Tokens last about an hour.

**Concepts learned**
- A Custom API is a message with a defined contract, the modern replacement for custom process actions.
- Function (GET) vs action (POST).
- Binding type: global, entity, or entity collection.
- "Allowed custom processing step type" controls whether others can add steps (none, async only, sync and async).
- The logic is the plug-in type on the API record, not a normal step registration.
- The Web API belongs to Dataverse (OData v4). Entra ID only issues the token, and the token's audience is the environment URL.
- Read only: managed identity for plug-ins (signed assembly plus federated credentials, no stored secrets) and business events.

**Status**
- [x] Custom API design and Postman setup understood
- [~] Custom API record created with parameters and plug-in type
- [~] Plug-in trace line seen in the trace log
- [~] 200 response showing the output parameter in Postman
- [ ] 5-line note on managed identity and business events

---

## Evidence checklist (before starting Lab 5)

1. Lab 1: trace lines for stages 10, 20, 40 pasted into `NOTES.md`
2. Lab 2: trace log showing caseload change, alternate key Active, column security tested
3. Lab 3: screenshot of 200 response plus the trace log line
4. Lab 3: 5-line note on managed identity and business events

## Exam takeaways so far

- Know the three pipeline stages and which run inside the transaction.
- Pre-image for old values, filtering attributes to limit firing, `Contains` check on `Target`.
- Registration is rows in Dataverse.
- Custom API = defined contract, logic via plug-in type, action vs function, binding type.
- Entra proves who you are. Dataverse security roles decide what you may do.