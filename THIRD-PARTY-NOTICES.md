# Third-Party Notices

Legion + LOQ Control uses and studies open-source software. This file summarizes the
notices that apply to the current repository. Release packaging must regenerate and
verify this list from the locked dependency graph.

## Lenovo Legion Toolkit

Copyright belongs to the Lenovo Legion Toolkit contributors.

- Active source: <https://github.com/LenovoLegionToolkit-Team/LenovoLegionToolkit>
- Historical source: <https://github.com/BartoszCichecki/LenovoLegionToolkit>
- License: GNU General Public License version 3 with the additional permission published
  in the LLT repository.

LLT is retained as a reference-only git submodule and is not compiled into release
artifacts. Legacy prototype files that state they derive from LLT are being replaced or
will retain explicit GPL attribution and modification notices.

## HidSharp

Copyright 2010-2025 James F. Bellinger.

- Source: <https://github.com/IntergatedCircuits/HidSharp>
- License: Apache License 2.0

HidSharp is used by the rebuild for non-opening HID inventory. Legacy HID write code is
quarantined and disabled.

## Microsoft .NET and System.Management

Copyright .NET Foundation and contributors.

- Source: <https://github.com/dotnet/runtime>
- License: MIT

`System.Management` is currently used to access Lenovo and Windows WMI providers.

## xUnit.net

Copyright .NET Foundation and contributors.

- Source: <https://github.com/xunit/xunit>
- License: Apache License 2.0

xUnit.net is a test-only dependency and is not distributed as an application runtime
component.

## Trademarks

Lenovo, Legion, LOQ, Vantage, and related names are trademarks of their respective
owners. This project is independent and is not affiliated with, endorsed by, or sponsored
by Lenovo.

Third-party project names are used only to identify compatibility, dependencies, or
source provenance.
