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
  screen that contains `ip:port`. Join by IP is in (`HostAddress`): the host's lobby lists its
  IPv4 addresses, and a guest types one as `ip` or `ip:port`, with the port defaulting to 47778.
  The QR code is in too (`QrCode`, `JoinLink`). The host's lobby shows a QR code of
  `carracer://join?host=<first address>&port=<port>` beside the addresses. A guest scans it with
  the phone's own camera app, which opens the link. The game's Android intent filter (added by
  `AndroidManifestPostProcessor`) launches or switches to the game, which joins straight away;
  on a first launch it asks for a name first. The game itself needs no QR scanner. The encoder
  is byte mode, versions 1–10, and is checked module for module against python-qrcode.

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

1. Remote cars are shown at their predicted positions **with no PhysX collider**, so they never
   act as immovable walls. Each physics step, `BumpMonitor` checks this phone's car against them,
   using a 1 m contact radius per car (a little larger than the 0.6 m physics sphere).
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
- **Left half of the screen: analog steering** (the right half, left-handed; see Accessibility). Touch anywhere and drag left or right; the
  centre is wherever the thumb first lands.
- **Right half:** large **Drift** and **Brake/Reverse** buttons; item use is also on the right.
- **Auto-accelerate on by default** (settings toggle).
- **Tilt steering** is optional in settings (off by default; `TiltSteering`): turn the phone like a
  steering wheel, full lock at 30°, with 3° of dead zone, and straight ahead while the phone lies
  flat. It reads gravity in screen axes, so either landscape way round works, and it is one more
  device: the strongest steer wins, so the left thumb can still correct it.
- **Gamepad support** through the Input System: left stick steers, A or the right trigger
  accelerates, B or the left trigger brakes, either shoulder drifts, Y or X uses the item, and
  clicking the left stick looks back. The keyboard (arrows/WASD, Space to drift, E to use the
  item, Q to look back) works too, for testing in the editor.
  All devices work at once; the strongest steer wins and braking overrides accelerating
  (`DriverInput`).
- With auto-accelerate off, a **Gas** button joins Drift and Brake on the right. A steering thumb
  keeps steering until it lifts. Any other finger presses whichever button it is over (the nearest,
  where reaches overlap), so a thumb can rock from Brake to Gas; over empty screen it keeps its last
  button, so slipping off doesn't let go (`TouchControls`).
- In a race an **Item** button, labelled with the item held, sits above Brake. Only a finger that
  lands on it uses the item, once per tap; a finger sliding across it never does.
- On a track a **Look back** button sits in the top-left corner, below the HUD's text. Like Item,
  only a finger that lands on it presses it, and it holds until that finger lifts.
- **Haptics** on hits, boosts and item impacts (`HapticPatterns`): a bump's pulse grows with how hard
  it was (20–70 ms), a boost's with its tier, and pulses closer than 80 ms are dropped so a
  scrape doesn't buzz. They can be turned off, and the choice is remembered.
- **Accessibility** (Settings → Accessibility, `Accessibility`; each applies at once and is remembered):
  - **Left-handed controls:** the buttons mirror to the left and the right half steers. Look back
    stays top-left, clear of the HUD on the right.
  - **Large controls:** every button 25% bigger (`TouchControlLayout.LargeButtonScale`).
  - **Large text:** menu, lobby and HUD text 20% bigger.
  - **Reduced motion:** see §6.

## 6. Camera and HUD

- **Races:** chase camera that rises and widens its field of view with speed.
  - **Edge-of-screen indicators** for nearby cars behind or beside you (`HudIndicators`): a
    coloured arrow at the screen's edge, pointing at each car within 25 m that is more than 50°
    off straight ahead, growing to full size at 4 m. Hidden in arenas, whose camera shows everyone.
  - An **incoming-missile warning**: a flashing "Missile!" and a red arrow towards it while a
    Missile chasing you is within 80 m. The host's projectile updates say whom each Missile chases.
  - A **rear-view glance**: while Look back is held, the camera cuts to the chase framing turned
    round, in front of the car looking back past it (`ChaseCameraRig.RearView`), and cuts back on
    release. The edge arrows follow the way the camera faces.
