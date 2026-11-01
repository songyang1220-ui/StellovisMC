# Startup Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Replace the tiny static ICO splash with the approved star gathering introduction and a smooth transition into the ready homepage.

**Architecture:** A decorative WPF FrameworkElement draws a cached sky and a small animated DrawingVisual. FormMain starts this overlay when its content is ready; the existing window entrance remains intact. The old system SplashScreen is removed.

**Tech Stack:** Existing .NET 10 WPF, DrawingContext, CompositionTarget.Rendering and MSTest. No additional packages or media assets.

**Spec:** ../specs/2026-11-01-startup-animation-design.md

## Global Constraints

- Approximately 1.1 seconds: gather stars, reveal the existing 112 pixel icon and brand, then fade into the homepage.
- No sound, flash, fake progress or blocking waits.
- Existing startup-logo preference and reduced-motion settings skip the introduction.
- Consent dialogs remain above the decorative overlay.
- Detach rendering callbacks on completion or unload; preserve existing data migration and settings.
- Update both dist/StellovisMC and D:\Minecraft\我的世界 after validation.

## Review Focus

- Very fast startup: the first displayed frame must already cover the homepage, without the tiny ICO splash.
- Animation disabled: no overlay or delay.
- Early window close: no lingering rendering callback or completion callback.
- Resize/DPI: centered artwork, sharp icon and text, no layout invalidation on each frame.
- First-run consent: the dialog layer is above the introduction.

### Task 1: Animated overlay and startup integration

**Files:** Create Controls/StartupAnimation.cs and PCL.Core.Test/UI/StartupAnimationTest.cs. Modify PCL.Core.Test.csproj, Application.xaml.cs, Modules/Launcher/Core.cs, FormMain.xaml and FormMain.xaml.cs.

**Interfaces:** `StartupAnimation.Start(bool motionEnabled)` initializes one introduction. `AdvanceFrame(TimeSpan frameTime)` advances the composition clock. `IsRunning` reports activity; `Logo` accepts the existing icon ImageSource. Completion collapses the overlay and releases the clock.

- [x] Write regression tests for changing rendered frames, completion at 1.1 seconds, disabled animation, unload and restart; use real WPF rendering on an STA thread.
- [x] Run the new tests and confirm the missing implementation fails.
- [x] Implement the cached backdrop and animated stars, icon/text easing and final fade; update only the existing DrawingVisual per frame.
- [x] Replace the ICO SplashScreen references with the overlay below PanMsgBackground, using existing preference and OS/config motion settings.
- [x] Run focused tests for the new animation, starfield and directory migration; build/publish the actual launcher.

### Task 2: Validate and deliver

- [x] Inspect representative offscreen rendered frames and actual native startup/homepage behavior; verify no startup errors or recreated PCL directory.
- [x] Obtain an independent code review of the new animation lifecycle and hookup, resolving material findings.
- [x] Update the user's actual executable and leave the launcher open on its homepage.
- [x] Record checks and any full-suite failures honestly; do not commit unrelated pre-existing workspace changes.
