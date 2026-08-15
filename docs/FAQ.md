# Frequently Asked Questions

## What is Legion + LOQ Control?

A free and open-source project rebuilding Lenovo Legion and LOQ controls with an
unelevated UI, explicit hardware state, and a narrow privileged broker instead of a large
always-running management suite.

## Can it replace Lenovo Vantage today?

No. The current `main` rebuild provides serial-free diagnostics and atomic export, an
explicit read-only hardware-state dashboard, local battery/thermal profile previews, and
deterministic AC/battery automation previews. Hardware controls, profile application, and
automation execution remain disabled until their safety gates pass.

## What is being built next?

The release-grade read-only foundation now has hardened CI and preview packaging,
accessibility contracts, diagnostics export, and release documentation. Remaining work
includes Authenticode signing, a protected installer, and
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

It is designed to send zero hardware writes: the UI has no legacy writer reference, the
legacy assembly is globally locked, inventory only inspects WMI metadata and HID IDs, and
the optional state commands invoke a fixed allowlist of Lenovo getters. The elevated broker
accepts one read request and contains no writer. It is still pre-release software; review
[SAFETY.md](../SAFETY.md).

## Does it require administrator privileges?

No for builds, tests, inventory diagnostics, or the WPF shell. Keep the UI and inventory
unelevated. The optional `state` command also runs unelevated, but some Lenovo providers
return `AccessDenied`. `state-elevated` explicitly prompts through UAC and uses the
short-lived read-only broker. This is a development validation path; production broker use
is blocked on signing and protected installation, and hardware writes remain disabled.

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

The current read-only build does not take ownership or change settings. Future write
features will need explicit conflict detection and ownership rules before this answer can
change.

## Why is my keyboard listed as unsupported?

The current scan only recognizes the documented legacy product IDs in the repository.
`Unsupported` means that interface was not found during this scan; it does not prove the
laptop has no keyboard lighting. Submit redacted diagnostics rather than trying random HID
reports.

## Does it support AMD, Intel, or NVIDIA variants?

No blanket claim is made. Hardware support is tracked per capability, machine type, model,
and BIOS revision.

## Can I test fan curves or power limits?

Not yet. Arbitrary fan tables and power limits are explicitly prohibited until bounded
models, validation, serialization, readback, and recovery are implemented.