- **Arenas:** high angled overhead camera following your car, so everyone is visible
  (`OverheadCameraRig`: 30 m up and 20 m back, never turning, its focus pulled a third of the way
  towards the arena's centre). The arena HUD shows your lives (Battle) or your place once out,
  how many are left, and Battle's clock; the track's mini-map and "Wrong way!" are hidden.
- HUD: position (2nd/6), lap (2/3), item slot (top centre), mini-map with coloured dots, drift
  charge meter, countdown, "Wrong way!" (`WrongWayDetector`: sustained reverse for 1 s, with hysteresis).
- **Nothing is told by colour alone:** the drift meter names its tier ("DRIFT", "BOOST 1–3")
  as well as colouring it, and your mini-map dot has a white ring.
- **Reduced motion** (accessibility): the chase camera keeps its resting distance, height and
  field of view at every speed (`ChaseCameraSettings.WithReducedMotion`), the car body doesn't
  lean in turns (the drift swing stays, as it shows the slide), and the Missile warning stays up
  instead of flashing.

## 7. Game modes

| Mode | Summary |
|---|---|
| **Race** | 3 laps by default. First across the line wins; the rest get a timer, then DNF in current order. |
| **Sumo / Knockout** | Floating platform that shrinks over time; last car on it wins. |
| **Battle** | Item-based arena mode. |
| **Championship** | A cup of races and arena events with points (§10). |
| **Practice** | Offline: the host code runs alone with bots. Time trials. |

### 7.0 Arena rules (`SumoRules`, `BattleRules`, `EliminationTracker`, host side)

- **Sumo:** a circular platform, 30 m in radius at the start, holds its size for 20 s, then
  shrinks steadily to a 10 m radius over 90 s and stays there. A car is out once its centre is past the edge or it
  has dropped 3 m below the platform. The last car on it wins.
- **Battle:** a walled bowl 35 m in radius. Everyone starts with 3 lives; each item hit that
  lands (a Shield still soaks one up) costs a life, and bumps cost nothing. The last car with
  lives left wins; after 3 minutes the most lives wins. Twelve item boxes: eight halfway out and
  four round the centre.
- **Placings:** survivors first (most lives first), then everyone else in reverse order of going
  out, each with the time they lasted. An event with one player lasts until they are out.
- **Start:** cars start evenly round a circle at 70% of the arena's radius, facing the centre.
- **Referee** (`HostRace` in Sumo or Battle mode): the countdown carries the mode. In Sumo the host
  checks every car it can see against the platform each tick; in Battle the game reports each
  spin-out (`ReportSpinOut`), which costs a life, so a hit the Shield soaks up costs nothing.
  Standings reuse the race message: `LapsCompleted` carries lives and `FinishPosition` a place
  once out. Results carry how long each player lasted as the race time.
- **Venues** (`ArenaLayout`, `ArenaVenues`): both arenas sit in the track scene, far enough off
  either end of it that nothing overlaps: the Sumo platform floats 40 m up, with a catch floor
  30 m below it for the cars that fall off, and the Battle bowl sits at ground level. The Battle
  item boxes share the track's box list; each event only reaches its own. Every phone sizes the
  shrinking platform from the shared start time. The host picks the mode in the lobby (remembered).
  Out players lose their controls; the host reports spin-outs for its own car, for guests (the
  rising edge of their `SpunOut` flag) and for bots, which an `ArenaBotDriver` drives in arenas.

### 7.1 Race rules (`RaceTracker`, host side)

- Checkpoints are evenly spaced; **checkpoint 0 is the start/finish line** and the **grid sits
  behind it**.
- A car must cross 0, then 1…N-1, then 0 again to complete a lap. Reports must arrive **strictly
  in order**; anything else is ignored, which stops shortcuts, reversing over the line and
  duplicate triggers. Guests run the same rule locally and only report the expected next checkpoint.
- Standings = finished racers by finishing position, then checkpoints passed + fraction towards
  the next checkpoint (from track geometry).

### 7.2 Race flow (`HostRace`, `GuestRace`, `CheckpointDetector`)

1. **Start:** the host taps Start. `RaceCountdown` gives a start time 3.5 s ahead in the host's
   clock: 0.5 s for the message to arrive, then the 3-2-1. Each guest converts it with
   `ClockSync`. Joining closes until the lobby reopens.
2. **Racing:** each phone's `CheckpointDetector` watches its own car cross the next gate (forwards,
   between the road edges plus 2 m) and reports it. The host's `RaceTracker` validates the order.
   A guest's race time is capped at the time that has actually passed on the host. The host
   broadcasts standings (order, laps, finish positions) twice a second, and announces each
   finisher.
