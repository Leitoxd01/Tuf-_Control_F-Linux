#!/usr/bin/env bash
# TUF Control v0.4 - privileged installation only (does not touch GPU settings).
set -euo pipefail
if (( EUID != 0 )); then echo 'Se requieren permisos root.' >&2; exit 1; fi
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
command -v systemctl >/dev/null || { echo 'systemd no encontrado.' >&2; exit 1; }

# Preserve existing saved preference (including a previously installed v0.4 patch).
# If it is the first install, copy the *current* kernel setting, without changing it.
if [[ -f /etc/tuf-control/boost.state ]]; then
  saved="$(cat /etc/tuf-control/boost.state)"
  [[ "$saved" == on || "$saved" == off ]] || {
    echo 'La preferencia existente de Turbo Boost no es válida; no se reemplazará.' >&2
    exit 1
  }
else
  if [[ -r /sys/devices/system/cpu/intel_pstate/no_turbo ]]; then
    case "$(cat /sys/devices/system/cpu/intel_pstate/no_turbo)" in
      0) saved=on;; 1) saved=off;; *) echo 'Valor intel_pstate desconocido.' >&2; exit 1;;
    esac
  elif [[ -r /sys/devices/system/cpu/cpufreq/boost ]]; then
    case "$(cat /sys/devices/system/cpu/cpufreq/boost)" in
      1) saved=on;; 0) saved=off;; *) echo 'Valor cpufreq/boost desconocido.' >&2; exit 1;;
    esac
  else
    echo 'Este equipo no presenta una interfaz sysfs compatible con Turbo Boost.' >&2
    exit 1
  fi
fi

install -d -m 755 /usr/local/bin /usr/local/libexec /etc/systemd/system
install -o root -g root -m 755 "$ROOT/scripts/tuf-control-boost" /usr/local/bin/tuf-control-boost
install -o root -g root -m 755 "$ROOT/scripts/tuf-control-helper" /usr/local/libexec/tuf-control-helper
install -o root -g root -m 644 "$ROOT/systemd/tuf-control-boost.service" /etc/systemd/system/tuf-control-boost.service

if [[ ! -e /etc/tuf-control/boost.state ]]; then
  # Writes the state to sysfs and to /etc. This is the same state read above.
  /usr/local/bin/tuf-control-boost set "$saved"
fi
systemctl daemon-reload
systemctl enable tuf-control-boost.service
systemctl restart tuf-control-boost.service
printf '\nServicio de persistencia instalado y activo:\n'
/usr/local/bin/tuf-control-boost status
