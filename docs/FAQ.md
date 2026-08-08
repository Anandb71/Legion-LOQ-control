# Frequently Asked Questions

## What is Legion + LOQ Control?

A free and open-source project rebuilding Lenovo Legion and LOQ controls with an
unelevated UI, explicit hardware state, and a narrow privileged broker instead of a large
always-running management suite.

## Can it replace Lenovo Vantage today?

No. The current `rebuild/v1` application provides read-only diagnostics only. Hardware
controls, profiles, and automation remain disabled until their safety gates pass.

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

## Why are detected interfaces marked Unknown?

A class or USB interface existing does not prove that a protocol is safe on a specific
firmware revision. `Supported` requires fixtures, readback behavior, recovery testing, and
exact model/BIOS evidence.

## Why is battery mode unavailable while other states have getters?

Lenovo's `GetPowerChargeMode` is not the conservation/rapid-charge mode getter; it reports
a charging or power-suitability condition. Battery mode uses a separate Energy driver
protocol. The rebuild refuses to infer the wrong state and will add that read only after an
isolated, validated driver adapter exists.

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
