# Credits

## Models

From Kenney's [Car Kit](https://kenney.nl/assets/car-kit), released under
[CC0 1.0](http://creativecommons.org/publicdomain/zero/1.0/), unchanged except for the
`.bytes` extension, so Unity treats them as plain data.

| File in `Unity/Assets/CarRacer/Resources/Models` | Source |
| --- | --- |
| `race.obj.bytes` | Car Kit: `Models/OBJ format/race.obj` |
| `race-future.obj.bytes`, `sedan-sports.obj.bytes`, `hatchback-sports.obj.bytes`, `suv-luxury.obj.bytes`, `van.obj.bytes`, `sedan.obj.bytes`, `suv.obj.bytes`, `taxi.obj.bytes`, `truck.obj.bytes`, `delivery.obj.bytes`, `kart-oopi.obj.bytes` | Car Kit: the same names under `Models/OBJ format` |
| `colormap.png.bytes` | Car Kit: `Models/OBJ format/Textures/colormap.png` |

And from Kenney's [Racing Kit](https://kenney.nl/assets/racing-kit), also CC0, unchanged except
for the extension: `overhead`, `grandStand`, `treeLarge`, `treeSmall`, `bannerTowerRed` and
`bannerTowerGreen` (each `.obj.bytes` with its `.mtl.bytes`, from `Models/OBJ format`).

## Sound effects

From [Kenney](https://www.kenney.nl), released under
[CC0 1.0](http://creativecommons.org/publicdomain/zero/1.0/). They were converted to mono
22 kHz Ogg Vorbis, and some were trimmed and faded.

| File in `Unity/Assets/CarRacer/Resources/Audio` | Source |
| --- | --- |
| `crunch_light.ogg` | Impact Sounds: `impactMetal_light_000` |
| `crunch_medium.ogg` | Impact Sounds: `impactMetal_medium_000` |
| `crunch_heavy.ogg` | Impact Sounds: `impactMetal_heavy_000` |
| `boost.ogg` | Sci-fi Sounds: `thrusterFire_000`, first 0.9 s |
| `missile.ogg` | Sci-fi Sounds: `laserLarge_000` |
| `bomb_throw.ogg` | Sci-fi Sounds: `laserRetro_000` |
| `shockwave.ogg` | Sci-fi Sounds: `lowFrequency_explosion_000`, first 1.2 s |
| `shield.ogg` | Sci-fi Sounds: `forceField_000` |
| `oil.ogg` | Sci-fi Sounds: `slime_000` |
| `explosion.ogg` | Sci-fi Sounds: `explosionCrunch_000` |
| `item_box.ogg` | Interface Sounds: `confirmation_001` |

The engine note is synthesised in code (`Core/Audio/SoundRules.cs`, `EngineTone`).

## Music

All [CC0 1.0](http://creativecommons.org/publicdomain/zero/1.0/) from OpenGameArt. Each was
re-encoded to 32 kHz stereo Ogg Vorbis.

| File in `Unity/Assets/CarRacer/Resources/Music` | Plays | Track and author |
| --- | --- | --- |
| `menu.ogg` | Menus and lobbies | [Dizzy Racing](https://opengameart.org/content/10-free-chiptune-tracks-a-bag-of-chips) by Zane Little Music |
| `oval.ogg` | The oval | [Pixel Sprinter](https://opengameart.org/content/pixel-sprinter) (loop version) by Zane Little Music |
| `canyon.ogg` | The canyon | [Fever Stadium](https://opengameart.org/content/fever-stadium) (165 bpm) by mintodog |
| `figure_eight.ogg` | The figure-eight | [Winning the Race](https://opengameart.org/content/winning-the-race) by Section 31 |
| `arena.ogg` | Sumo and Battle | [Midnight Cruiser](https://opengameart.org/content/midnight-cruiser) by Zane Little Music |
| `sweepers.ogg` | Seaside Sweepers | [The World Is Ours](https://opengameart.org/content/10-free-chiptune-tracks-a-bag-of-chips) by Zane Little Music |
| `switchbacks.ogg` | Switchback Hill | [100 Victories](https://opengameart.org/content/10-free-chiptune-tracks-a-bag-of-chips) by Zane Little Music |
