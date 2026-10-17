# Car Racer: design

An arcade, bump-heavy 3D kart racer for Android. 2 to 6 friends in the same room race on their
own phones over the local Wi-Fi network. One phone hosts; there's no internet server.

This document records the decisions made during the initial design interview. Each section
gives the decision and the main reason for it. When the design changes, update this file in the
same PR.

---

## 1. Players and connectivity

| Decision | Detail |
|---|---|
| Where players are | **Same room, same Wi-Fi network** (or the host's phone hotspot). No internet play. |
| Players per session | **2–6** humans, with bots filling empty slots (see §9). |
| Topology | **One phone hosts** (server + player); the others join as clients. No dedicated server. |
| Transport | **Netcode for GameObjects (NGO) 2.x** over **Unity Transport (UDP)**. |
| Host migration | **None.** If the host leaves, the session ends for everyone. |

### 1.1 LAN discovery

The game uses its own UDP announcement, not Android NsdManager/mDNS. NsdManager has a history of
bugs and differs between phone makers, and it can't be tested on desktop.

- While a session exists, the host sends a `DiscoveryAnnouncement` **once a second** to:
  - multicast group **`239.255.42.99:47777`** (TTL 1),
  - limited broadcast **`255.255.255.255:47777`**,
  - each interface's **subnet broadcast** (best effort; interface enumeration is restricted on some Android versions).

  Some access points drop multicast but pass broadcast, so the host sends both.
- Announcement payload: magic `CRCR`, **protocol version**, random **session id**, host name (≤24 chars),
  game port (default **47778**), player count, max players, state (lobby / in event).
- Guests listen on 47777 and list hosts keyed by *(address, session id)*, so duplicates from
  multicast and broadcast merge. A host disappears after **3 s** without an announcement.
- If the protocol version doesn't match, the host is listed as **"Update required"** and
  can't be joined. **Bump `DiscoveryConstants.ProtocolVersion` whenever any network message changes.**
- Android: the app holds a **`WifiManager.MulticastLock`** only while the lobby or join screen is
  open, and declares `CHANGE_WIFI_MULTICAST_STATE` (added by `AndroidManifestPostProcessor`).
- Fallback for networks with client isolation: **join by IP**, or scan a **QR code** on the host
  screen that contains `ip:port`.

Implemented in `Core/Discovery`. PR 1 ships a temporary on-device discovery test screen.

## 2. Network authority model

**Each phone controls its own car, and the host is the referee.**

- **Owner-authoritative cars.** Each phone simulates its own car locally with no input lag, and
  sends `CarStateMessage` (position, velocity, heading, flags, sequence, timestamp; 43 bytes) at
  **30 Hz**. Bots are owned and simulated by the host.
- **Host-authoritative race state.** The host decides countdown and start time, checkpoint and
  lap validation, finish order, results, item boxes, item rolls, item hits and championship points.
- **Remote cars are shown where they are probably *now*.** `RemoteCarPredictor` extrapolates
  from the last state by velocity × (age + one-way latency), capped at 250 ms, and smooths out
  corrections over about 80 ms. Errors over 5 m snap. Being close to the truth matters more than
  smoothness here, because collisions are resolved against this position.
- **Race start sync:** the host sends `RaceCountdownMessage` with a start time in its own clock;
  each phone converts it using its estimated clock offset, so "GO" lands at the same moment everywhere.

### 2.1 Session protocol

NGO and Unity Transport carry the connection, but the game's own messages (`Core/Net`, framed by
`MessageCodec`) travel as NGO custom messages rather than as NetworkVariables or RPCs. The
formats stay engine-independent and testable, and nothing needs NGO prefab registration.
NGO only lets guests talk to the host, so the host relays car states between guests. `HostSession`
and `GuestSession` (`Core/Session`) implement the protocol below against a small
`ISessionTransport` interface, so it is unit tested without NGO.

1. **Join:** the guest connects and sends `JoinRequest` (protocol version, name). The host
   replies with `JoinReply`: accepted with a player id, or refused because the session is full,
   the version doesn't match, or an event is in progress. A refused guest is then disconnected.
