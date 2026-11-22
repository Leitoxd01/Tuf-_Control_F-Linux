#!/usr/bin/env bash
# TUF Control v0.4: build/stage before modifying installed application.
# Run without sudo; sudo is only requested for the restricted system helper.
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
command -v dotnet >/dev/null || { echo 'Falta .NET SDK 10: sudo dnf install dotnet-sdk-10.0' >&2; exit 1; }
command -v sudo >/dev/null || { echo 'Falta sudo (necesario para registrar el servicio de arranque).' >&2; exit 1; }
if ! dotnet --list-sdks | grep -q '^10\.'; then
  echo 'Se necesita .NET SDK 10 para compilar (dotnet --list-sdks).' >&2
  exit 1
fi
command -v pkexec >/dev/null || echo 'Aviso: pkexec no está disponible; los botones protegidos no funcionarán.'
command -v sensors >/dev/null || echo 'Aviso: lm_sensors no está instalado; algunas temperaturas no aparecerán.'
mkdir -p "$APPROOT" "$BIN" "$DESKTOP"
STAGE="$(mktemp -d "${APPROOT}/.tuf-control-stage.XXXXXXXX")"
trap '[[ -d "$STAGE" ]] && rm -rf -- "$STAGE"' EXIT

echo 'Compilando TUF Control v0.4 (sin modificar la versión instalada)...'
dotnet publish "$ROOT/src/TufControl/TufControl.csproj" -c Release -r linux-x64 --self-contained false -o "$STAGE"
[[ -f "$STAGE/TufControl.dll" ]] || { echo 'Fallo de compilación: v0.3 intacta.' >&2; exit 1; }

# Install privilege-restricted boost helper and one-shot service first. If this
# fails, the existing application, launcher and KDE desktop entry remain intact.
echo 'Registrando la persistencia de Turbo Boost (se pedirá sudo)...'
sudo bash "$ROOT/scripts/install-system.sh"

# No writes to GPU mode, supergfxctl, kernel parameters, or existing settings.
BACKUP="${APPROOT}/tuf-control.backup-$(date +%Y%m%d-%H%M%S)-$$"
if [[ -d "$DEST" ]]; then
  mkdir -p "$BACKUP"
  mv -- "$DEST" "$BACKUP/tuf-control"
fi
# Save launcher and desktop entry, including older custom icons/Exec values.
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
# Keep the desktop file ID and custom icon name so an existing KDE pin is reused.
cat > "$DESKTOP/tuf-control.desktop" <<EOF2
[Desktop Entry]
Version=1.0
Type=Application
Name=TUF Control
GenericName=Control ASUS TUF
Comment=Control de rendimiento, energía, RGB y batería
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
printf '\nTUF Control v0.4 instalado. Abre desde KDE o ejecuta: %s/tuf-control\n' "$BIN"
if [[ -d "$BACKUP" ]]; then printf 'Respaldo anterior: %s\n' "$BACKUP"; fi
printf 'Verifica persistencia: tuf-control-boost status\n'
