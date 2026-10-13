# Dataverse Plug-in Template (Mac, browser-based deploy)

A reusable checklist for any new plug-in. Replace every `<PLACEHOLDER>`.

## Fill in first

| Placeholder | Meaning |
|---|---|
| `<PROJECT_DIR>` | Plug-in project folder |
| `<ASSEMBLY>` | Assembly name (the project name) |
| `<CLASS>` | Plug-in class name |
| `<VERSION>` | New build version, e.g. `1.0.4.0` (bump every upload) |
| `<TABLE>` | Table logical name, lowercase (step plug-ins) |
| `<MESSAGE>` | Create / Update / Delete ... (step plug-ins) |
| `<API_NAME>` | Custom API unique name (Custom API plug-ins) |

## Which kind of plug-in is it?

| Kind | Trigger | Registration |
|---|---|---|
| **Step plug-in** | A message on a table (Create, Update, ...) | Create a **step** row (Part 4A) |
| **Custom API plug-in** | A call to your own API | Set **Plugin Type** on the Custom API record (Part 4B). No step. |

## Rules to remember

- Update the **`.dll`**, not the `.nupkg` (packages are only for plug-ins with dependency DLLs).
- Keep the **same `.snk` signing key** forever. A new key makes updates fail.
- **Bump the version** on every upload so you can prove which build is deployed.
- Assembly = the DLL (`pluginassembly`). Plug-in type = a class in it (`plugintype`). Steps and Custom APIs point at a **type**.
- Logical names (tables, columns) are **lowercase**.
- Filter and sort on the server (`QueryExpression`), never load everything and loop.
- Throw `InvalidPluginExecutionException` for expected errors.

---

## Part 1: Code

Add `<CLASS>.cs` to the project. Both kinds use the project's `PluginBase`.

### Step plug-in skeleton



### Custom API plug-in skeleton


## Part 2: Build (Terminal)

First set both versions in `<ASSEMBLY>.csproj` to `<VERSION>`:
```xml
<AssemblyVersion><VERSION></AssemblyVersion>
<FileVersion><VERSION></FileVersion>
```
Then (one line):
```bash
cd <PROJECT_DIR>
CLASS="<CLASS>"
dotnet clean -c Release -v q && dotnet build -c Release -v q && grep -c "$CLASS" bin/Release/net462/<ASSEMBLY>.dll && base64 -i bin/Release/net462/<ASSEMBLY>.dll | tr -d '\n' | pbcopy && echo "built, class found, base64 copied"
```
Expected: a number of 1 or more, then `built, class found, base64 copied`. If it stops early, the build failed or the class name is not in the DLL.

---

## Part 3: Upload (browser Console)

Open any row in a **model-driven app** (URL contains `main.aspx`), press **cmd+option +J **, open **Console**.

**3.1 Unpause the page.** Sources tab: press **F8** until "Paused in debugger" clears, then **Cmd+F8**. Back to Console.

**3.2 Get the assembly id**
```javascript
let res = await Xrm.WebApi.retrieveMultipleRecords("pluginassembly", "?$select=pluginassemblyid,name,version&$filter=name eq '<ASSEMBLY>'");
console.log(res.entities);
window.asmId = res.entities[0].pluginassemblyid;
```
If `Xrm is not defined`, switch the Console frame dropdown (`top`) to `contentIFrame0`.

**3.3 Read the DLL from the clipboard** (click the page body first)
```javascript
window.b64 = (await navigator.clipboard.readText()).trim();
console.log(window.b64.length, atob(window.b64).slice(0, 2));
```
Expected: a long number, then `MZ`.

**3.4 Upload**
```javascript
await Xrm.WebApi.updateRecord("pluginassembly", window.asmId, { content: window.b64 });
console.log("updated");
```

**3.5 Verify the version and the class, and keep the type id**
```javascript
res = await Xrm.WebApi.retrieveMultipleRecords("pluginassembly", `?$select=version&$filter=pluginassemblyid eq ${window.asmId}`);
console.log(res.entities[0].version);
res = await Xrm.WebApi.retrieveMultipleRecords("plugintype", `?$select=plugintypeid,typename&$filter=_pluginassemblyid_value eq ${window.asmId}`);
console.log(res.entities);
window.typeId = res.entities.find(e => e.typename.endsWith(".<CLASS>"))?.plugintypeid;
console.log("typeId:", window.typeId);
```
Expected: the new version, and a `typeId` (not undefined). If the class is missing, create the type row:
```javascript
await Xrm.WebApi.createRecord("plugintype", {
  typename: "<ASSEMBLY>.<CLASS>", friendlyname: "<ASSEMBLY>.<CLASS>", name: "<ASSEMBLY>.<CLASS>",
  "pluginassemblyid@odata.bind": `/pluginassemblies(${window.asmId})`
});
```
then rerun the 3.5 listing.

Alternative: `pac plugin push --pluginId <assemblyId> --pluginFile bin/Release/net462/<ASSEMBLY>.dll --type Assembly`.

