# PC Network Backup

A small Windows desktop utility that keeps selected folders on a user's PC
(`Desktop`, `Documents`, `Downloads`, `Pictures`, `Music`, `Videos`)
continuously mirrored to the **root** of a mapped organizational network
drive - a true one-way mirror, not a two-way sync and not a historical
backup system in its own right.

```
C:\Users\<you>\Documents  --one-way mirror-->  N:\Documents  --org backup system-->  historical recovery
```

If you delete a file on your PC, it is also removed from the network
destination on the next sync. This is intentional: your organization's
existing backup/versioning infrastructure on the network side is what
provides historical recovery, not this tool.

## How it works

- **Source of truth is always C:.** The network drive is never written back
  to the PC. Manual edits made directly on the network destination do not
  propagate back.
- **Destinations sit directly under the drive root** (`N:\Documents`), never
  under a `PC-Backup\` (or similar) subfolder.
- **File copying is done by Windows' built-in Robocopy**, not a custom
  copy engine - see `RobocopyService.cs` for the exact flags used and why.
- **A recurring Windows Task Scheduler task** (`PC Network Backup`) runs the
  sync in the background under the *current user's own logon session* (not
  SYSTEM), so the mapped drive letter and permissions are visible to it.
- **No credentials are ever stored.** The app relies entirely on the
  Windows/network permissions the signed-in user already has.
- **No telemetry, no external network calls, nothing leaves the machine**
  other than to the already-configured, already-authenticated network
  destination.

## Safety model

Because `/MIR`-style mirroring is destructive by nature (it deletes
destination files that no longer exist on the source), several gates must
all pass, in order, before any destructive sync is ever allowed to run:

1. **Destination validation** - is it really a mapped network drive, is it
   reachable, can the app actually write and delete a test file there.
2. **Existing-data warning** - if the destination folder already has files
   in it that don't correspond to anything in the source, the user is
   warned before continuing.
3. **Explicit confirmation screen** - shows the exact source -> destination
   folder pairs and a plain warning about deletion behavior.
4. **Dry-run preview** (`robocopy /L` - list-only, changes nothing) - shows
   real "files to copy / files to delete" counts before anything destructive
   runs.
5. Only after all four steps does `Save & Enable` persist the configuration,
   flip it to enabled, and register the scheduled task.

Changing the destination drive or folder selection later re-runs this
entire flow from step 1 - it never silently starts mirroring against a
newly selected drive.

Additional safety behavior:
- A locked/in-use file is skipped and retried on the next cycle - it is
  never force-unlocked, and its last-known-good copy on the destination is
  never deleted just because the source copy was temporarily unreadable.
- A global mutex (`SingleInstanceGuard`) prevents two sync runs (scheduled +
  manual, or two overlapping scheduled runs) from mirroring the same
  destination at once.
- Robocopy's multi-bit exit code is interpreted correctly - e.g. exit code
  `3` means "files were copied and old destination-only files were removed",
  which is a *success*, not a failure. Only bit 16 (serious/fatal error) or
  bit 8 (some individual file failures) are treated as errors. See
  `RobocopyExitCodeTests.cs`.
- Disabling backup does **not** delete anything already on the network
  drive; it just stops the scheduled task from running further syncs.

## Repository layout

```
/src
  PCNetworkBackup.Core/    Non-GUI logic: config, known-folder resolution,
                            network-drive discovery, Robocopy wrapper,
                            Task Scheduler integration, sync engine, logging.
  PCNetworkBackup.App/     WinForms GUI + Program.cs (also handles the
                            headless "--sync" mode invoked by the scheduled
                            task).
/tests
  PCNetworkBackup.Tests/   xUnit tests - unit tests for config/mapping/
                            command-building/exit-code logic, plus a small
                            set of real-Robocopy integration tests that only
                            ever touch throwaway temp directories, never
                            real user folders or a real network drive.
