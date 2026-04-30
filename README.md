# Flag Exercise — T(x) / R(x) Windows Services

## Explanation

Imagine two computers in an office: **Computer A** and **Computer B**.
You want a small piece of software that constantly does this:

> *"Every few seconds, Computer A drops a file into a shared folder. Computer B sees the file, deletes it, and emails the IT team that it happened."*

This project is exactly that. Each computer runs a small program that starts automatically with Windows (a **Windows Service**) and shows a simple **web page** so a person can configure it from the browser.

There are **two roles** — one per computer:

- **T(x) — the Sender.** Lives on Computer A.
  Every 5–10 seconds it creates a small "flag" file in a folder, then immediately moves any file it finds in that folder to the destination folder (which can live on a shared drive that Computer B can also see).

- **R(x) — the Receiver / Cleaner.** Lives on Computer B.
  It watches the destination folder. As soon as a file lands there, it deletes the file and sends a notification — by **email (SMTP)** and to a **Syslog server** (a central log collector commonly used by IT departments).

You install **only one role per machine** — the installer asks which one.

### What you see when you use it

Open a browser on each machine:

- On the Sender machine: <http://localhost:5081>
- On the Receiver machine: <http://localhost:5082>

You get a small dashboard where you can:

- Set the **source** and **destination** folders.
- Turn email and syslog notifications on/off and fill in the server details.
- Set the timer (how often files are created / how often the folder is checked).
- See live counters ("flags created", "files moved", "files deleted").
- See the **last 200 log lines** scrolling live.
- Click **Start / Stop / Restart** to control the service from the page.

If anything you type is invalid (empty folder, bad email address, bad port number, etc.), the page tells you exactly what's wrong before saving.

### What every component does

| Component | What it is | What it does |
|---|---|---|
| **T(x) Windows Service** | A program that runs in the background on the Sender machine | Creates flag files, moves files to the destination, writes log entries |
| **R(x) Windows Service** | A program that runs in the background on the Receiver machine | Deletes any file that arrives, sends an email + syslog notification, writes log entries |
| **UI T(x)** / **UI R(x)** | A small web page hosted by each service | Lets you configure folders, SMTP, syslog, timers; shows live status and logs |
| **Configuration file** | A JSON file under `C:\ProgramData\FlagExercise\<Role>\config.json` | Stores everything you set in the UI |
| **Log file** | A text file under `C:\ProgramData\FlagExercise\<Role>\logs\` | Records every action and every error so an admin can audit what happened |
| **Installer (`install.bat`)** | Double-click batch file (self-elevates via UAC) | Asks "Tx or Rx?", builds and registers the chosen service with Windows |
| **Uninstaller (`uninstall.bat`)** | Double-click batch file (self-elevates via UAC) | Stops and removes the service and (optionally) the saved config + logs |

### How to use it (the short version)

1. On the **Sender** machine, **double-click `installer\install.bat`**, click *Yes* in the UAC prompt, and choose **Tx**.
2. On the **Receiver** machine, **double-click `installer\install.bat`**, click *Yes* in the UAC prompt, and choose **Rx**.
3. Open the browser on each machine (`localhost:5081` for Tx, `localhost:5082` for Rx) and fill in the folders and notification details.
4. Click **Save**, then **Start** if it isn't already running.
5. Watch the counters tick up. If you enabled email or syslog, you'll receive notifications every time the Receiver deletes a file.

To uninstall, **double-click `installer\uninstall.bat`** and choose the role to remove.

That's the whole exercise.

---

## Technical reference

```
  UI T(x)                                                UI R(x)
    |                                                      |
    v                                                      v
 [T(x) Windows Service]                          [R(x) Windows Service]
    |  watches Source folder                       |  watches Destination
    |  creates Flag every 5-10s (random)           |  deletes any file that arrives
    |  moves any file Source -> Destination -----> |  notifies via SMTP + Syslog
    |  logs to flat file / syslog                  |  logs to flat file / syslog
```

- **Backend:** C# / .NET 8 Worker Services (`Microsoft.Extensions.Hosting.WindowsServices`).
- **UI:** React (loaded via CDN) served by each service's embedded Kestrel HTTP host - no build step required.
- **Logging:** rolling flat file in `%ProgramData%\FlagExercise\<Role>\logs\` plus optional Syslog (RFC 3164, UDP) and SMTP notifications.
- **Configuration:** JSON file in `%ProgramData%\FlagExercise\<Role>\config.json`, edited live from the UI with both client-side and server-side validation.
- **Installer:** `installer\Install.ps1` - asks whether to install **Tx** or **Rx** on this machine. One role per machine.

### Project layout

```
FlagExercise.sln
src/
  FlagExercise.Common/          shared code: config model + validation, file logger,
                                syslog client, SMTP/Syslog notifier, embedded React UI
  FlagExercise.TxService/       T(x) worker + flag generator + file mover + UI host
  FlagExercise.RxService/       R(x) worker + file deleter + notifier + UI host
installer/
  Install.ps1                   prompts Tx/Rx, publishes, registers the service,
                                opens firewall, starts it
  Uninstall.ps1                 stops & removes the service, optionally wipes data
  install.bat                   convenience wrapper
