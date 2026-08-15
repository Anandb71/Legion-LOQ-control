# Linux

Legion + LOQ Control talks to Lenovo firmware through Windows WMI, EnergyDrv, and HID.
There is no Linux GUI and no Linux hardware broker in this release.

What Linux does get:

- Domain and Application class libraries targeting `net10.0`
- CI on Ubuntu that restores and runs `LegionLoqControl.Application.Tests`
- the source archives GitHub attaches to every tagged release

The Linux portable zip is those libraries plus this notice. It does not open EnergyDrv,
Lenovo WMI, or ITE HID. Firmware control remains a Windows session with one UAC prompt
for the elevated broker.

Kernel work for Legion laptops on Linux lives in other projects, including
[LenovoLegionLinux](https://github.com/johnfanv2/LenovoLegionLinux). This repository does
not vendor that code.