2. **Roster:** the host keeps a `LobbyRoster` (lowest free player id and colour, and Ready flags)
   and sends the whole `Roster` to everyone whenever it changes. Guests send `SetReady`.
3. **Clock:** each guest sends `ClockPing` about once a second, and the host answers with
   `ClockPong`. `ClockSync` takes the offset from the fastest of the last 16 samples (NTP-style),
   which converts the host's countdown start time. Half that round trip is the one-way latency
   that `RemoteCarPredictor` uses.

### 2.2 Car-to-car contact (central to the game)

Contact must feel instant and fair for both drivers, even though no single machine sees both
cars exactly.

1. Remote cars are **kinematic** colliders at their predicted positions. PhysX doesn't resolve
   car-to-car contact, so remote cars never act as immovable walls.
2. When a phone detects contact between **its own car** and a remote car, it calls
   `BumpResolver.ComputeSelfImpulse(self, other)` and applies the result **only to its own car**.
   The formula is antisymmetric on the ground plane, so the other phone computes the equal and
   opposite push for its car from its own view. In the common case no message is needed.
   - Impulse = (1 + restitution) × approach speed ÷ (1/m₁ + 1/m₂) × exaggeration (arcade),
     clamped to [min, max] × reduced mass, plus a small equal upward "pop".
3. **Fallback:** if A detects contact that B might miss because of timing, A sends `HitMessage`
   with the impulse for B. B applies it unless it already handled contact with A in the last
   **100 ms** (`HitDeduplicator`), so contacts are neither missed nor doubled.
4. Feel helpers that hide small disagreements: slightly larger car-to-car colliders, a short
   wobble or spin-out, crunch sound scaled by impact, screen shake, and haptics.

Accepted trade-off: after a hard hit the two phones may briefly disagree by a fraction of a
metre, then converge within a few frames.

## 3. Engine and tech stack

| Area | Choice |
|---|---|
| Engine | **Unity 6.3 LTS** (`6000.3.26f1`), **URP** |
| Networking | `com.unity.netcode.gameobjects` 2.13.x, `com.unity.transport` 2.7.x |
| Input | `com.unity.inputsystem` (touch + gamepad) |
| Language | C# 9 (Unity's compiler) |
| Licence | Unity Personal (free while revenue and funding are under US$200k a year) |

### 3.1 Code layout

```
Unity/                         Unity project (GameCI projectPath)
  Assets/CarRacer/
    Core/      CarRacer.Core   engine-independent logic (noEngineReferences), .NET Standard 2.1
    Game/      CarRacer.Game   Unity runtime: MonoBehaviours, NGO wiring, UI, platform glue
    Editor/    CarRacer.Editor player settings, build script, Android manifest post-processing
    Tests/     Unity EditMode/PlayMode tests
dotnet/                        builds Core outside Unity for `dotnet test`
  CarRacer.Core/               compiles the .cs files under Unity/Assets/CarRacer/Core
  CarRacer.Core.Tests/         xUnit tests
tools/generate_unity_meta.py   deterministic .meta files for files written outside the editor
```

**Rule:** anything that can be plain C# goes in `Core` and gets `dotnet test` coverage. That
covers protocol, rules, scoring, track geometry, bot decisions and physics formulas. `Game`
stays a thin layer of Unity glue. Core has its own `Vec3`/`Vec2`; `UnityConversions` bridges them.

Player settings live in code (`ProjectConfigurator`), not hand-edited in `ProjectSettings/`.

## 4. Driving model

- **Sphere-based arcade car** (Mario Kart style). The physics body is a single rolling sphere
  rigidbody. The visible car follows it, steers by turning its heading, and tilts and leans for
  show. It almost never flips or snags, behaves the same at 30 and 60 fps, and gives simple,
  predictable collisions.
- **Fixed 50 Hz physics on every device.** A 30 fps phone drives identically to a 120 Hz one.
- **Drift and boost:** hold drift through a corner to charge, release for a boost (charge meter on the HUD).
- **Cosmetic cars only:** every body has identical handling and mass.

## 5. Controls

- **Landscape only**, safe-area aware.
- **Left half of the screen: analog steering.** Touch anywhere and drag left or right; the
  centre is wherever the thumb first lands.
