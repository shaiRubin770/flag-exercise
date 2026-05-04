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

You install **only one role per machine** — the installer asks which one. The two services are completely independent: each one writes its own configuration file, runs its own Windows service, hosts its own dashboard, and works on its own even if the other side has not been installed yet.

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
| **Installer (`FlagExercise-Setup.exe`)** | One-click Windows installer | Asks "Tx or Rx?", registers the chosen service with Windows, opens firewall port, starts the service |
| **Uninstaller** | Windows "Apps & features" | Stops and removes the service and (optionally) the saved config + logs |

### How to use it

1. On the **Sender** machine, **double-click `FlagExercise-Setup.exe`**, click *Yes* in the UAC prompt, and choose **Tx**.
2. On the **Receiver** machine, **double-click `FlagExercise-Setup.exe`**, click *Yes* in the UAC prompt, and choose **Rx**.
3. Open the browser on each machine (`localhost:5081` for Tx, `localhost:5082` for Rx) and fill in the folders and notification details.
4. Click **Save**, then **Start** if it isn't already running.
5. Watch the counters tick up. If you enabled email or syslog, you'll receive notifications every time the Receiver deletes a file.

To uninstall, go to **Settings → Apps → FlagExercise → Uninstall**.

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
- **Installer:** `FlagExercise-Setup.exe` — asks whether to install **Tx** or **Rx** on this machine. One role per machine.

### Project layout

```
FlagExercise.sln
src/
  FlagExercise.Common/          shared code: config model + validation, file logger,
                                syslog client, SMTP/Syslog notifier, embedded React UI
  FlagExercise.TxService/       T(x) worker + flag generator + file mover + UI host
  FlagExercise.RxService/       R(x) worker + file deleter + notifier + UI host
```

### Prerequisites

- Windows 10/11 or Windows Server 2019+.

### installer (.exe)

- **Get it:** Download `FlagExercise-Setup-1.0.0.exe` from the project's
  [GitHub Releases](https://github.com/shaiRubin770/flag-exercise/releases).
- **Run it:** Double-click the `.exe` -> accept the UAC prompt -> choose **Tx** or
  **Rx** in the wizard -> click through. The installer:
  - copies the files,
  - registers the Windows Service (auto-start + failure recovery),
  - opens the firewall port,
  - starts the service,
  - drops a **desktop shortcut** + Start Menu entries pointing at the dashboard URL,
  - and offers a final "Open the dashboard now" checkbox so the browser opens automatically when setup finishes.

  No manual post-install steps are required.
- **Uninstall:** Settings -> Apps -> FlagExercise -> Uninstall.

### Uninstall

Go to **Settings → Apps → FlagExercise → Uninstall**.

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

1. On **Machine A** double-click `FlagExercise-Setup.exe`, accept the UAC prompt, and choose **Tx**.
2. On **Machine B** double-click `FlagExercise-Setup.exe`, accept the UAC prompt, and choose **Rx**.
3. Make sure both machines can see the same destination folder (e.g. a UNC path like `\\fileserver\drop`).
4. Open <http://localhost:5081> on Machine A and set Source + Destination.
5. Open <http://localhost:5082> on Machine B and set the same Destination. Enable SMTP and/or Syslog and fill in your server.
6. Watch the Tx UI counters tick up (flag created -> moved). Watch the Rx UI counters tick up (file deleted). Verify your mailbox / syslog server.
