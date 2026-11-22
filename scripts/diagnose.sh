#!/usr/bin/env bash
set -u
printf '%s\n' '=== Versión .NET ==='
dotnet --version 2>&1 || true
printf '%s\n' '=== ASUS / perfiles ==='
asusctl profile get 2>&1 || true
printf '%s\n' '=== GPU ==='
supergfxctl -g 2>&1 || true
printf '%s\n' '=== GPU runtime ==='
cat /sys/bus/pci/devices/0000:01:00.0/power/runtime_status 2>&1 || true
printf '%s\n' '=== Turbo ==='
cat /sys/devices/system/cpu/intel_pstate/no_turbo 2>&1 || true
printf '%s\n' '=== Helper ==='
ls -l /usr/local/libexec/tuf-control-helper 2>&1 || true
printf '%s\n' '=== Datos de sensores (CPU/SSD/ventiladores) ==='
sensors 2>&1 | grep -E 'Package id|Composite:|cpu_fan:|gpu_fan:' || true