3. **End:** when everyone has finished, or **30 s after the winner**, the host places the rest by
   race order as DNF and sends `RaceResults`.
4. **Rematch:** the host returns everyone to the same lobby (`ReturnToLobby`). Ready is cleared and
   joining reopens.

## 8. Items

Six items: **Boost**, **Shield**, **Shockwave** (radial bump), **Homing missile**,
**Oil slick / mine** (dropped hazard), **Bouncing bomb** (lobbed projectile).

| Who | Responsibility |
|---|---|
| Host | Item box pickups (first to arrive), rolls (with catch-up weighting so players further back get better items), spawning and moving projectiles and hazards, **every hit decision** (projectile, hazard, shockwave radius). |
| Firing phone | Plays launch effects and sound immediately so it feels instant. |
| Victim phone | Applies spin-out and knock-back when the host's hit arrives (`HitMessage` with `IsItemHit`), through the same force path as bumps. |
| Self-only items | Boost and Shield apply **locally at once**; the host is told afterwards. |

- **Messages** (`HostItems`, `GuestItems`): the owner sends `ItemBoxClaim` and hides the box at once;
  the host answers everyone with `ItemBoxTaken` (who, which item, respawn time in the host's clock).
  The owner sends `ItemUsed` and shows the effect at once; the host checks the player held that
  item, stamps the time and relays it to everyone. A claim the host never answers stops blocking
  new claims after 1 s. A new race puts every box back and empties every slot.
- **Boost** adds 12 m/s of top speed for 1.8 s, a little more than a top-tier drift boost, through
  the same path (`CarModel.Boost`). **Shield** lasts 8 s and soaks up the first item hit (`Shield`);
  other phones see it from the `Shielded` car-state flag, as a cyan ring.
- **Item hits** (`ItemEffects`, decided in `HostItems`): a **Shockwave** hits every other car within
  9 m, pushing it 14 m/s straight away. An **Oil slick** lands 3 m behind its dropper, is harmless
  for 0.5 s, lasts 20 s and catches the first car on it (its dropper too), then is gone. The host
  tells everyone with `HazardGone`, and slicks that time out are removed everywhere without a
  message. Each relayed `ItemUsed` carries a host-assigned effect id for this. A hit reaches its
  victim as a `HitMessage` with `IsItemHit`: a raised Shield soaks it up, otherwise the car takes
  the push plus a small pop and **spins out** for 1 s (no control; it turns 1.5 times a second and
  its slide dies away; `CarModel.SpinOut`).
- **Missiles and Bombs** (`ProjectileRules`) are simulated only by the host, which sends their
  positions 20 times a second (`Projectiles`) and says when each is gone (`HazardGone`). A
  **Missile** starts 2.5 m ahead and flies at 40 m/s at its launch height, turning at up to 220°/s
  towards the car one place ahead in the standings (straight on if fired by the leader). It hits
  the first car it reaches, pushing it 10 m/s along its flight, or runs out after 5 s. A **Bomb** is
  lobbed forwards (14 m/s on top of the car's speed, 9 m/s up), bounces on the ground it was thrown
  from, and goes off when it meets a car (its thrower only after 0.5 s) or after 2.5 s, hitting
  everyone within 6 m, the thrower included. Both fly over walls for now; following the track comes
  with the art.
- **One item slot.** A player holding an item drives through boxes without taking them, leaving them
  for others (`ItemSlots`).
- **Boxes** (`ItemBoxField`): rows from the track definition, four across the road. A car's owner
  notices it has touched a box (centres within 1.6 m) and claims it; the host grants the first
  claim on a box that is still up and tells everyone. A taken box comes back after 3 s.
- **Catch-up rolls** (`ItemRoll`): the odds are interpolated between a leader's table (mostly
  Shield and Oil slick, never a Missile) and last place's (mostly Boost, Missile and Shockwave,
  never Oil) by how far back the player is. A lone racer rolls from the mid-pack odds.

