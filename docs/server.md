# Running the host on a Linux server

A phone normally hosts a game, but an x86-64 Linux machine on the same network (a home server,
a NAS, a spare laptop) can host instead, so no phone pays the cost of
hosting. The server has no car of its own; the **first player to join runs the room** from their
phone, with the same settings and Start button a phone host has (design doc §2.3). When they
leave, whoever has been there longest takes over.

Each release (`v*` tag) publishes the server two ways:

- **Docker image:** `ghcr.io/spence-com-au/car-racer:<tag>` (and `:latest`)
- **Tarball:** `CarRacer-server-<tag>-linux-x86_64.tar.gz` on the Releases page

Use the same version as the phones: a different protocol version shows as "Update required".

## Docker

```sh
docker run -d --name car-racer --network host --restart unless-stopped \
  -v car-racer:/home/carracer/.config \
  ghcr.io/spence-com-au/car-racer:latest -name "Lounge"
```

- **`--network host` is required.** Phones find the server by UDP broadcast and multicast,
  which don't cross Docker's default bridge network. (Docker Desktop on macOS and Windows
  doesn't support host networking; use a Linux host.)
- The volume keeps the room's last settings and a saved cup between restarts.
- `docker logs -f car-racer` shows who joins, leaves and runs the room, and each event.

## Tarball

```sh
tar -xzf CarRacer-server-v0.11.0-linux-x86_64.tar.gz
./car-racer-server/run-server.sh -name "Lounge"
```

It needs an x86-64 Linux with glibc (any recent Ubuntu, Debian or Fedora). To keep it running,
a systemd unit along these lines works:

```ini
[Unit]
Description=Car Racer host
After=network-online.target

[Service]
User=carracer
ExecStart=/opt/car-racer-server/run-server.sh -name "Lounge"
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

## Options

| Option | Default | What it does |
|---|---|---|
| `-name <name>` | `Car Racer server` | What phones list the game as (up to 24 characters) |
| `-port <port>` | `47778` | The game port guests connect to (UDP) |

Open UDP **47777** (discovery) and the game port in any firewall. If phones can't see the server
(some networks block broadcast), they can still **join by IP** with the server's address.

The server exits with a non-zero status if it can't host (for example, the port is taken), so a
service manager or Docker restarts it.

## How it's built

The server is the ordinary Linux player (`BuildScript.BuildLinuxServerFromCommandLine`) run with
`-batchmode -nographics -server` by `server/run-server.sh`, rather than Unity's Dedicated Server
subtarget, which needs an editor module the CI images may lack. `build.yaml` builds it on every PR
and starts it for a few seconds to check it hosts; `release.yaml` packages it and pushes the image
built from `server/Dockerfile`.