---

## Part 4A: Register a step (step plug-ins only)

**4A.1 Find the message and its filter for your table**
```javascript
res = await Xrm.WebApi.retrieveMultipleRecords("sdkmessage", "?$select=sdkmessageid&$filter=name eq '<MESSAGE>'");
window.msgId = res.entities[0].sdkmessageid;
res = await Xrm.WebApi.retrieveMultipleRecords("sdkmessagefilter", `?$select=sdkmessagefilterid&$filter=_sdkmessageid_value eq ${window.msgId} and primaryobjecttypecode eq '<TABLE>'`);
window.filterId = res.entities[0].sdkmessagefilterid;
console.log(window.msgId, window.filterId);
```

**4A.2 Create the step**
```javascript
const step = await Xrm.WebApi.createRecord("sdkmessageprocessingstep", {
  name: "<CLASS>: <MESSAGE> of <TABLE>",
  stage: 20,               // 10 = PreValidation, 20 = PreOperation, 40 = PostOperation
  mode: 0,                 // 0 = Synchronous, 1 = Asynchronous (async only with stage 40)
  rank: 1,                 // execution order among steps at the same stage
  supporteddeployment: 0,  // 0 = server only
  // filteringattributes: "col1,col2",   // Update only: run only when these columns change
  "eventhandler_plugintype@odata.bind": `/plugintypes(${window.typeId})`,
  "sdkmessageid@odata.bind": `/sdkmessages(${window.msgId})`,
  "sdkmessagefilterid@odata.bind": `/sdkmessagefilters(${window.filterId})`
});
window.stepId = step.id;
console.log("step:", window.stepId);
```
If the create fails with a navigation property error, tell Claude the exact error text; the lookup property name for the plug-in type can differ.

**4A.3 (Optional) Add an image** (pre/post copy of the row, so you can read old values)
```javascript
await Xrm.WebApi.createRecord("sdkmessageprocessingstepimage", {
  name: "PreImage", entityalias: "PreImage",
  imagetype: 0,                      // 0 = Pre, 1 = Post, 2 = Both
  messagepropertyname: "Target",     // Update uses Target (Create uses Id)
  attributes: "col1,col2",           // only the columns you need
  "sdkmessageprocessingstepid@odata.bind": `/sdkmessageprocessingsteps(${window.stepId})`
});
```
In code: `context.PreEntityImages["PreImage"]`.

**Stage and mode cheat sheet**
- **PreValidation (10):** before the transaction; can reject early.
- **PreOperation (20):** inside the transaction, before the write; change `Target` directly.
- **PostOperation (40):** after the write; use for follow-up work.
- **Sync** blocks the user and rolls back on error. **Async** runs later, never blocks, can't cancel the operation.

## Part 4B: Link a Custom API (Custom API plug-ins only)

make.powerapps.com, your solution, **Objects**, **Custom APIs**, open the API, set **Plugin Type** to `<CLASS>`, **Save**. Do not add a step.

---

## Part 5: Trace logging and testing

1. Power Platform admin center, your environment, **Settings**, **Audit and logs**, **Plug-in trace log**, set to **All**. Set it back to **Exception** afterwards.
2. Trigger the plug-in:
   - Step plug-in: perform the message (create or update a row in the app).
   - Custom API: call it from the Console:
   ```javascript
   const req = {
     <InputName>: <value>,
     getMetadata: () => ({
       boundParameter: null,       // null = global API
       operationType: 0,           // 0 = action, 1 = function
       operationName: "<API_NAME>",
       parameterTypes: { <InputName>: { typeName: "Edm.<Type>", structuralProperty: 1 } }
     })
   };
   const resp = await Xrm.WebApi.online.execute(req);
   console.log(resp.status, await resp.json());
   ```
   Common `typeName` values: `Edm.Guid`, `Edm.String`, `Edm.Int32`, `Edm.Boolean`.
3. Read the trace log: `<org-url>/main.aspx?etn=plugintracelog&pagetype=entitylist`. Screenshot your lines as proof.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `await can not be used ... paused in the debugger` | Redo 3.1 (F8, then Cmd+F8). |
| `Xrm is not defined` | Use a `main.aspx` page; switch frame to `contentIFrame0`. |
| 3.3 does not print `MZ` | Clipboard did not hold the DLL. Rerun Part 2. |
| 3.5 shows old version | Upload did not land. Rerun Parts 2 and 3. |
| `typeId` is undefined | Class not registered. Run the plug-in type create snippet. |
| Update rejected | Signing key changed. Use the original `.snk`. |
| Plug-in never fires | No step registered, wrong message/table, or `filteringattributes` excludes the changed column. |
| "key not found: X" (Custom API) | Parameter name in code does not match the API's Unique Name. |
| 403 | Calling user lacks privileges. |
| Nothing in trace log | Trace log not set to All, or the plug-in never ran. |
| Infinite loop / depth error | Your plug-in updates the row it triggers on. Check `context.Depth` and use `filteringattributes`. |