- **Right half:** large **Drift** and **Brake/Reverse** buttons; item use is also on the right.
- **Auto-accelerate on by default** (settings toggle).
- **Tilt steering** is optional in settings.
- **Gamepad support** through the Input System.
- **Haptics** on hits, boosts and item impacts.

## 6. Camera and HUD

- **Races:** chase camera that rises and widens its field of view with speed.
  - **Edge-of-screen indicators** for nearby cars behind or beside you (coloured arrows that
    grow as they close in), plus an **incoming-missile warning**.
  - A **rear-view glance** button.
- **Arenas:** high angled overhead camera following your car, so everyone is visible.
- HUD: position (2nd/6), lap (2/3), item slot (top centre), mini-map with coloured dots, drift
  charge meter, countdown, "Wrong way!" (`WrongWayDetector`: sustained reverse for 1 s, with hysteresis).

## 7. Game modes

| Mode | Summary |
|---|---|
| **Race** | 3 laps by default. First across the line wins; the rest get a timer, then DNF in current order. |
| **Sumo / Knockout** | Floating platform that shrinks over time; last car on it wins. |
| **Battle** | Item-based arena mode. |
| **Championship** | A cup of races and arena events with points (§10). |
| **Practice** | Offline: the host code runs alone with bots. Time trials. |

### 7.1 Race rules (`RaceTracker`, host side)

- Checkpoints are evenly spaced; **checkpoint 0 is the start/finish line** and the **grid sits
  behind it**.
- A car must cross 0, then 1…N-1, then 0 again to complete a lap. Reports must arrive **strictly
  in order**; anything else is ignored, which stops shortcuts, reversing over the line and
  duplicate triggers. Guests run the same rule locally and only report the expected next checkpoint.
- Standings = finished racers by finishing position, then checkpoints passed + fraction towards
  the next checkpoint (from track geometry).

## 8. Items

Six items: **Boost**, **Shield**, **Shockwave** (radial bump), **Homing missile**,
**Oil slick / mine** (dropped hazard), **Bouncing bomb** (lobbed projectile).

| Who | Responsibility |
|---|---|
| Host | Item box pickups (first to arrive), rolls (with catch-up weighting so players further back get better items), spawning and moving projectiles and hazards, **every hit decision** (projectile, hazard, shockwave radius). |
| Firing phone | Plays launch effects and sound immediately so it feels instant. |
| Victim phone | Applies spin-out and knock-back when the host's hit arrives (`HitMessage` with `IsItemHit`), through the same force path as bumps. |
| Self-only items | Boost and Shield apply **locally at once**; the host is told afterwards. |

## 9. Bots

- The host chooses bot fill (**off / up to 4 / up to 6**) and **difficulty** in the lobby.
- Bots run **on the host** as host-owned cars, using the same network path, bumping and items as
  players.
- Behaviour: follow the racing line generated from track data, avoid walls, ram nearby
  rivals, use items opportunistically. In arenas, chase opponents and avoid edges.
- Difficulty scales top speed, line accuracy and item timing, with optional catch-up.
- A guest who disconnects mid-race is **taken over by a bot** (shown as "(bot)").

## 10. Championship (`Championship`)

- Cups: **Grand Prix** (3 races) and **Mixed Cup** (3 races + Sumo + Battle), or custom.
- Points for 1st to 6th: **10 / 8 / 6 / 4 / 2 / 1**, the same for races and arenas. Arena order is
  last car standing first, then reverse elimination order.
- **Reverse grid** (leader starts last) is on by default as a host toggle.
- Tiebreaks: most wins, then best finish in the latest event, then name.
- Between events: a standings screen and a 10 s auto-advance that the host can pause.
- The host **auto-saves after every event**; a dead session can be **resumed**, and players
  reclaim slots by name and device id.

## 11. Lobby and identity

1. First launch: choose a display name (stored on the device; no accounts).
2. Menu: **Host Game**, **Join Game** (discovered list + join by IP/QR), **Practice**, **Garage**.
3. Garage: car body (4–6 Kenney models) and colour. Colour clashes are auto-adjusted for the
   later joiner, and the palette is colour-blind safe.
4. Host lobby: mode, track/arena (or random), laps, items on/off and frequency, bots and
   difficulty. **The host decides** (no voting for now).