## 9. Bots

- The host chooses bot fill (**off / up to 4 / up to 6**) and **difficulty** in the lobby.
- Bots run **on the host** as host-owned cars, using the same network path, bumping and items as
  players.
- **In the roster** (`LobbyRoster.AddBot`, `HostSession.AddBotsForEvent`): when the host starts a
  race, bots are added until there are as many cars as the bot fill (never past a full session), so
  a bot never takes a place a friend could join into. They get the next free id and colour and a
  name (Turbo, Sprocket, Nitro…), are always ready, and are marked `IsBot` in the roster so every
  phone shows "(bot)". They leave again when the lobby reopens. The host reports their checkpoints
  (`HostRace.ReportBotCheckpoint`) and claims and uses their items (`HostItems.*ForBot`), and an
  item hit aimed at a bot stays on the host (`HostSession.BotHitReceived`).
- **On the track** (`BotCars`, host only): each bot gets a real car, with physics and walls like
  anyone's (no haptics), driven by its `BotDriver`. The host lines it up on its grid slot and holds
  it through the countdown, reports its checkpoints, picks up and uses its items, bumps it off other
  phones' cars with a `BumpMonitor` per bot (the host's own car and other bots collide physically),
  applies item hits through its own `Shield`, and sends its state at 30 Hz with `BotControlled`.
  Other phones see bots as ordinary remote cars. The lobby's **Bots** button cycles off / fill to 4
  / fill to 6, with Easy / Normal / Hard beside it; both are remembered.
- Behaviour: follow the racing line generated from track data, avoid walls, ram nearby
  rivals, use items opportunistically. In arenas, chase opponents and avoid edges.
- **Arena driving** (`ArenaBotDriver`): a bot hunts the nearest rival (in Sumo it aims 1.5 m past
  them on the side away from the centre, so the hit pushes them out) or, alone, circles the
  middle. Within 3 m plus 0.5 s of travel of the edge it turns for the centre, lifting off if
  heading outwards.
  Its top speed is capped at 0.6 m/s per metre of arena radius, so it slows as the platform shrinks.
- Difficulty scales top speed, line accuracy and item timing, with optional catch-up.
- A guest who disconnects mid-race is **taken over by a bot** (shown as "(bot)").
- **Driving** (`BotDriver`): a bot chases a point on the track 6 m plus 0.45 s of travel ahead,
  wandering a little either side of the centre line (its seed varies the wander, so bots don't
  share a line). It judges the bend over the next 10 m plus 1.2 s of travel and slows to keep its
  cornering under its skill's sideways acceleration. A rival within 10 m and about 35° of straight
  ahead gets rammed. Bots make the same `CarInput` a player's controls do, so their cars use the
  same model and physics.
- **Difficulty** (`BotSkill`): Easy drives at 82% of top speed, corners at up to 10 m/s², wanders
  2 m and waits 2.5 s before using an item; Normal 92%, 13 m/s², 1.2 m, 1.2 s; Hard 100%, 16 m/s²,
  0.5 m, 0.4 s. On the sample oval that is about 18.7, 16.8 and 15.6 s a lap.
- **Items** (`BotItems`): Boost on a straight, Shield straight away, Shockwave with a rival within
  7 m, Missile when there is a car ahead within 60 m, Oil with a rival behind within 20 m, Bomb
  with a rival ahead within 25 m. Anything held for 10 s is used anyway.

## 10. Championship (`Championship`)

- Cups: **Grand Prix** (a race on every track: 5) and **Mixed Cup** (the same 5 races + Sumo + Battle), or custom.
- Points for 1st to 6th: **10 / 8 / 6 / 4 / 2 / 1**, the same for races and arenas. Arena order is
  last car standing first, then reverse elimination order.
- **Reverse grid** (leader starts last) is on by default as a host toggle.
- Tiebreaks: most wins, then best finish in the latest event, then name.
- Between events: a standings screen and a 10 s auto-advance that the host can pause.
  Built (`CupPanel`): the host's lobby cycles **Event: single / Grand Prix / Mixed Cup**, with a
  reverse-grid toggle and **Resume** for a saved cup (both remembered; the save lives in
  PlayerPrefs). The results screen shows the cup table beside the results with the countdown;
  the host gets Pause/Resume, Next event and Leave cup (which keeps the save), and Back to lobby
  once the cup is won. Cars start on their grid order's slot.
