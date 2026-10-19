# The Car Racer host on a Linux server (docs/server.md). Built by release.yml from the Linux player
# in build/StandaloneLinux64, with this directory's files copied in beside it.
#
#   docker run -d --network host --restart unless-stopped \
#     -v car-racer:/home/carracer/.config ghcr.io/joshuaspence/car-racer -name "Lounge"
#
# Host networking is needed: phones find the server by UDP broadcast and multicast on the LAN,
# which don't cross Docker's bridge network.
FROM ubuntu:22.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home carracer

COPY --chown=carracer:carracer . /opt/car-racer/
RUN chmod +x /opt/car-racer/CarRacer /opt/car-racer/run-server.sh

USER carracer
WORKDIR /opt/car-racer

# Discovery announcements go out from 47777; guests connect to the game port.
EXPOSE 47777/udp 47778/udp

# Settings and the saved cup live in PlayerPrefs under ~/.config, so mount a volume there to keep them.
ENTRYPOINT ["/opt/car-racer/run-server.sh"]
