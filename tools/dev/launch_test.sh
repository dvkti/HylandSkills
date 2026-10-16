#!/usr/bin/env bash
# Launches Schedule I with the Thunderstore profile, then loads save slot N (default 3) from "Continue".
# Usage: tools/dev/launch_test.sh [slot]
SLOT=${1:-3}
P="$APPDATA/Thunderstore Mod Manager/DataFolder/ScheduleI/profiles/Default"
LOG="$P/MelonLoader/Latest.log"
START=$(date +%s)
powershell -NoProfile -Command "Start-Process 'C:\Program Files (x86)\Steam\steam.exe' -ArgumentList @('-applaunch','3164500','--melonloader.basedir','\"$P\"')" >/dev/null
# wait for a fresh log with the menu up
until [ -f "$LOG" ] && [ "$(stat -c %Y "$LOG")" -ge "$START" ] && grep -aq "SM_Component" "$LOG"; do sleep 2; done
sleep 8
um win drive --proc "Schedule I" "focus" "click 88 782" >/dev/null
sleep 3
Y=$((396 + (SLOT - 1) * 76))
um win drive --proc "Schedule I" "click 720 $Y" >/dev/null
echo "loading slot $SLOT"
