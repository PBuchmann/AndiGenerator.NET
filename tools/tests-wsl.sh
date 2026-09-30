#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 Peter Buchmann
# SPDX-License-Identifier: GPL-3.0-only
# Baut die Solution unter WSL (Linux) und fuehrt alle Tests aus. Unter Linux greift Smart App Control nicht.
# Aufruf aus Windows: tests-wsl.cmd (Doppelklick). Protokoll: build-log.txt (wird von Claude gelesen).
set -u
cd "$(dirname "$0")/.."
LOG="$PWD/build-log.txt"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
{
    echo "===== START $(date '+%d.%m.%Y %H:%M:%S') (WSL)"
    echo "----- dotnet --list-sdks"
    dotnet --list-sdks
    echo "----- BUILD"
    dotnet build AndiGenerator.slnx -c Release -nologo -v:minimal
    rc=$?
    echo "----- BUILD Exitcode $rc"
    if [ "$rc" -eq 0 ]; then
        echo "----- TEST"
        dotnet test AndiGenerator.slnx -c Release --no-build -nologo --logger "console;verbosity=normal"
        echo "----- TEST Exitcode $?"
    fi
    echo "===== ENDE $(date '+%d.%m.%Y %H:%M:%S')"
} > "$LOG" 2>&1
