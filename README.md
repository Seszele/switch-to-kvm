# SwitchToKVM

[![Release](https://img.shields.io/github/v/release/Seszele/switch-to-kvm)](https://github.com/Seszele/switch-to-kvm/releases/latest)
[![CI](https://github.com/Seszele/switch-to-kvm/actions/workflows/ci.yml/badge.svg)](https://github.com/Seszele/switch-to-kvm/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

A small Windows tray app that changes your monitor's input when you press your USB switch. With it, a plain USB switch and one monitor work more or less like a KVM.

https://github.com/user-attachments/assets/743548b2-0148-4818-8971-5d359d805bfc

## How it works

When you press the switch, your keyboard and mouse disappear from this computer. The app notices that and tells the monitor to change input over DDC/CI. When they come back, it switches the input back. It only has to run on one of the two computers, which helps if the other one is a work laptop you can't install things on.

Why not just buy a KVM switch? Good ones for high resolution and high refresh rate monitors are hard to find and pricey, and some of them make VRR flicker. A USB switch costs about $10, and since the video doesn't go through it, the monitor stays plugged straight into both computers.

## Install

Grab the installer from [Releases](https://github.com/Seszele/switch-to-kvm/releases/latest).

## Setup

The first time you run it, it walks you through three steps:

1. Pick your monitor
2. Check it still listens while showing the other computer
3. Press your switch, so it learns which device to watch

## Will it work with my monitor?

It needs DDC/CI, which most monitors have. Sometimes it's turned off in the monitor's menu, so check there if setup can't find yours.

The catch is that the monitor has to accept commands from the computer it's *not* currently showing. A lot of monitors do, some don't. Setup tests this for you.

If yours doesn't, install it on both computers. Each one only ever switches the monitor away from itself, and that always works.

## Command line

If you tick "Add to PATH" in the installer, you also get `stkvm`:

```
stkvm status     what the app is doing
stkvm here       switch the monitor to this computer
stkvm away       switch it to the other one
stkvm settings   open settings
```

Run `stkvm` with no arguments to see everything.

## Good to know

- It's a normal tray app, not a service.
- Config and log live in `%APPDATA%\SwitchToKVM`.
- The only network call is checking GitHub for new releases.
- Windows only for now. Linux and macOS maybe later.

## Building

You need the .NET 11 SDK.

```
dotnet build
dotnet test
```

The installer is built with `installer/build.ps1` (needs Inno Setup).

## License

MIT