- The host **auto-saves after every event**; a dead session can be **resumed**, and players
  reclaim their points by name (ignoring case and spaces at the ends). Names are unique within a
  lobby; device ids aren't needed on a LAN of friends.
- **Flow** (`Cups`, `CupRun`, host side): Grand Prix is every track in catalog order; Mixed Cup adds
  Sumo then Battle. After each event `CupRun` scores the results, sends everyone a
  `CupStandingsMessage` (the table and when the next event starts) and saves. The next event
  starts 10 s later straight from the results screen, unless the host pauses; the host can also
  skip the wait. The countdown carries the grid order (pole first, protocol 11) from the table,
  reversed with reverse grid, so newcomers start at the front with reverse grid and at the back
  without.

## 11. Lobby and identity

1. First launch: choose a display name (stored on the device; no accounts).
2. Menu: **Host Game**, **Join Game** (discovered list + join by IP/QR), **Practice**, **Garage**,
   **Settings**. Built (`MainMenu`): name entry on first launch, Host, Join (game cards + join by
   IP) and Practice (a free drive), joining from the host's QR code, Garage and Settings.
   - **Settings** gathers sound and music volume, graphics quality, the performance readout,
     haptics, auto-accelerate and tilt steering on one page, with an Accessibility page beside it
     (§5); each choice is remembered as soon as it changes. Haptics and
     auto-accelerate can also be switched while driving.
3. Garage: car body (4–6 Kenney models) and colour. Colour clashes are auto-adjusted for the
   later joiner, and the palette is colour-blind safe.
   - Built so far: six Car Kit bodies (`CarModels.Bodies`: race, race future, sedan sports,
     hatchback sports, SUV luxury and van), each drawn 2.43 m long so they fill the same physics
     car and drive the same. The choice (`Garage`, remembered on the phone) goes in the join request;
     the host keeps the player's colour unless someone has it, when they get the next free colour
     along (`LobbyRoster`), and the roster tells every phone each player's body. Bots take the
     bodies in turn. Practice uses the garage car.
   - The **Garage** page (from the main menu) shows the car turning on a turntable (`GaragePreview`:
     its own camera, far below the menu, rendering to a texture), arrows to step through the
     bodies, and a swatch per colour. Choices are remembered at once. Picking a car in the lobby
     isn't possible yet: change it in the garage before hosting or joining.
4. Host lobby: mode, track/arena (or random), laps, items on/off and frequency, bots and
   difficulty. **The host decides** (no voting for now).
5. Guests tap **Ready**; the host taps **Start** to begin a synced countdown.
6. After an event: results, then **back to the same lobby** for a one-tap rematch.

## 12. Disconnects

| Situation | Behaviour |
|---|---|
| Guest drops mid-race | A bot takes over the car (`HostSession.PlayerTakenOver`): the same id, name, colour, race progress and held item, now marked as a bot. The host's `BotCars` carries on from where the car was last seen, at its speed, with its laps (`CheckpointDetector.Resume`). Other phones just see the car carry on. The bot leaves with the others when the lobby reopens. Leaving in the lobby is still just leaving. |
| Guest rejoins | Lobby only (no joining mid-race); reclaims their championship slot. |
| App goes to the background | 10 s grace with the car coasting, then a bot takes over; control returns if the race is still running. Built: the host's `HostSession` takes over any guest it hasn't heard from for 10 s during an event (`PlayerTakenOver`, as for a drop). The first message from them afterwards gives the car back (`PlayerReturned`): the host's `BotCars` sends a `ControlReturnedMessage` (protocol 12) with where the car got to and its checkpoint count, and the guest carries on from there. A guest still away when the event ends is back in the lobby as a player. |
| Host leaves | Session ends: "Host left". The host gets a confirmation before leaving. The screen is kept awake during sessions. |
| Wi-Fi drops | "Reconnecting…" until the NGO timeout (~5–10 s), then back to the menu. Built: a guest that hasn't heard from the host for 1.5 s shows "Reconnecting…" (`GuestSession.HostQuiet`). After 10 s it gives up (`HostLost`), shows why, and is back at the main menu 4 s later with the reason under the title. The transport's own timeout stays long so a guest can come back from the background (row above), and time in the background doesn't count as silence. The same 4 s return applies whenever a session ends ("The host left"). |