/.github/workflows/build.yml   GitHub Actions workflow (see below).
```

## Building via GitHub Actions (no Windows machine needed)

`.github/workflows/build.yml` runs on a Microsoft-hosted `windows-latest`
runner and:

1. Checks out the source.
2. Installs the .NET 8 SDK.
3. Restores dependencies.
4. Builds the app in Release configuration.
5. Runs the full test suite.
6. Publishes a **self-contained, single-file win-x64** executable
   (`dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`).
7. Uploads it as a build artifact named `PCNetworkBackup-win-x64`, containing
   `PCNetworkBackup-win-x64.exe`.

**To get the exe:** push this repo to GitHub (or trigger the workflow
manually via the "Run workflow" button, since it's also wired to
`workflow_dispatch`), then open the completed run under the **Actions** tab
and download the `PCNetworkBackup-win-x64` artifact. Unzip it to get
`PCNetworkBackup-win-x64.exe` - rename it to `PCNetworkBackup.exe` if you
like, it's fully portable either way.

## Deploying to a Windows PC

1. Copy `PCNetworkBackup.exe` anywhere on the target machine (no installer,
   no admin rights needed for normal use).
2. Double-click to run it. Select the mapped network drive, tick the
   folders to mirror, choose a sync interval, and click **Save & Enable**.
3. Walk through the confirmation and dry-run review screens.
4. Done - the app registers a background scheduled task and can be closed;
   syncing continues without the GUI open.

To stop syncing: reopen the app and click **Disable Backup** (this removes
the scheduled task but leaves everything already mirrored on the network
drive untouched).

## Windows 10 LTSC 21H2 compatibility notes

- Target framework is `net8.0-windows10.0.19041.0`, matched to the known
  target machine (build 19044.x). No Windows 11-only APIs are used.
- `robocopy.exe` and `schtasks.exe` are both built into Windows since long
  before Windows 10 and are used exactly as shipped - no extra installs.
- The app manifest requests `asInvoker` (never elevates) and declares
  support for both Windows 10 and Windows 11 via `supportedOS` entries.
- Long-path awareness is declared in the manifest (`longPathAware`), but the
  app does not otherwise assume long-path support is available, since LTSC
  21H2 needs it explicitly enabled via Group Policy/registry to take effect.

## What's intentionally out of scope

This tool is a **current-state mirror only**. It does not implement (and
should not be extended to implement) versioning, snapshots, deduplication,
cloud backup, or ransomware recovery - that is the job of the organization's
existing backup infrastructure on the network-drive side.

## Known simplifications (disclosed, not hidden)

Given the scope of this first version, a couple of things from the original
spec were implemented in a simplified form rather than left out silently:

- **First-run wizard**: implemented as the existing confirm + dry-run dialog
  sequence (`ConfirmMirrorForm` -> `DryRunSummaryForm`) rather than a
  separate multi-step wizard shell. All the safety-critical steps (explain,
  select, validate, confirm, preview, confirm again) are present; only the
  dedicated wizard *chrome* was folded into the main flow.
- **Drive-letter-changed-but-same-UNC-share recovery** (section 17 of the
  spec): not automatic yet. If the drive letter changes, destination
  validation will simply fail and the user is asked to reselect/reconfirm
  the destination through the normal safety flow - which is the safe
  fallback the spec explicitly allows when automatic recovery can't be done
  confidently.
- **"Start automatically with Windows"** is currently implemented as "keep
  the recurring scheduled task installed" - Windows Task Scheduler resumes
  a `/SC MINUTE` schedule automatically once the user logs back in after a
  reboot, so a separate logon-trigger wasn't needed for this to work.

None of these affect the core data-safety guarantees (validation, warning,
confirmation, dry-run, no writeback to C:, no silent destination changes).

## Manual test plan (real Windows 10 LTSC 21H2 hardware)

Automated tests cover config/mapping/command-building/exit-code logic and
Robocopy behavior against temp directories. The following still needs a
real machine and a real mapped drive:

1. **Initial copy** - create a file under a selected source folder, sync,
   verify it appears at `N:\<Folder>\...`.
2. **Modify** - edit the file, sync, verify the destination updates.
3. **Delete** - delete the source file, sync, verify the destination copy
   is removed (this is intentional mirror behavior).
4. **New file** - verify new files appear on the next sync.
5. **Network unavailable** - disconnect VPN/network, verify the app reports
   "Network drive unavailable, will retry automatically" without crashing,
   then verify it catches up once reconnected.
6. **Wrong destination protection** - select a destination with unrelated
   files already in it, verify the existing-data warning appears before any
   mirroring.
7. **File in use** - open a file exclusively, sync, verify it's skipped and
   retried on the next cycle rather than causing a failed sync overall.
8. **Reboot** - verify the scheduled task resumes syncing after a restart.
9. **Multiple folders** - enable several folders at once, verify each is
   mirrored independently.
10. **Drive letter change** - remap the same share to a different letter,
    verify the app asks for reconfirmation rather than silently mirroring
    to the wrong (or right, but unconfirmed) place.

## Security & privacy

- Never requests administrator elevation for normal operation.
- Stores no passwords or network credentials.
- Makes no external network calls, collects no telemetry/analytics.
- The only network destination the app ever writes to is the one the user
  explicitly selected and confirmed through the GUI.
