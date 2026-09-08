# UI refresh — first visual pass

Unified ink background, slate surfaces, champagne selection accents, keyboard focus rings, restrained popup shadows, input/scrollbar/progress styles and title bars. Buttons use event-driven hover and press highlights without moving hit rectangles; item drag feedback remains owned by ItemCell. Notifications fade out. Battle drawing is clipped to its control so effects cannot cover surrounding interface text.

Verification: Release gate passed with 826 tests; final background/tab/clipping changes then passed Release compilation (zero warnings/errors) and the isolated normal-scene renderer. Screenshot: artifacts/ui-refresh-preview/case-04.webp. Renderer errors log empty. This is a shared-theme first pass, not exhaustive page-by-page or interaction acceptance. The two-second rendering smoke is not a sustained performance benchmark.
