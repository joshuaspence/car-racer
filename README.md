# Car Racer

An arcade, bump-heavy 3D kart racer for Android. 2 to 6 friends in the same room race on their
own phones over local Wi-Fi: one phone hosts, the others find it automatically, and bots fill
the empty slots. Built with Unity 6.3 LTS and Netcode for GameObjects.

- **Design:** [`docs/design.md`](docs/design.md) records every decision so far and the milestone plan.
- **CI and releases:** [`docs/ci-setup.md`](docs/ci-setup.md) covers secrets, signing and cutting a release.

## Status

**PR 1: foundations.** The engine-independent core is implemented and tested: LAN discovery,
network messages, race rules, car-to-car bump physics, remote-car prediction, spline tracks with
generated road meshes, and championship scoring. The Unity project skeleton is in place, and a
temporary on-device screen tests LAN discovery between phones.

**M1: core loop (in progress).** The arcade car handling, touch/gamepad/keyboard controls and
chase camera are in. **Drive test** on the start screen puts one car on the sample oval for tuning.

## Layout

```
Unity/                        Unity project
  Assets/CarRacer/Core/       engine-independent game logic (no UnityEngine references)
  Assets/CarRacer/Game/       Unity runtime code
  Assets/CarRacer/Editor/     project settings, build script, Android manifest tweaks
  Assets/CarRacer/Tests/      Unity tests
dotnet/                       builds and tests Core with plain .NET
tools/                        helper scripts
docs/                         design and setup docs
```

## Quick start

```sh
# Core tests (needs the .NET 8 SDK; no Unity required)
dotnet test dotnet/CarRacer.sln
```

To work in Unity, open `Unity/` with Unity **6000.3.26f1** (with Android Build Support) and follow
[first-time setup](docs/ci-setup.md#5-first-time-unity-editor-setup-local).

### Trying LAN discovery on phones

Install the development APK (from a PR's **Unity** workflow artifacts, or built locally) on two
or more phones on the same Wi-Fi. Tap **Host** on one; the others should list it within a
couple of seconds.

Extract the APK from the artifact zip before tapping it; installing from inside the zip fails.
Development builds are signed with a shared debug key (`Unity/Keystores/debug.keystore`), so
newer ones install over older ones. Builds made before that key existed must be uninstalled once.