## 13. Tracks, arenas and art

- **Tracks are data**: `TrackDefinition` holds closed-spline control points with width,
  banking and elevation, plus checkpoint count, laps and item box rows. The built-in tracks are
  defined in code (`SampleTracks`) so `dotnet test` can check them; the fields stay
  `JsonUtility`-friendly for track files later.
- **Everything else is generated** from it: road and wall meshes (`RoadMeshBuilder`),
  checkpoint gates, start grid (two staggered columns behind the line), item box positions and
  the bot racing line (`TrackLayout`). Arenas will work the same way (platform shape + edge type).
- Content: **5 tracks** and **2 arenas** (Sumo platform, Battle bowl):
  - **Pebble Oval** (`oval`, ~390 m): an easy rounded rectangle.
  - **Canyon Hairpins** (`canyon`, ~590 m): a jump over a crest on the opening straight, two
    hairpins, a chicane and a fast sweeper.
  - **Crossover Eight** (`figure-eight`, ~700 m): crosses the middle on the ground, climbs round
    the right-hand loop and crosses back on a bridge 8 m up.
  - **Seaside Sweepers** (`sweepers`, ~500 m): wide and flat; a long straight into a banked
    right-hander, a run of left-right sweepers, and a wide hairpin home.
  - **Switchback Hill** (`switchbacks`, ~480 m): up a long ramp, round a switchback near the top,
    along a ridge 9 m up, and down through a long, sweeping bend.
  - Both cups race every track, so a new track joins them (a test enforces it).
- **In the scene** (`TrackCatalog`): every track is built at once, each moved to its own patch
  of grass clear of the others and of the arenas. The host picks the track in the lobby
  (remembered); the countdown carries its index (protocol 10) and everyone lines up on that
  track's grid. Each phone's checkpoint detector, its bots' drivers and the mini-map switch with
  it. The item box list holds every track's boxes and the Battle bowl's; each event only reaches
  its own.
- **Track checks** (`TrackCatalogTests`): every track keeps its roads 4 m apart (or 6 m above
  each other), has no slope over 20% or bend under an 11 m radius, and bots lap it at every
  difficulty without leaving the road.
- **Crossovers:** `FindNearestDistance` measures in 3D, so a car under the bridge matches its own
  road, and checkpoint gates only reach 2 m below and 5 m above the road, so a car on the bridge
  doesn't cross the gate below it.
- **Art:** CC0 low-poly packs (Kenney Car Kit, Racing Kit), flat-shaded, bright toy-like look.
  - **Cars** are Kenney's race car (`CarModels`), read from its OBJ in `Resources/Models` by the
    game itself (`ObjModel`, mirroring x for Unity) rather than a model importer, so nothing
    depends on import settings or the pipeline's default shader. The kit's cars share one palette
    texture; each player's copy has the bodywork's strip of it recoloured to their colour, keeping
    its shading (`PaintRecolour`). A car is one mesh and one material, shared by every car of that
    colour. If the files can't be loaded the primitive placeholder is built instead.
  - **Track scenery** (`TrackScenery`, from the Racing Kit): a gantry over the start line, a metre
    past each wall; red and green banner towers beside it; up to three grandstands along the start
    straight, facing the road; and trees (two sizes, 6–9× scale, random turns) tried every 14 m on
    each side, 6–20 m beyond the wall. Nothing goes within 3 m of any road edge (so not on an
    oval's infield edge or a figure-eight's crossing), near a raised or sunken stretch, or on
    another piece, and the placement is the same every time. Pieces are coloured by their OBJ
    materials and baked into one mesh per colour (`SceneryMesh`), so a track's scenery is a handful
    of draw calls, with no colliders: it is all behind the walls.
