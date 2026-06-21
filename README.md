# ShareDeal Attend

A locked-down Windows kiosk desktop app that displays
**https://sharedealnow.com/attend/** inside Microsoft Edge **WebView2**,
runs fullscreen with no chrome, and **auto-starts with Windows**.

- **Tech:** C# · .NET 8 · Windows Forms · Microsoft.Web.WebView2
- **Output:** a single self-contained `.exe` (no .NET install needed on employee PCs)
- **Installer:** Inno Setup, with desktop shortcut + startup registration + uninstaller

---

## 1. Project structure

```
ShareDealAttend/
├─ ShareDealAttend.sln
├─ publish.ps1                     # one-click self-contained single-file build
├─ README.md
├─ ShareDealAttend/
│  ├─ ShareDealAttend.csproj
│  ├─ app.manifest                 # DPI awareness, asInvoker, OS compat
│  ├─ appsettings.json             # URL + behavior, editable without rebuilding
│  ├─ Program.cs                   # entry point, single-instance, crash handling
│  ├─ MainForm.cs                  # WebView2 host + kiosk lockdown + offline UI
│  ├─ MainForm.Designer.cs
│  └─ Helpers/
│     ├─ AppPaths.cs               # per-user writable folders
│     ├─ AppConfig.cs              # appsettings.json loader (safe defaults)
│     ├─ Logger.cs                 # file logging to %LocalAppData%
│     └─ StartupManager.cs         # HKCU ...\CurrentVersion\Run registration
└─ installer/
   └─ setup.iss                    # Inno Setup script
```

---

## 2. Prerequisites

| Requirement | Notes |
|---|---|
| **.NET 8 SDK** | https://dotnet.microsoft.com/download/dotnet/8.0 — required to build. *(Not currently installed on this machine — install it first.)* |
| **WebView2 Runtime** | Already present on this PC (v149). Pre-installed on Win 11 and current Win 10. |
| **Visual Studio 2022** *(optional)* | Workload: *.NET desktop development*. You can also build entirely from the CLI. |
| **Inno Setup 6** *(for the installer)* | https://jrsoftware.org/isdl.php |

---

## 3. Create / open the project in Visual Studio

The project already exists — just open it:

1. Launch **Visual Studio 2022**.
2. **File → Open → Project/Solution** → select `ShareDealAttend.sln`.
3. Visual Studio restores the **Microsoft.Web.WebView2** NuGet package automatically.

> To recreate it from scratch instead: *Create new project → Windows Forms App (.NET) →
> .NET 8 →* then add the NuGet package (next section) and drop in these source files.

---

## 4. Install the NuGet package

Already referenced in the `.csproj`. If you ever need to add it manually:

- **GUI:** right-click project → *Manage NuGet Packages* → Browse → install
  **`Microsoft.Web.WebView2`**.
- **CLI:**
  ```powershell
  dotnet add ShareDealAttend\ShareDealAttend.csproj package Microsoft.Web.WebView2
  ```

---

## 5. Run in development mode

```powershell
# from the solution folder
dotnet run --project ShareDealAttend\ShareDealAttend.csproj
```

Or press **F5** in Visual Studio.

> While developing you can change behavior without touching code by editing
> `appsettings.json` (URL, fullscreen on/off, startup on/off, etc.).
> **Tip:** set `"PreventAccidentalClose": false` during testing so you can
> close the window normally. In production the hidden exit is
> **Ctrl + Shift + Alt + Q**.

---

## 6. Publish a single self-contained EXE (no .NET required on target)

Easiest — run the helper:

```powershell
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

Equivalent manual command:

```powershell
dotnet publish ShareDealAttend\ShareDealAttend.csproj -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

Result:

```
ShareDealAttend\bin\Release\net8.0-windows\win-x64\publish\ShareDealAttend.exe
```

That single `.exe` runs on any 64-bit Windows 10/11 PC with the WebView2
Runtime — **no .NET install needed**.

---

## 7. Build the installer (Inno Setup)

1. **Publish first** (section 6) so the EXE exists.
2. Open `installer\setup.iss` in **Inno Setup Compiler** and click **Compile**
   (or run `ISCC.exe installer\setup.iss`).
3. Output: `installer\Output\ShareDealAttend-Setup-1.0.0.exe`.

The installer:
- installs to **Program Files\ShareDeal Attend**,
- creates a **desktop shortcut** + Start Menu entries,
- writes the **HKLM…\Run** startup entry (machine-wide auto-start),
- launches the app right after install,
- ships a full **uninstaller** that removes the app, the startup entry, and
  per-user data.

---

## 8. How the requirements are met

| Requirement | Where |
|---|---|
| Auto-start with Windows | `StartupManager` writes `HKCU\…\Run` on every launch (self-healing); installer also writes `HKLM\…\Run`. Value name: **`ShareDeal Attend`**. |
| Show main window after startup | `Application.Run(new MainForm)` maximized, fullscreen. |
| Display the attendance URL | `appsettings.json` → `StartUrl`, loaded into WebView2. |
| Fullscreen, no border/title bar | `FormBorderStyle.None` + `WindowState.Maximized`. |
| Hide menu bar | WinForms form has no menu; status bar disabled. |
| Disable right-click menu | `AreDefaultContextMenusEnabled = false`. |
| Disable dev tools / F12 / Ctrl+Shift+I | `AreDevToolsEnabled=false`, `AreBrowserAcceleratorKeysEnabled=false`, plus form-level `KeyDown` blocking. |
| No external browser / stay in-app | `NewWindowRequested` handled in same view; optional host allow-list. |
| Internet error handling | `NavigationCompleted` → friendly offline page with **Try Again**. |
| WebView2 init + async + failure message | `EnsureCoreWebView2Async`; `ShowInitFailure` on error. |
| Disable shortcuts / prevent accidental close | `KeyDown` blocks Alt+F4/Ctrl+P/etc.; `FormClosing` cancels user-initiated closes. |
| Exception handling + logging | global handlers in `Program.cs`; `Logger` → `%LocalAppData%\ShareDeal Attend\logs`. |
| Self-contained single EXE | `publish.ps1` / csproj publish props. |
| Inno Setup installer + uninstaller | `installer\setup.iss`. |

---

## 9. Operating notes

- **Hidden admin exit:** `Ctrl + Shift + Alt + Q`.
- **Logs:** `%LocalAppData%\ShareDeal Attend\logs\app-YYYYMMDD.log`.
- **Browser data (cookies/login):** `%LocalAppData%\ShareDeal Attend\WebView2`.
- **Change the URL after deployment:** edit `appsettings.json` next to the EXE —
  no rebuild required.
- **Add an app icon:** drop `app.ico` into the `ShareDealAttend` folder and
  uncomment `<ApplicationIcon>app.ico</ApplicationIcon>` in the `.csproj`.
- If the target PC lacks the WebView2 Runtime (rare on Win 10/11), install the
  Evergreen Bootstrapper from Microsoft, or add it to the installer.
