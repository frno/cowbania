# monogame-runtime

Owns `Cowbania.Host`: MonoGame lifecycle, keyboard input, rendering, audio, content loading, diagnostics, packaging, desktop launch behavior, and runtime performance.

Keeps the host thin, preserves lazy asset loading where required, and does not duplicate deterministic gameplay rules or authored room coordinates from the core.
