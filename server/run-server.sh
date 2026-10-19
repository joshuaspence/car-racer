#!/bin/sh
# Starts the Car Racer host with no player of its own (docs/server.md), logging to stdout.
# Extra arguments are passed on: -name "Lounge" (what phones list it as) and -port 47778.
cd "$(dirname "$0")" || exit 1
exec ./CarRacer -batchmode -nographics -server -logFile - "$@"