5. Guests tap **Ready**; the host taps **Start** to begin a synced countdown.
6. After an event: results, then **back to the same lobby** for a one-tap rematch.

## 12. Disconnects

| Situation | Behaviour |
|---|---|
| Guest drops mid-race | A bot takes over the car; standings stay intact. |
| Guest rejoins | Lobby only (no joining mid-race); reclaims their championship slot. |
| App goes to the background | 10 s grace with the car coasting, then a bot takes over; control returns if the race is still running. |
| Host leaves | Session ends: "Host left". The host gets a confirmation before leaving. The screen is kept awake during sessions. |
| Wi-Fi drops | "Reconnecting…" until the NGO timeout (~5–10 s), then back to the menu. |

## 13. Tracks, arenas and art

- **Tracks are data**: `TrackDefinition` holds closed-spline control points with width,
  banking and elevation, plus checkpoint count, laps and item box rows. Loaded with
  `JsonUtility` from a TextAsset.
- **Everything else is generated** from it: road and wall meshes (`RoadMeshBuilder`),
  checkpoint gates, start grid (two staggered columns behind the line), item box positions and
  the bot racing line (`TrackLayout`). Arenas will work the same way (platform shape + edge type).
- Initial content: **3 tracks** (an easy oval-ish circuit, a technical track with a jump, a
  figure-eight with a crossover) and **2 arenas** (Sumo platform, Battle bowl).
  `FindNearestDistance` takes a hint so it doesn't jump between roads at the crossover.
- **Art:** CC0 low-poly packs (Kenney Car Kit, Racing Kit), flat-shaded, bright toy-like look.
- **Audio:** CC0 sound effects (Kenney), engine loops that change pitch with speed, a crunch
  scaled by impact, per-item sounds, CC0 music per track, and separate music and sound-effects volume.

## 14. Devices and performance

- **Minimum Android 8.0 (API 26)**; target API follows Google Play's requirement. ARM64, IL2CPP.
- **Baseline device: Pixel 2 XL** (Snapdragon 835 / Adreno 540, 1440×2880). Low/Medium presets
  aim for **60 fps** and never drop below 30, rendering at **0.65–0.75 of native resolution**.
- Vulkan by default, **GLES3 on Adreno 5xx** (Vulkan device filter list).
- Frame rate capped at 60. Quality preset is picked automatically on first launch, with a
  manual override.
- Worst-case benchmark: **Pixel 2 XL hosting 6 cars with items**, plus a 20-minute session for
  thermal throttling.
- Network: 30 Hz × 43 bytes × 6 cars is about 7.7 KB/s per phone, before transport overhead.

## 15. CI, builds and distribution

- **`Core` workflow** (no secrets): `dotnet test` and a `.meta` file check on every push and PR.
- **`Unity` workflow** (GameCI): Unity tests and a **development APK artifact** on every PR.
  Needs `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`; skipped with a notice if they're absent.
- **`Release` workflow:** pushing a `v*` tag builds a **release-signed APK** and publishes it to
  **GitHub Releases** (Obtainium-friendly). Needs the Android keystore secrets too.
- Setup: [`ci-setup.md`](ci-setup.md).

## 16. Milestones

| Milestone | Scope |
|---|---|
| **PR 1: Foundations** | This design doc; Unity project skeleton; `Core` (discovery, messages, race rules, bump physics, prediction, spline tracks and meshes, championship) with tests; CI; discovery test screen. |
| **M1: Core loop** | Sphere car controller, touch and gamepad input, chase camera, generated oval track, NGO host/join wired to discovery, lobby UI, countdown, race, results, rematch. |
| **M2: Items** | Item boxes, the six items, host-side hit resolution, catch-up rolls. |
| **M3: Arenas and bots** | Sumo and Battle arenas, overhead camera, bots (racing line, ramming, items), bot takeover on disconnect, practice mode. |
| **M4: Championship** | Cups, points, reverse grid, standings screens, auto-save and resume. |
| Later | Track voting, Google Play internal testing track, more tracks and cars, accessibility pass. |

Not planned: internet play, accounts, analytics, host migration, car stat differences.
