#!/usr/bin/env bash
# TUF Control v0.5: build/stage before modifying installed application.
# Run without sudo; sudo is only requested if system helpers are not yet installed.
set -euo pipefail
if (( EUID == 0 )); then
  echo 'Ejecuta ./install.sh con tu usuario normal (NO sudo).' >&2
  exit 1
fi
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
DEST="${HOME}/.local/share/tuf-control"
APPROOT="${HOME}/.local/share"
BIN="${HOME}/.local/bin"
DESKTOP="${HOME}/.local/share/applications"
ICON_DIR="${HOME}/.local/share/icons/hicolor/512x512/apps"

command -v dotnet >/dev/null || { echo 'Falta .NET SDK 10: sudo dnf install dotnet-sdk-10.0' >&2; exit 1; }
if ! dotnet --list-sdks | grep -q '^10\.'; then
  echo 'Se necesita .NET SDK 10 para compilar (dotnet --list-sdks).' >&2
  echo 'Instálalo con: sudo dnf install dotnet-sdk-10.0' >&2
  exit 1
fi
command -v pkexec >/dev/null || echo 'Aviso: pkexec no está disponible; los botones protegidos no funcionarán.'
command -v sensors >/dev/null || echo 'Aviso: lm_sensors no está instalado; algunas temperaturas no aparecerán.'
mkdir -p "$APPROOT" "$BIN" "$DESKTOP" "$ICON_DIR"

# Install icon if found
if [[ -f "${ROOT}/tuf-control.png" ]]; then
  cp -f "${ROOT}/tuf-control.png" "${ICON_DIR}/tuf-control.png"
elif [[ -f "${HOME}/Descargas/TUF-Control-Menu/tuf-control.png" ]]; then
  cp -f "${HOME}/Descargas/TUF-Control-Menu/tuf-control.png" "${ICON_DIR}/tuf-control.png"
fi

STAGE="$(mktemp -d "${APPROOT}/.tuf-control-stage.XXXXXXXX")"
trap '[[ -d "$STAGE" ]] && rm -rf -- "$STAGE"' EXIT

echo 'Compilando TUF Control v0.5 (sin modificar la versión instalada)...'
dotnet publish "$ROOT/src/TufControl/TufControl.csproj" -c Release -r linux-x64 --self-contained false -o "$STAGE"
[[ -f "$STAGE/TufControl.dll" ]] || { echo 'Fallo de compilación: versión previa intacta.' >&2; exit 1; }

# Install privilege-restricted boost helper if not already installed
if [[ ! -f /usr/local/libexec/tuf-control-helper || ! -f /usr/local/bin/tuf-control-boost || "${1:-}" == "--system" ]]; then
  echo 'Registrando la persistencia de Turbo Boost (se pedirá sudo)...'
  sudo bash "$ROOT/scripts/install-system.sh"
else
  echo 'Servicios de sistema y helper de Turbo Boost ya instalados. Omitiendo sudo.'
fi

BACKUP="${APPROOT}/tuf-control.backup-$(date +%Y%m%d-%H%M%S)-$$"
if [[ -d "$DEST" ]]; then
  mkdir -p "$BACKUP"
  mv -- "$DEST" "$BACKUP/tuf-control"
fi
if [[ -f "$BIN/tuf-control" || -f "$DESKTOP/tuf-control.desktop" ]]; then
  mkdir -p "$BACKUP"
  [[ ! -f "$BIN/tuf-control" ]] || cp -a -- "$BIN/tuf-control" "$BACKUP/tuf-control-launcher"
  [[ ! -f "$DESKTOP/tuf-control.desktop" ]] || cp -a -- "$DESKTOP/tuf-control.desktop" "$BACKUP/tuf-control.desktop"
fi

if ! mv -- "$STAGE" "$DEST"; then
  echo 'Error moviendo la nueva versión; intentando restaurar la anterior...' >&2
  [[ ! -d "$BACKUP/tuf-control" ]] || mv -- "$BACKUP/tuf-control" "$DEST"
  exit 1
fi

cat > "$BIN/tuf-control" <<'LAUNCH'
#!/usr/bin/env bash
set -euo pipefail
exec dotnet "${HOME}/.local/share/tuf-control/TufControl.dll" "$@"
LAUNCH
chmod 755 "$BIN/tuf-control"

cat > "$DESKTOP/tuf-control.desktop" <<EOF2
[Desktop Entry]
Version=1.0
Type=Application
Name=TUF Control
GenericName=Control ASUS TUF
Comment=Control de rendimiento, energía, pantalla, RGB y batería
Exec=${BIN}/tuf-control
Icon=tuf-control
Terminal=false
Categories=System;Settings;
StartupNotify=true
EOF2
chmod 644 "$DESKTOP/tuf-control.desktop"

if command -v update-desktop-database >/dev/null; then
  update-desktop-database "$DESKTOP" || true
fi
if command -v kbuildsycoca6 >/dev/null 2>&1; then
  kbuildsycoca6 --noincremental >/dev/null 2>&1 || true
fi

printf '\n======================================================\n'
printf '  TUF Control v0.5 instalado con éxito.\n'
printf '  Ejecutable: %s/tuf-control\n' "$BIN"
if [[ -d "$BACKUP" ]]; then printf '  Respaldo anterior en: %s\n' "$BACKUP"; fi
printf '======================================================\n\n'