- **Audio:** CC0 sound effects (Kenney's Impact, Sci-fi and Interface packs, mono 22 kHz in
  `Resources/Audio`; see `docs/credits.md`), a crunch scaled by impact (light, medium or heavy, on
  the same range as the bump's haptic pulse), per-item sounds where each is used, a bang where a
  Missile ends or a Bomb goes off, a chime on picking up an item, a whoosh on each boost, and
  separate music and sound-effects volume (Off, Low, Medium, High; music starts at Medium).
  - **Music** (`MusicChoice`): CC0 tunes from OpenGameArt in `Resources/Music`, streamed. The menus
    and a session's lobby play the menu tune; each track has its own tune while racing or
    practising on it, and both arenas share one. A change of tune fades out and in over 0.6 s, and
    the music carries on across screens.
  - **Engines** are made in code, not recorded, so they loop without a click (`EngineTone`): a
    stack of harmonics pulsing like an engine's firing. Every car has one, its pitch and loudness
    rising with speed (`SoundRules`) and a touch higher while boosting; other cars' fade with
    distance, and each car's note is slightly different so several don't drone in unison.

## 14. Devices and performance

- **Minimum Android 8.0 (API 26)**; target API follows Google Play's requirement. ARM64, IL2CPP.
- **Installs to internal storage only** (`internalOnly`, not Unity's default `preferExternal`), to
  match what install sessions ask for. With `preferExternal`, installing the APK from the Files app
  logged the manifest attribute as dropped and the install was abandoned ("Cancelling"), while
  `adb install` worked.
- **Baseline device: Pixel 2 XL** (Snapdragon 835 / Adreno 540, 1440×2880). Low/Medium presets
  aim for **60 fps** and never drop below 30, rendering at **0.65–0.75 of native resolution**.
- Vulkan by default, **GLES3 on Adreno 5xx** (Vulkan device filter list).
- Frame rate capped at 60. Quality preset is picked automatically on first launch, with a
  manual override. Built (`QualityPresets`, `GraphicsQuality`): the level is the lower of what
  the GPU and the memory allow. Adreno 5xx, Mali before G72 or under 4.5 GB is **Low** (0.65 of
  native resolution, no MSAA). Adreno 600–639, Mali G72–G76 or under 7 GB is **Medium** (0.75,
  2× MSAA). Anything newer is **High** (full resolution, 4× MSAA). An unrecognised GPU goes by
  memory. The main menu's Graphics button cycles auto / Low / Medium / High (remembered).
- **Only the current track is drawn.** Every track is built at once, and the 600 m view would
  otherwise draw all of them; the others' road, walls and scenery are hidden (colliders kept)
  until racing moves there. Their grass stays, so the world doesn't end at the track's edge.
- **Low quality keeps half the trees** (`QualitySettingsValues.TreeShare`): every other tree that
  higher levels place, in the same spots. Takes effect the next time a track screen is built.
- Worst-case benchmark: **Pixel 2 XL hosting 6 cars with items**, plus a 20-minute session for
  thermal throttling.
- Network: 30 Hz × 43 bytes × 6 cars is about 7.7 KB/s per phone, before transport overhead.
- **Performance readout** (`PerfStats`, `PerfOverlay`): small text at the top centre of every
  screen with the frame rate and slowest frame over the last 2 s, how many frames missed 30 fps,
  draw calls, memory in use and this phone's session traffic (up and down, per second). On by
  default in development builds, and switched in Settings ("Perf stats"). Draw calls and memory
  only report in development builds; release builds show the frame and traffic figures.

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
| **M5: Robustness** | Background grace with bot takeover and return, "Reconnecting…" on Wi-Fi drops, join by QR code, automatic quality presets. |
| **M6: Race feel** | Edge-of-screen indicators, incoming-missile warning, rear-view glance, audio (engines, crunches, items, music). |
| **M7: Art and settings** | Kenney Car Kit cars and Racing Kit track dressing in place of the primitive placeholders; a settings screen gathering sound and music volume, graphics, haptics, auto-accelerate and tilt steering. |
| **M8: Garage and tracks** | A garage of six Car Kit bodies and the colour-blind-safe colours, sent to every phone; two more tracks (Seaside Sweepers, Switchback Hill). |
| **M9: Performance, accessibility and voting** | A performance readout for testing on phones, lighter scenery on Low, picking a car in the lobby; an accessibility pass (text size, bigger targets, left-handed controls, reduced motion); guests vote for the track. |
| Later | Google Play internal testing track. |

Not planned: internet play, accounts, analytics, host migration, car stat differences.
