# History

Project context: Unity/C# cowboy metroidvania POC. User: Fredrik Norum.

📌 Team update (2026-09-19T11:09:35.831+02:00): User direction changed the project engine from Unity to MonoGame; Lead's migration plan retains existing PNG assets through the MonoGame Content Pipeline and defines a code-first first slice — recorded by Scribe.

📌 Team update (2026-09-19T11:21:02.783+02:00): Fix wave complete: sprite assets load from copied PNGs with PointClamp, placeholder actor rectangles are replaced by sprite textures, and movement constants have been corrected to elapsed-time simulation — decided by Gunslinger and Pixelwright

📌 Team update (2026-09-19T11:28:12.016+02:00): Copied sprite assets and PointClamp rendering are part of the approved stabilization baseline; avoid reverting to actor rectangles during follow-up work — recorded by Scribe.

📌 Team update (2026-09-19T12:22:09.216+02:00): Release 2 layered stage visuals were implemented while preserving RoomCatalog as the authority for room metadata and collision geometry.
