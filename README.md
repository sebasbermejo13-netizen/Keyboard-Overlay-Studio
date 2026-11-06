# Keyboard Overlay Studio

Programa para Windows que te deja **diseñar el teclado que se ve en tu stream** (OBS) y, si quieres, **verlo encima de tus juegos sin OBS**.

- Editor visual: teclas, tipografía, forma 3D, colores, ángulo, efectos al pulsar e imágenes.
- Plantillas de juegos (Fortnite, Valorant, CS2, LoL…) con iconos de construir.
- Estilos listos para usar y la opción de guardar, exportar e importar los tuyos.
- Visor en pantalla: el teclado flotando encima del juego, sin OBS.

---

## 1. Requisitos

| Qué | Para qué | ¿Obligatorio? |
|---|---|---|
| Windows 10 u 11 (64 bits) | Ejecutar el programa | Sí |
| .NET Framework 4.7.2 o superior | Ya viene instalado en Windows 10/11 | Sí (normalmente ya lo tienes) |
| [OBS Studio](https://obsproject.com/) | Mostrar el teclado en el stream | Solo para OBS |

> **No hace falta input-overlay ni ningún plugin de terceros.** El programa trae su propio mini plugin para OBS (se instala con un botón).
> El **Visor en pantalla** ni siquiera necesita OBS.

---

## 2. Descargar e instalar

1. En GitHub pulsa **Code → Download ZIP** (o descarga el `.zip` de la sección **Releases**).
2. **Antes de descomprimir**: clic derecho en el `.zip` → **Propiedades** → marca **Desbloquear** → **Aceptar**.
   (Si no lo haces, Windows puede bloquear el programa por venir de internet.)
3. Descomprime la carpeta donde quieras, por ejemplo `Documentos\Keyboard Overlay Studio`.
   - No la pongas en `Archivos de programa`: el programa guarda tu diseño en su propia carpeta y ahí Windows no le deja escribir.
4. Abre **`Keyboard Overlay Studio.exe`**.
   - Si sale *"Windows protegió su PC"*: **Más información → Ejecutar de todas formas**. Pasa con cualquier programa que no esté firmado.

No hay instalador: la carpeta es el programa. Para desinstalarlo, borra la carpeta.

---

## 3. Primer uso

1. Elige una plantilla en **Plantillas** o empieza desde **Teclas**.
2. Dale tu estilo en **Letra, Forma, Colores, Ángulo, Efectos e Imagen** (o elige uno en **Estilos**).
3. Pulsa tus teclas: la vista previa las ilumina para que lo pruebes.
4. Pulsa **Guardar** (abajo a la izquierda) o **Ctrl+S**. Se escribe `config.js` en la carpeta.

Atajos útiles en la vista previa:

| Acción | Cómo |
|---|---|
| Mover teclas | Arrastrar |
| Cambiar tamaño | Tirar de los bordes |
| Elegir varias | Arrastrar un recuadro en vacío / Mayús + clic |
| Imán | Ctrl al arrastrar |
| Mover en línea recta / mantener forma | Mayús al arrastrar |
| Estirar desde el centro | Alt |
| Acercar / alejar | Rueda del ratón (doble clic para volver) |
| Deshacer / rehacer | Ctrl+Z / Ctrl+Y |
| Copiar / pegar / duplicar | Ctrl+C / Ctrl+V / Ctrl+D |

---

## 4. Ponerlo en OBS (solo la primera vez)

1. En OBS: **Fuentes → + → Navegador**.
2. Marca **Archivo local** y elige **`overlay.html`** de la carpeta del programa
   (en la sección **OBS** del programa hay un botón **Copiar ruta de overlay.html**).
3. Pon el **ancho y alto** que indica la sección **OBS** del programa (botón **Ajustar el lienzo al teclado** si no cabe).
4. Acepta. Desde ese momento, **cada vez que pulses Guardar, OBS se actualiza solo** en unos 2 segundos.
5. **Instala el mini plugin de teclas** (recomendado): en el programa, sección **OBS → Teclas en OBS → Instalar plugin en OBS** (con OBS cerrado). A partir de ahí **OBS ilumina las teclas él solo, con el programa cerrado**.
   - Se instala en `C:\ProgramData\obs-studio\plugins\kos-teclas\` (la ubicación oficial de OBS para plugins). Si Windows pide permiso de administrador, acéptalo.
   - Para quitarlo: mismo sitio → **Quitar de OBS**.
   - **Sin el plugin** también funciona, pero dejando el programa abierto mientras haces stream (al cerrar el editor se queda en la bandeja).

> Truco: añade `?tilt=left`, `?tilt=right`, `?tilt=center`, `?tilt=side-left`, `?tilt=side-right` o `?tilt=flat` al final de la ruta para tener dos fuentes con ángulos distintos y el mismo estilo.

Si no usas el plugin, abajo en el programa verás **"OBS conectado (sin plugin)"** cuando OBS esté recibiendo tus teclas desde el programa.

> ¿Ya usabas **input-overlay**? Puedes desinstalarlo. Si lo dejas, no pasa nada: el que arranque primero atiende a OBS y el otro espera.

---

## 5. Visor en pantalla (sin OBS)

Menú **Extra → Visor en pantalla**:

- **Ctrl+Alt+K**: mostrar / ocultar el teclado encima de todo.
- **Ctrl+Alt+L**: bloquear (los clics lo atraviesan) / desbloquear (para arrastrarlo).
- **Desbloqueado (Ctrl+Alt+L)**: arrástralo para moverlo y tira de las esquinas o los lados para cambiar el tamaño, como en el editor.
- Opacidad, fluidez de animaciones y opción **Animaciones** (desactívala si notas bajada de FPS).
- **Seguir en la bandeja**: al cerrar el editor, el visor sigue funcionando junto al reloj de Windows.
- **Abrir con Windows**: al encender el PC arranca en la bandeja (sin abrir el editor).

Funciona con juegos en **ventana** o **pantalla completa sin bordes**. En pantalla completa *exclusiva* Windows no deja dibujar nada encima.

> Para jugar, cierra el editor (queda en la bandeja): el visor gasta muchísimo menos que la vista previa del editor.

---

## 6. Compartir plantillas y estilos

- **Plantillas → Exportar plantilla…** guarda la distribución de tus teclas en un `.json`. Quien lo reciba usa **Importar plantilla…**.
- **Estilos → Exportar estilo…** guarda el diseño completo (colores, letra, efectos, teclas…).

---

## 7. Actualizar a una versión nueva

Tus cosas están en estos archivos de la carpeta. **Cópialos a la carpeta nueva** antes de borrar la vieja:

| Archivo | Qué es |
|---|---|
| `config.js` | Tu diseño guardado (el que usa OBS) |
| `mis-estilos.json` | Tus estilos guardados |
| `mis-plantillas.json` | Tus plantillas guardadas |
| `studio-prefs.json` | Preferencias del programa y del visor |

Después, en OBS, si cambiaste de carpeta, vuelve a elegir `overlay.html` en la fuente de navegador.

---

## 8. Estructura de la carpeta

```
Keyboard Overlay Studio.exe   el programa
overlay.html                  la página que usa OBS
kb-render.js, kb-keycodes.js  los usa overlay.html
config.js                     tu diseño (se crea al pulsar Guardar)
fonts\                        tipografías que el programa descarga solas de Google Fonts al usarlas
obs-plugin\                   mini plugin de OBS (kos-teclas.dll) y su código en C
src\                          código fuente del programa (C# WinForms)
```

---

## 9. Compilar desde el código

No hace falta para usarlo. Si cambias el código o Windows bloquea el `.exe` incluido:

1. Abre la carpeta `src`.
2. Doble clic en **`Compilar.bat`**. Usa el compilador que ya trae Windows (.NET Framework), no hay que instalar nada.
3. Se crea `Keyboard Overlay Studio.exe` en la carpeta principal.

---

## 10. Problemas frecuentes

| Problema | Solución |
|---|---|
| Windows no deja abrir el `.exe` | Desbloquea el `.zip` antes de descomprimir (paso 2.2) o **Más información → Ejecutar de todas formas**. |
| OBS no ilumina las teclas | Instala el plugin (sección **OBS** del programa) y reinicia OBS. Sin plugin, el programa tiene que estar abierto (o en la bandeja). |
| OBS no cambia al guardar | Clic derecho en la fuente → **Actualizar la caché de la página actual** (solo la primera vez). |
| El teclado sale cortado en OBS | Sección **OBS → Ajustar el lienzo al teclado** y pon ese tamaño en la fuente. |
| No guarda / "no se pudo guardar" | La carpeta está en un sitio protegido (Archivos de programa). Muévela a Documentos. |
| El visor no se ve encima del juego | Pon el juego en **pantalla completa sin bordes** o **ventana**. |
| El programa se cierra con un error | Mira `error.txt` en la carpeta y compártelo al reportar el fallo. |

---

Hecho por **SBB**.
