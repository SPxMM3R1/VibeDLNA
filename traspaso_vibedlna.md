# traspaso de vibedlna
fecha 2026-11-14
servidor DLNA Windows
WinForms y .NET 8
base 590da53 (v1.0.9); ampliacion 1.1.0 del servidor en commits locales, sin push, tag ni release
origin GitHub release v1.0.9
Player Android y VibeSubtitle separados
VibeM3U fuera: avisar y no cambiar
cache SHA warmup Shell albumArt
HTTP SSDP updater CI checksum

## ampliacion 1.1.0 (lado servidor, contrato §7)
- A: `ShellPropertyReader` lee `System.Video.*` y `System.Audio.*` en el calentamiento, junto a la duracion; el dato va en la entrada de `index.json` (campo `Technical`) y se mide una vez por contenido. `DlnaXml` emite `res@resolution`, `res@bitrate` (bytes/s), `res@nrAudioChannels` y `dc:description`.
- B: la raiz `0` lleva `dc:description` «N carpetas · M videos»; los videos se cuentan en segundo plano (`DlnaContentLibrary.StartCountingVideos`).
- C: `ClientIdentity` interpreta el User-Agent `VibeDLNA-Player/...`, VLC, Kodi, o «Reproductor en <ip>».
- D: `ViewingActivity` (API interna, expuesta como `DlnaServer.Viewing`): viendo ahora (15 s), `seguir-viendo.json` (7 dias, 50, 95 %) y `reproductores.json` (30 dias) en `%APPDATA%\VibeDLNA`, y el registro de actividad con las frases de la spec 03.
- decisiones: Windows no informa la profundidad de bits, asi que no se emite «10 bit»; `System.Video.Compression` y `System.Audio.Format` llegan como subtipos GUID y se traducen; el fps se mide pero no se envia.
- pendiente: Player 0.4.0 (§A y §C en su repo) y prueba en TV fisica. La ventana 1.1.0 esta hecha (ver abajo).
- pruebas: `VibeDLNA.Tests\AmpliacionDlnaTests.cs`; `dotnet test .\VibeDLNA.Tests\VibeDLNA.Tests.csproj -c Release`.

## ventana 1.1.0 (diseño v1, `03-vibedlna-windows-1.1.0.md`)

- Ventana: `MainForm` sin barra nativa. Barra propia (logo, nombre, versión, tema, minimizar, maximizar, cerrar) y bordes de redimensión por `WM_NCHITTEST` y `WM_NCCALCSIZE`. El estado de maximizada se pregunta con `IsZoomed`, porque `WindowState` va atrasado durante la restauración. Mica sigue como antes.
- Piezas: `Ui.cs` (tokens sobre `AppPalette`, fuentes, iconos del mockup), `UiControls.cs` y `UiLists.cs` (controles pintados y listas), `UiLayout.cs` (páginas, tarjetas, riel), `UiPages.cs` (Inicio, Carpetas, Reproductores, Actividad, Opciones), `AppModel.cs` (`AppView`, `IAppActions`).
- Eliminados: `OptionsDialog.cs` y `ModernControls.cs`. Sus opciones están en la sección Opciones.
- Servidor (consume la ampliación §A–§D, sin reimplementar): `LibraryTracker` cuenta videos, canciones e imágenes por carpeta y sigue las miniaturas listas sin abrir archivos; `DlnaServer.Overview` lo expone. `ViewingActivity.WatchingNow` trae `ClientKey` y `ClientDisplayName`, y hay una frase de rango fuera del archivo.
- Suspensión: `keepAwake` salió del servidor. `SetThreadExecutionState` es por hilo, así que la ventana lo llama desde la UI y solo mientras haya algo en «Viendo ahora».
- Registro: `ActivityLog.Classify` decide el tipo por la frase (Reproduce, Navega, Servidor, Error) y `ActivityLog.Apply` filtra por tipo y texto. Se guardan 500 líneas en memoria.
- Modo de desarrollo: `DevMode` (solo Debug + `VIBEDLNA_DEV=1`). Mutex `VibeDLNA.SingleInstance.Dev`, datos en `%APPDATA%\VibeDLNA-dev`, miniaturas en `%LOCALAPPDATA%\VibeDLNA-dev`, puerto 8211, nombre «VibeDLNA DEV», sin tocar el inicio con Windows.
- Decisiones no previstas en la spec:
  - «Red privada «<nombre>»» no se muestra: no hay API sencilla para el nombre de la red. Se muestra «red local».
  - «Escaneando X %» es miniaturas listas / total de la carpeta mientras el calentamiento corre. Al terminar, la carpeta dice «Lista» aunque queden archivos sin miniatura.
  - «Nombre en la TV» se guarda al salir del campo o al pulsar Enter, no a cada tecla, porque cambiarlo reinicia el servidor.
  - La ruta de «Seguir viendo» se corta por el medio (elipsis de ruta) para que se lea el reproductor.
  - Un clic en «Reescanear» o cambiar un tipo de archivo reinicia el servidor cuando está activo (los tipos y las vigilancias se fijan al iniciar).
- Pruebas: `VibeDLNA.Tests\VentanaTests.cs` (formatos, filas de «Viendo ahora», clasificación y filtros del registro, conteo de biblioteca). `dotnet test` en Release: 100 superadas. Las dos advertencias `xUnit1031` vienen de `AmpliacionDlnaTests.cs` (commit anterior).
- Probado en modo dev (puerto 8211, copia de prueba de tres videos, reproductores simulados con `curl`): inicio y detención con el botón, quitar carpeta, maximizar y restaurar, tema claro y oscuro en las cinco secciones.
- Pendiente y conocido:
  - HTTP: un rango que empieza fuera del archivo se sirve como 200 con el archivo completo (`TryParseRange` lo recorta), no como 416. Por eso la frase «pidió un rango fuera del archivo» no aparece aún. Cambiarlo es decisión aparte.
  - «Agregar carpeta» abre el diálogo nativo; no se automatizó la prueba. Se probó quitar carpeta y la configuración de desarrollo.
  - Contrato §6 y §8 y el lado Player (0.4.0) siguen pendientes; no se tocó el contrato en esta entrega.
  - Prueba en TV física: pendiente.
