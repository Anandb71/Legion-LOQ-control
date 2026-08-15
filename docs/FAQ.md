# Frequently Asked Questions

## What is Legion + LOQ Control?

A free and open-source project rebuilding Lenovo Legion and LOQ controls with an
unelevated UI, explicit hardware state, and a narrow privileged broker instead of a large
always-running management suite.

## Can it replace Lenovo Vantage today?

Not yet. The current `main` rebuild provides serial-free diagnostics and atomic export, an
explicit hardware-state dashboard, local battery/thermal profile previews, and
deterministic AC/battery automation previews. Thermal, overdrive, integrated-GPU, and
battery apply, and 4-zone keyboard brightness require an explicit click on the session
broker. Windows asks once when that session starts.
The OEM fan table is read-only. Per-zone colors, fan-table writes, profile application,
and automation execution remain disabled.

## What is being built next?

The release-grade foundation now has hardened CI, preview packaging, accessibility
contracts, diagnostics export, and the first typed apply path. Remaining work includes
profile Apply, opt-in automation, Authenticode signing, a protected installer, and
supported Windows/scaling validation. See the [rebuild roadmap](ROADMAP.md).

## Can a saved profile change my hardware?

No. A profile is currently a strictly validated local draft. New, save, delete, and preview
operations do not launch the broker, and the UI contains no Apply command.

## Can an automation rule change my hardware?

No. A rule is a strictly validated local preview. The app observes AC/battery state once
when the workspace initializes and again only after an explicit refresh. It evaluates which
profile would win, but there is no watcher, scheduler, runner, broker call, or profile Apply
command.

## Is the current build safe to run?

Inventory, refresh, preview, and export send no hardware writes. Dashboard apply can
change five typed states after an explicit click on the session broker. The UI has no legacy
writer reference, the legacy assembly is globally locked, and the broker has no generic
IOCTL or caller-supplied WMI name. It is still pre-release software; review
[SAFETY.md](../SAFETY.md).

## Does it require administrator privileges?

No for builds, tests, inventory diagnostics, or the WPF shell. Keep the UI and inventory
unelevated. The optional `state` command also runs unelevated, but some Lenovo providers
return `AccessDenied`. `state-elevated` prompts through UAC for that command. The
dashboard asks once for a session-lived broker, then reuses it. This is a development
validation path; production broker
use is blocked on signing and protected installation.

## What data does diagnostics collect?

Inventory collects manufacturer, product/model, machine type, BIOS version, WMI
method-name presence, known Lenovo HID product-ID presence, evidence codes, and
timestamps. The state command emits typed state/read outcomes and stable error codes. Both
exclude serial numbers, usernames, device paths, and telemetry.

The dashboard export contains only the inventory and any hardware snapshot already retained
by the current session. It does not read the profile or automation stores, start a new
hardware read, request elevation, or upload anything. Model, BIOS, and timestamps can still
identify a device configuration, so review the JSON before sharing it.

## Why are detected interfaces marked Unknown?

A class or USB interface existing does not prove that a protocol is safe on a specific
firmware revision. `Supported` requires fixtures, readback behavior, recovery testing, and
exact model/BIOS evidence.

## Why is battery mode unavailable in the unelevated `state` command?

Lenovo's `GetPowerChargeMode` is not the conservation/rapid-charge mode getter; it reports
a charging or power-suitability condition. Battery mode uses a separate Energy driver
protocol. The rebuild refuses to infer the wrong state. The `state-elevated` broker now has
one isolated, validated, zero-access EnergyDrv reader; the unelevated path deliberately
does not open that device.

## Can I run it alongside Lenovo Vantage?

Yes, but dashboard apply can change the same thermal, overdrive, GPU, battery, and
keyboard-brightness settings Vantage owns. Use one owner at a time. Profile Apply and
automation execution are still disabled.

## Why is my keyboard listed as unsupported?

The scan now treats ITE `048D:C993` as a 4-zone candidate in addition to the older
`C935`/`C955` IDs. `Unsupported` still means only that the scanned IDs were absent; it
does not prove the laptop has no lighting. This LOQ's 4-zone controller is `C993`.
Spectrum remains unrecognized unless its HID ID appears.

## Does it support AMD, Intel, or NVIDIA variants?

No blanket claim is made. Hardware support is tracked per capability, machine type, model,
and BIOS revision.

## Can I test fan curves or power limits?

No. The dashboard can show the OEM `Fan_Get_Table` result after an elevated refresh.
`Fan_Set_Table`, freeform curves, and power limits stay prohibited. This LOQ BIOS has
no full-speed fan methods.
