# History

Project context: Unity/C# cowboy metroidvania POC. User: Fredrik Norum.

📌 Team update (2026-09-19T10:43:31.676+02:00): Animation fix implemented: seven Animator Any State mappings, transient shoot/hurt driver, locomotion suppression during transient states, and gameplay event wiring. GUIDs resolve; git diff --check is clean; Unity validation remains unavailable — decided by Gunslinger

📌 Team update (2026-09-19T11:09:35.831+02:00): User direction changed the project engine from Unity to MonoGame; implementation should target a pure C# gameplay core and thin MonoGame host, with the first slice covering movement, aim, fire/reload, enemy, damage/respawn, and checkpoint behavior — recorded by Scribe.


📌 Team update (2026-09-19T11:15:43.869+02:00): MonoGame migration wave completed as a .NET code-first project with pure gameplay core and thin MonoGame host; `dotnet build --no-restore` passed with 0 warnings/0 errors and `dotnet test --no-restore` exited successfully — logged by Scribe.

📌 Team update (2026-09-19T11:21:02.783+02:00): Fix wave complete: sprite assets load from copied PNGs with PointClamp, placeholder actor rectangles are replaced by sprite textures, and movement constants have been corrected to elapsed-time simulation — decided by Gunslinger and Pixelwright

📌 Team update (2026-09-19T11:28:12.016+02:00): Sprite-art and elapsed-time fixes are the approved completed implementation wave; preserve the passing build/test gate during follow-up stabilization — recorded by Scribe.

📌 Team update (2026-09-19T12:22:09.346+02:00): Explorer-launch diagnostics completed: host CWD and asset resolution now anchor to `AppContext.BaseDirectory`; startup exceptions log beside the executable and show a Windows MessageBox. Build/tests passed; direct `Start-Process` survival was verified, while window presentation remained unobservable in the no-desktop environment — recorded by Scribe.

📌 Team update (2026-09-19T12:22:09.216+02:00): Release 2 layered stage visuals were integrated with host plumbing; Explorer-launch diagnostics now set the executable working directory, log startup exceptions, and show a MessageBox. Build/tests passed; visible-window verification remained environment-limited.


📌 Team update (2026-09-19T17:10:01.800+02:00): Release 4 gameplay/HUD and log4net stall diagnostics are complete. Core state owns the certified play loop; runtime logging rolls `Cowbania.Host.log` at 5 MB with three backups while preserving `Cowbania.Host.startup.log`. Build/smoke checks and the 7-second host probe passed; Marshal approved — recorded by Scribe.