```

### Prerequisites

- Windows 10/11 or Windows Server 2019+.
- **.NET 8 SDK** to build & publish (download from <https://dotnet.microsoft.com/>).
- Administrator PowerShell to install the service.

### Build (manual)

```powershell
dotnet restore
dotnet build  -c Release
# Publish the role you want:
dotnet publish src\FlagExercise.TxService\FlagExercise.TxService.csproj -c Release -r win-x64 --self-contained false
dotnet publish src\FlagExercise.RxService\FlagExercise.RxService.csproj -c Release -r win-x64 --self-contained false
```

### Install as a Windows Service

Open **PowerShell as Administrator** in the repo root, then:

```powershell
# Interactive: the script asks "Tx or Rx?"
powershell -ExecutionPolicy Bypass -File .\installer\Install.ps1

# Non-interactive
powershell -ExecutionPolicy Bypass -File .\installer\Install.ps1 -Role Tx
powershell -ExecutionPolicy Bypass -File .\installer\Install.ps1 -Role Rx
```

The installer:

1. Verifies elevation and the .NET SDK.
2. Publishes the chosen project into `%ProgramFiles%\FlagExercise\<Role>\`.
3. Creates a Windows Service (`FlagExercise.Tx` **or** `FlagExercise.Rx`) with auto-start and recovery actions (3 restarts on failure).
4. Opens the relevant inbound TCP port in Windows Firewall.
5. Starts the service.

The service hosts its UI on:

- **Tx UI:** http://localhost:5081
- **Rx UI:** http://localhost:5082

(Override with environment variables `FLAGEX_TX_URL` / `FLAGEX_RX_URL`.)

### Uninstall

```powershell
# Interactive
powershell -ExecutionPolicy Bypass -File .\installer\Uninstall.ps1

# Non-interactive
powershell -ExecutionPolicy Bypass -File .\installer\Uninstall.ps1 -Role Tx
powershell -ExecutionPolicy Bypass -File .\installer\Uninstall.ps1 -Role Rx -KeepData   # keep config + logs
```

### Run during development (without installing)

```powershell
# Tx, in one terminal:
dotnet run --project src\FlagExercise.TxService
# Rx, in another terminal:
dotnet run --project src\FlagExercise.RxService
```

Browse to <http://localhost:5081> (Tx) or <http://localhost:5082> (Rx).

### UI features (per service)

- Start / Stop / Restart the running worker.
- Live status panel (counters, next flag time, machine).
- Live tail of the last 200 log lines (auto-refresh every 2 s).
- Editable configuration with **client-side and server-side validation**:
  - Source folder *(Tx)* / Destination folder *(both)* — required, must differ.
  - Folder poll interval (250–600 000 ms).
  - *(Tx)* random flag-creation interval, min/max seconds.
  - SMTP enabled / host / port / SSL / from / to / user / password — with email + port checks.
  - Syslog enabled / host / port — with port check.
  - Master "Service enabled" switch and log level.

The same validation rules run in the React form **and** in
`AppConfig.Validate()` server-side (`src/FlagExercise.Common/Models/AppConfig.cs`),
so invalid configurations are rejected with a 400 + error list regardless of how they're submitted.

### Logging

- **Flat file:** `%ProgramData%\FlagExercise\<Role>\logs\<role>-service.log` — rolls at 5 MB.
- **In-memory ring buffer:** last 500 entries, exposed at `GET /api/logs?n=200` (the UI tails this).
- **Crashes:** `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` are captured in `Program.cs` and written before the process exits.
- **Syslog:** when enabled, every notification is also sent as RFC 3164 UDP to the configured host.

### REST API (used by the UI)

| Method | Path           | Body / Query              | Purpose                                |
| ------ | -------------- | ------------------------- | -------------------------------------- |
| GET    | `/`            | —                         | React SPA                              |
| GET    | `/api/config`  | —                         | Current configuration                  |
| POST   | `/api/config`  | `AppConfig` JSON          | Validate + save (returns 400 + errors) |
| GET    | `/api/status`  | —                         | Counters, running flag, machine, cfg   |
| GET    | `/api/logs`    | `?n=200`                  | Recent log lines                       |
| POST   | `/api/control` | `{"action":"start\|stop\|restart"}` | Control the worker loop          |


### Operations cheat-sheet

```powershell
# Status
sc.exe query FlagExercise.Tx
sc.exe query FlagExercise.Rx

# Stop / start
sc.exe stop  FlagExercise.Tx
sc.exe start FlagExercise.Tx

# Tail logs
Get-Content "$Env:ProgramData\FlagExercise\Tx\logs\tx-service.log" -Tail 50 -Wait
Get-Content "$Env:ProgramData\FlagExercise\Rx\logs\rx-service.log" -Tail 50 -Wait
```

### Testing the full flow on two machines

1. On **Machine A** run `Install.ps1` and choose **Tx**.
2. On **Machine B** run `Install.ps1` and choose **Rx**.
3. Make sure both machines can see the same destination folder (e.g. a UNC path like `\\fileserver\drop`).
4. Open <http://localhost:5081> on Machine A and set Source + Destination.
5. Open <http://localhost:5082> on Machine B and set the same Destination. Enable SMTP and/or Syslog and fill in your server.
6. Watch the Tx UI counters tick up (flag created -> moved). Watch the Rx UI counters tick up (file deleted). Verify your mailbox / syslog server.
