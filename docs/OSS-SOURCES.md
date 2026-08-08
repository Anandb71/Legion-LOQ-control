# Open-Source Sources and Dependencies

This ledger records every external project used as a dependency, protocol reference,
or source of adapted code. An entry in the candidate list is not approval to import it.

## Rules

1. Pin packages and reference repositories to an exact version or commit.
2. Verify the license before copying code or adding a package.
3. Prefer a maintained package behind a local adapter over copied source.
4. Record copied or adapted files in `PROVENANCE.md` before merging them.
5. Preserve copyright, license, and modification notices.
6. Benchmark startup time, idle memory, idle CPU, and package size before accepting a
   runtime dependency.
7. Never merge upstream changes directly into product code. Review and test each change.

## Active sources

| Project | Pin | License | Use | Distribution |
|---|---|---|---|---|
| [Lenovo Legion Toolkit](https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit) | `6f19ef48095a32afe439474a65e5b95cf8fa1b24` | GPL-3.0 with its repository-specific plugin exception | Reference-only submodule for protocol and behavior comparison | Not compiled or packaged |
| [HidSharp](https://github.com/IntergatedCircuits/HidSharp) | NuGet `2.6.4` | Apache-2.0 | Read-only HID inventory; quarantined legacy transport | Runtime dependency |
| [System.Management](https://github.com/dotnet/runtime) | NuGet `10.0.10` | MIT | Allowlisted identity reads, WMI metadata inventory, and bounded Lenovo getter invocation | Runtime dependency |
| [xUnit.net](https://github.com/xunit/xunit) | NuGet `xunit.v3.mtp-v2` `3.2.2` | Apache-2.0 | Core safety and contract tests | Test-only dependency |

The LLT reference points to
[`Anandb71/LenovoLegionToolkit`](https://github.com/Anandb71/LenovoLegionToolkit),
which tracks the active team repository. The archived
[`BartoszCichecki/LenovoLegionToolkit`](https://github.com/BartoszCichecki/LenovoLegionToolkit)
remains relevant for historical attribution.

## Candidates under evaluation

| Project | License | Intended role | Acceptance condition |
|---|---|---|---|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | MVVM source generators and commands | Accept after .NET 10 build and trimming review |
| [WPF UI](https://github.com/lepoco/wpfui) | MIT | Fluent WPF controls and navigation | Accept after startup, memory, high-contrast, and packaging benchmarks |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | MPL-2.0 plus third-party notices | CPU, GPU, storage, and system sensors | Accept behind a sensor adapter after privilege and polling-cost tests |
| [Microsoft.Windows.CsWin32](https://github.com/microsoft/CsWin32) | MIT | Generated Win32 interop | Accept if it removes hand-written unsafe declarations without increasing the broker surface |
| [Serilog](https://github.com/serilog/serilog) | Apache-2.0 | Structured local diagnostics | Accept with privacy redaction and bounded rolling-file storage |
| [FlaUI](https://github.com/FlaUI/FlaUI) | MIT | WPF UI automation | Test-only dependency |
| [OpenRGB](https://gitlab.com/CalcProgrammer1/OpenRGB) | GPL-2.0-or-later | Cross-check RGB device layouts and behavior | Reference only unless a specific compatible file is reviewed and attributed |
| [LenovoLegionLinux](https://github.com/johnfanv2/LenovoLegionLinux) | GPL-3.0 | Cross-check firmware capabilities and model behavior | Reference only; Linux kernel paths are not portable to Windows |

## Updating this ledger

For each accepted source, add:

- exact version or commit;
- license and notice location;
- adapter or files that use it;
- whether source was copied, adapted, or only studied;
- local modifications;
- security and update owner;
- last review date.
