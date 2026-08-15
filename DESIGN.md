# Design System — Legion + LOQ Control

## Product context

- **What this is:** A fast, local-first Windows control center for Lenovo Legion and LOQ
  laptops. It replaces promotional OEM software with explicit hardware state, evidence,
  profiles, and automation.
- **Who it is for:** Owners who care about performance, battery health, predictable
  controls, and knowing exactly what the software did.
- **Peers:** Lenovo Vantage and Legion Space, Lenovo Legion Toolkit, G-Helper, and Fan
  Control.
- **Memorable quality:** Quiet precision. The first screen should feel like a trustworthy
  hardware instrument, not a game launcher or storefront.

## Research synthesis

- Vantage and Legion Space bury core controls among promotional and game-library surfaces.
- Lenovo Legion Toolkit covers a broad feature set but has grown into page-heavy navigation.
- G-Helper proves that a dense, native control surface can remain understandable and fast.
- Fan Control shows the value of data-led hierarchy for advanced hardware configuration.

The product keeps state and primary actions on one scan-friendly canvas. Deeper editors may
use dedicated pages later, but navigation never displaces the current machine state.

## Aesthetic direction

- **Direction:** Industrial/utilitarian precision.
- **Decoration:** Intentional and sparse. Structure comes from typography, alignment, and
  one-pixel separators.
- **Mood:** Calm, exact, local, and technically credible.
- **Avoid:** Gamer gradients, neon glow, promotional art, glossy cards, oversized radii,
  decorative icons, and color without meaning.

## Typography

- **Headings and data:** Bahnschrift, with Segoe UI fallback. Its DIN-derived shapes give
  the interface an instrument-panel character without shipping another font.
- **Body and controls:** Segoe UI Variable Text on supported Windows versions, then Segoe UI.
- **Data rules:** Use tabular figures where available. Keep status values short and use
  sentence case for explanations.
- **Scale:** Display 28 px; page title 20 px; section title 14 px; body 13 px; label 11 px;
  microcopy 10 px.
- **Weight:** 600 for headings and observed values, 500 for labels, 400 for body text.

## Color

- **Approach:** Restrained. Most of the interface is neutral; semantic color is evidence.
- **Canvas:** `#0B0D0F`
- **Surface:** `#12161A`
- **Raised surface:** `#181E23`
- **Hover surface:** `#20272D`
- **Structural border:** `#27313A`
- **Primary text:** `#F2F5F7`
- **Secondary text:** `#C1CBD2`
- **Muted text:** `#91A0AB`
- **Action/read accent:** `#5CC8E8`
- **Verified success:** `#63D99A`
- **Elevation/safety warning:** `#F0B35A`
- **Failure:** `#FF6B6B`

Color never carries meaning alone. Every semantic color is paired with a label or status.

## Spacing and shape

- **Base unit:** 4 px.
- **Density:** Compact-comfortable.
- **Scale:** 4, 8, 12, 16, 20, 24, 32, and 48 px.
- **Corner radius:** 2 px for small controls, 6 px for surfaces, full radius only for a
  compact status dot or badge.
- **Borders:** One physical pixel where possible. Do not stack multiple outlined cards.

## Layout

- **Approach:** Grid-disciplined.
- **Window:** Default 1080 × 720; minimum 860 × 600.
- **Header:** Device identity and safety state above all controls.
- **Primary canvas:** A 2 × 2 state matrix for battery, thermal, display, and GPU state.
- **Secondary region:** Capability evidence and model/BIOS details.
- **Primary action:** One explicit elevated refresh. It always explains why UAC is required.
- **Responsive behavior:** Collapse the state matrix to one column before reducing text
  below the defined scale.

## Motion

- **Approach:** Minimal-functional.
- **Duration:** 120 ms for hover/focus, 180 ms for state replacement.
- **Easing:** Ease-out on entry, ease-in on exit.
- **Rules:** No looping animation, parallax, spring motion, or decorative transitions.
  Respect the Windows animation setting.

## Interaction and accessibility

- Keyboard access and visible focus are required for every action.
- Minimum text contrast is WCAG AA; observed values target AAA where practical.
- Loading, unavailable, denied, stale, and failed are distinct states.
- UAC is never triggered on launch. Privileged refresh is always a direct user action.
- Unsupported controls are omitted or explained, not shown as unexplained disabled chrome.
- Hardware writes are explicit: the user chooses a value, Windows elevation runs, and
  the broker reads the result back. There is no write on launch, refresh, or preview.

## Safe choices

- Native Windows typography and control behavior preserve familiarity and reduce package
  weight.
- Semantic status labels make failure and privilege boundaries explicit.
- A fixed information hierarchy keeps the model and current state visible.

## Deliberate risks

- The instrument-like data face is less playful than gaming software; it gains trust and
  scan speed.
- Compact spacing asks more attention from casual users; it avoids the oversized,
  low-information panels common in OEM utilities.
- A single cyan accent creates less brand spectacle; it lets safety and hardware state keep
  their meaning.

## Decision log

- **2026-08-09:** Initial system approved. Quiet precision selected over gaming energy and
  conventional Fluent styling after reviewing Vantage/Legion Space, Lenovo Legion Toolkit,
  G-Helper, and hardware-monitoring dashboards.
- **2026-08-15:** 4-zone keyboard brightness sits in a third row under the 2 × 2 matrix.
  Off/Low/High only; no RGB color picker or effect chrome.
- **2026-08-15:** The OEM fan table sits in a fourth full-width row. It is read-only: no
  apply chrome, no freeform curve editor, and no invented full-speed control.
