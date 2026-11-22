# TUF Control Linux v0.4 — ASUS TUF F15 FX506HC

Actualización completa **del código fuente** C# / Avalonia (.NET 10) basada en la v0.3 proporcionada por el usuario. Pensada para Nobara KDE con Intel Core i5-11400H y NVIDIA RTX 3050.

## Cambios respecto a v0.3

- Se retiró de la ventana principal la **tarjeta “Modo de GPU”** con los botones **Intel/Integrated** y **Hybrid**. No se ejecuta `supergfxctl -m` desde la aplicación. Se conserva la monitorización de la RTX, temperatura, ventiladores, batería y ASUS Aura.
- En **Fans + Power → Intel Turbo Boost**, las acciones Activar/Desactivar **guardan la elección** en `/etc/tuf-control/boost.state` (solo después de aplicar el cambio). Un servicio de systemd (`tuf-control-boost.service`) la restaura en el próximo arranque.
- **Fresco / Equilibrado / Gaming** y la automatización por cargador modifican **solo el perfil ASUS**, nunca Boost ni GPU. El ajuste de Turbo Boost es independiente. Se conservarán los perfiles personalizados previos y otros ajustes de usuario; los antiguos campos de Boost por preset se ignoran.
- Se conserva el nombre `tuf-control.desktop` y el icono `tuf-control` para que la entrada anclada a KDE use la nueva aplicación sin crear otra entrada.
- El instalador compila primero a una carpeta temporal; si `dotnet publish` falla, no reemplaza la aplicación previa. Antes de sustituirla conserva un respaldo con marca de tiempo.

**No hay ningún cambio automático en GRUB, Secure Boot, modos GPU, drivers NVIDIA ni kernel.** El servicio solo controla la interfaz sysfs del Turbo Boost.

## Requisitos

- Nobara/Fedora x86_64 con KDE, `asusctl` y `asusd` como antes.
- **SDK .NET 10** para compilar (`sudo dnf install dotnet-sdk-10.0`). La primera compilación puede requerir Internet para recuperar los paquetes de Avalonia desde NuGet.
- Polkit con `pkexec` y agente KDE para que los botones privilegiados pidan autorización; `sudo` y `systemd` para instalar la persistencia.
- `lm_sensors` para sensores térmicos (opcional: `sudo dnf install lm_sensors`).

## Instalar sin desinstalar v0.3 primero

1. **Cierra TUF Control v0.3** si está abierto.
2. Descomprime el ZIP en `~/Descargas/TUF-Control-Linux-v0.4/`.
3. En Konsole como tu usuario normal (no uses `sudo` delante de `./install.sh`):

```bash
cd ~/Descargas/TUF-Control-Linux-v0.4
chmod +x install.sh
./install.sh
```

El instalador pedirá `sudo` únicamente al registrar el helper limitado y el servicio de restauración del Boost. **Respeta la preferencia ya guardada** si previamente instalaste el parche parcial v0.4; si no existe, guarda el estado actual sin elegir uno arbitrariamente.

4. Comprueba:

```bash
~/.local/bin/tuf-control
# Muestra estado actual y elección guardada:
tuf-control-boost status
systemctl is-enabled tuf-control-boost.service
```

Al volver a abrir desde KDE, el mismo acceso directo `tuf-control.desktop` ejecutará la nueva DLL. Si el icono en la barra sigue apuntando a una instancia anterior, cierra la app y vuelve a abrirla desde el menú; normalmente no hace falta desanclarlo.

## Usar y comprobar Boost persistente

- Abre TUF Control → **Fans + Power** → **Desactivar Boost** → confirma la autorización de KDE.
- El estado visible debe indicar **DESACTIVADO** y **Guardado: DESACTIVADO**.
- Desde Konsole también puedes comprobarlo sin modificar nada:

```bash
tuf-control-boost status
cat /sys/devices/system/cpu/intel_pstate/no_turbo
# Un valor de 1 en intel_pstate/no_turbo significa Boost desactivado.
```

En equipos cuyo kernel no use `intel_pstate/no_turbo`, el gestor usa `/sys/devices/system/cpu/cpufreq/boost` (0 = desactivado). Para cambiarlo manualmente y persistir:

```bash
sudo tuf-control-boost set off
sudo tuf-control-boost set on
```

Reinicia **solo después de comprobar el funcionamiento** y vuelve a ejecutar `tuf-control-boost status`. Si otro servicio posterior (`tuned`, `power-profiles-daemon`, etc.) cambia Boost, la preferencia guardada seguirá ahí aunque el estado actual sea distinto; revisa servicios de energía si ocurre.

## Respaldo y vuelta atrás

El instalador guarda la versión anterior en una ruta similar a:

`~/.local/share/tuf-control.backup-20261026-214500-12345/`

Contiene `tuf-control/` (versión anterior), `tuf-control-launcher` y `tuf-control.desktop` cuando existían. También puedes conservar el respaldo previo `~/Respaldo-TUF-Control-v0.3/`.

Si deseas volver a la DLL anterior, **cierra TUF Control** y, cambiando `RUTA_DEL_RESPALDO` por la carpeta real que muestre el instalador:

```bash
BACKUP="RUTA_DEL_RESPALDO"
mv ~/.local/share/tuf-control ~/.local/share/tuf-control.v0.4-apartada
cp -a "$BACKUP/tuf-control" ~/.local/share/tuf-control
cp -a "$BACKUP/tuf-control-launcher" ~/.local/bin/tuf-control
cp -a "$BACKUP/tuf-control.desktop" ~/.local/share/applications/tuf-control.desktop
```

**Atención**: esto restaura la interfaz anterior, pero no desinstala el servicio de Boost. Si restauras v0.3, sus presets anteriores pueden volver a modificar Turbo Boost. Para retirar solamente la persistencia (sin restaurar ningún modo de CPU):

```bash
sudo systemctl disable --now tuf-control-boost.service
```

No borres `~/.config/tuf-control/settings.json`: conserva tus preferencias.

## Estructura

- `src/TufControl/` — aplicación Avalonia completa (fuente, no binarios compilados).
- `scripts/tuf-control-helper` — helper limitado para Turbo Boost y batería, invocado por Polkit.
- `scripts/tuf-control-boost` — gestor de Boost y preferencia persistente.
- `systemd/tuf-control-boost.service` — restaura Boost durante el arranque.
- `scripts/install-system.sh` — instala los componentes del sistema con `sudo`.
- `install.sh` — compila e instala la app en el usuario; mantiene el lanzador de KDE.

## Alcance y pruebas

Se comprobaron las referencias entre archivos y los scripts Bash. **No se pudo ejecutar `dotnet publish` ni abrir la GUI en este entorno**, porque no tiene el SDK de .NET 10/Avalonia instalado; el instalador compilará en Nobara. Si falla, comparte el error de compilación (líneas `error CS...`).

Licencia original del proyecto: `GPL-3.0-only` (véase `LICENSE`). Esta app es independiente y no está afiliada a ASUS ni a G-Helper.
