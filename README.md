# TUF Control Linux v0.5 — ASUS TUF F15 FX506HC

Actualización completa C# / Avalonia (.NET 10) pensada para Nobara KDE en ASUS TUF Gaming F15 (Intel Core i5-11400H + NVIDIA RTX 3050).

## Novedades en v0.5

1. **🎨 Nuevo selector fácil de color para el teclado (Aura RGB):**
   - Paleta de 12 colores directos predefinidos (Rojo TUF, Cian ASUS, Verde Neón, Ámbar, Azul Cielo, Púrpura, etc.) con previsualización en un solo clic.
   - Controles deslizantes manuales R, G, B (0-255) con sincronización bidireccional y vista previa en tiempo real.
   - Soporte directo para efectos estático, respiración, pulso y ciclo.

2. **📌 Integración con la bandeja del sistema (System Tray de KDE):**
   - Icono en la barra de tareas con Tooltip informativo en tiempo real (temperaturas CPU/GPU, perfil y batería).
   - Menú de clic derecho rápido: cambiar perfiles (*Silencioso*, *Equilibrado*, *Turbo*), activar/desactivar *Turbo Boost*, cambiar pantalla entre *60 Hz* y *144 Hz*, y salir.
   - Minimización a la bandeja al cerrar la ventana principal (configurable).

3. **🔋 Presets rápidos de límite de batería:**
   - Botón directo **60% Cuidado**: ideal para uso continuo conectado a la corriente (máxima vida útil de la batería).
   - Botón directo **80% Diario**: balance recomendado entre autonomía y salud de batería.
   - Botón directo **100% Total**: para viajes o salidas.

4. **🖥️ Control de tasa de refresco (60 Hz vs 144 Hz):**
   - Botones manuales en la pantalla principal para cambiar la tasa de refresco al instante mediante `kscreen-doctor`.
   - Opción en configuración para **cambio automático de Hz** (60 Hz al desconectar el cargador para maximizar la autonomía; 144 Hz al conectarlo para máxima fluidez).

5. **🌡️ Indicadores de temperatura por código de colores:**
   - Verde fresco (< 55 °C)
   - Cian / Azul templado (55 - 74 °C)
   - Amarillo / Naranja alerta (75 - 84 °C)
   - Rojo carga pesada (≥ 85 °C)
   - Indicador de GPU suspendida en gris para confirmar el ahorro de energía en la RTX 3050.

---

## Compilación e Instalación

### 1. Instalar el SDK de .NET 10 (solo una vez)
En tu terminal Konsole ejecuta:
```bash
sudo dnf install -y dotnet-sdk-10.0
```

### 2. Compilar e instalar la v0.5
En tu terminal:
```bash
cd ~/Proyectos/tuf-control
chmod +x install.sh
./install.sh
```

El script compilará la aplicación sin alterar tu sistema y actualizará el ejecutable en `~/.local/bin/tuf-control` manteniendo un respaldo automático de la versión previa.
