# contexto de continuidad de vibedlna para windows

## estado de este documento

- fecha de actualización: 2026-09-11
- este archivo reemplaza a CONTEXTO_VIBEDLNA.md
- nombre del archivo en minúsculas: contexto_vibedlna.md
- alcance: aplicación Windows VibeDLNA, no VibeM3U ni VibeDLNA Player

Este documento resume el estado real conocido del proyecto, sus decisiones de producto, la arquitectura actual, la forma correcta de validar cambios y los pendientes. Debe leerse antes de hacer cambios grandes.

## regla principal de trabajo

VibeDLNA es una aplicación real, no un mockup. No afirmar que una corrección está terminada solo porque el código parece correcto o porque una compilación parcial terminó. Cada cambio debe terminar con:

1. revisión del diff y del estado de Git;
2. pruebas proporcionales al riesgo;
3. verificación del ejecutable o del workflow que realmente se entregará;
4. comprobación visual de la ventana cuando el cambio sea de interfaz;
5. declaración explícita de cualquier parte no probada, especialmente en una TV física o en un PC limpio.

El usuario trabaja en español y prefiere respuestas directas, honestas y con resultados verificables.

## ubicación y estado del repositorio al 2026-09-11

- carpeta de trabajo: D:\Users\SP4MM3R\Documents\Codex\VibeDLNA
- proyecto: D:\Users\SP4MM3R\Documents\Codex\VibeDLNA\VibeDLNA.csproj
- repositorio principal de GitHub: https://github.com/SPxMM3R1/VibeDLNA
- repositorio anterior de GitLab: https://gitlab.com/roberto.ramos.dz/folder-dlna-server.git
- remoto github: repositorio de GitHub
- remoto origin: repositorio anterior de GitLab
- rama actual: main
- rama remota seguida por main: github/main
- commit actual: 567d409 — Build VibeDLNA on GitHub Actions
- último commit visible en el remoto GitLab origin/main: 25f3fec — Send readable media filenames to DLNA clients
- el árbol de trabajo contiene cambios locales no publicados para caché de miniaturas y actualizador, además de este contexto no versionado
- no borrar ni revertir esos cambios sin revisarlos; pertenecen a la solicitud actual

La carpeta mockups/, que contenía 12 PNG y prompts de diseño, fue retirada el 2026-09-11 y enviada a la Papelera de reciclaje. No forma parte del producto ni del código de compilación. No recrearla ni volver a añadirla al repositorio salvo que el usuario lo pida.

No deben usarse las antiguas rutas D:\Users\SP4MM3R\Documents\New project 3\VibeDLNA ni otros proyectos parecidos como sustitutos de este repositorio.

## producto

VibeDLNA es un servidor DLNA/UPnP para Windows que permite seleccionar una o varias carpetas y publicarlas en la red local para televisores y reproductores compatibles.

Funciones del producto:

- compartir videos, audio e imágenes según filtros globales;
- publicar una o varias carpetas;
- ser descubierto por televisores y clientes DLNA mediante SSDP/UPnP;
- iniciar el servidor al abrir la aplicación;
- iniciar la aplicación con Windows;
- abrir minimizada;
- cerrar hacia el área de notificación;
- conservar carpetas configuradas aunque una unidad externa esté desconectada;
- mostrar estado del servidor, dirección de red y actividad;
- reescanear carpetas automáticamente con debounce;
- funcionar en modo claro y oscuro;
- usar Mica en Windows compatible y un fondo propio como fallback;
- construir y servir miniaturas de video mediante una caché persistente por SHA-256 del contenido;
- reutilizar la miniatura cuando el mismo video aparece duplicado, renombrado o movido a otra ruta;
- comprobar actualizaciones desde la última release pública del repositorio de GitHub y preparar la sustitución/reinicio del ejecutable;
- servir los archivos originales sin transcodificar.

La aplicación no es un reproductor de canales IPTV. VibeM3U y VibeDLNA Player son proyectos distintos.

## estado técnico actual

El código actual sigue siendo una aplicación WinForms .NET 8; todavía no existe una migración a WinUI 3.

Propiedades actuales del proyecto:

- OutputType: WinExe
- TargetFramework: net8.0-windows
- UseWindowsForms: true
- versión base de producto: 1.0.0, controlada por `VersionPrefix` para releases versionadas;
- publicación autocontenida de un solo archivo;
- runtime de entrega: win-x64;
- ApplicationIcon: Assets\VibeDLNA.ico;
- manifest con DPI PerMonitorV2;
- ensamblado y producto llamados VibeDLNA.

La idea de pasar a WinUI 3 solo fue exploratoria mediante mockups. Si se retoma, debe tratarse como una migración de interfaz y empaquetado, manteniendo primero el backend DLNA estable.

## archivos importantes

- MainForm.cs: ventana principal, estado, acciones, botones, bandeja y ciclo de vida.
- OptionsDialog.cs: diálogo de opciones y controles visuales personalizados.
- ModernControls.cs: controles ligeros dibujados por la aplicación.
- AppPalette.cs: colores, superficies y fondos.
- NativeTheme.cs: Mica, título nativo y temas de Windows.
- SystemTheme.cs: detección del tema del sistema.
- AppIcon.cs: icono y recursos visuales de la aplicación.
- AppSettings.cs: modelo de configuración.
- SettingsService.cs: lectura, normalización, migración y guardado.
- DlnaContentLibrary.cs: enumeración, filtrado y seguridad de las carpetas compartidas.
- DlnaServer.cs: servidor HTTP, descripción UPnP y streaming con rangos.
- ThumbnailCache.cs: hash SHA-256, caché persistente JPG, generación mediante Windows Shell y fallback visual.
- GitHubUpdateService.cs: consulta de releases, selección de paquete, verificación de tamaño/SHA-256 y actualizador auxiliar.
- SsdpServer.cs: anuncios y descubrimiento SSDP.
- DlnaXml.cs: XML del dispositivo, servicios y DIDL-Lite.
- HttpRequestData.cs: parser de solicitudes y límites HTTP.
- MediaTypes.cs: extensiones, MIME y clases de contenido UPnP.
- NetworkHelper.cs: dirección IPv4 e interfaz de red.
- PowerKeepAwake.cs: prevención opcional de suspensión.
- VibeDLNA.Tests\CoreTests.cs: pruebas unitarias y de integración ligera.
- VibeDLNA.Tests\VibeDLNA.Tests.csproj: proyecto xUnit para .NET 8.
- build-release.ps1: pruebas, publicación y compilación opcional del instalador.
- installer\VibeDLNA.iss: script del instalador Inno Setup.
- .github\workflows\build-windows.yml: compilación automática en GitHub Actions.
- Assets\VibeDLNA.ico: icono de la aplicación.
- app.manifest: manifest de Windows y DPI.

## interfaz y decisiones visuales

La dirección visual actual debe seguir siendo sobria, plana, densa y funcional, con inspiración en herramientas de escritorio reales como qBittorrent, pero sin copiar su aspecto literalmente.

Reglas visuales:

- fondo uniforme y superficies ligeras;
- controles integrados en la paleta de VibeDLNA;
- nada de rectángulos negros nativos detrás de botones, switches, diálogos o menús;
- no usar tarjetas orgánicas, paneles flotantes excesivos ni una interfaz de marketing;
- mantener acciones principales claramente visibles: Iniciar, Detener, Guardar y Opciones;
- el estado del servidor debe entenderse de inmediato;
- el selector claro/oscuro debe ser un icono sencillo de sol/luna y cambiar realmente el tema;
- conservar la estructura y el comportamiento si el usuario pide únicamente ajustar posiciones;
- no introducir CheckBox o Button nativos sin comprobar toda su superficie en modo claro y oscuro;
- verificar la ventana real, no solo el código, cuando se cambien coordenadas o pintado.

Los controles de opciones actuales se dibujan de forma personalizada porque los switches y controles nativos habían producido filas negras y artefactos de pintado.

## opciones funcionales

El diálogo de opciones debe conservar estas funciones:

- carpetas compartidas: agregar, quitar y reescanear;
- nombre visible en la TV;
- mantener el PC despierto mientras DLNA está activo;
- iniciar servidor al abrir;
- iniciar con Windows;
- abrir minimizada;
- cerrar hacia la bandeja;
- compartir videos;
- compartir audio;
- compartir fotos;
- reescanear automáticamente.
- consultar y aplicar actualizaciones desde la sección Actualizaciones; el chequeo comienza al abrir el diálogo y también puede repetirse manualmente.

La barra superior debe mantener Opciones como entrada principal, sin recuperar los menús antiguos Archivo, Editar, Ver, Herramientas y Ayuda, que no tenían acciones útiles.

## persistencia e inicio con Windows

La configuración se guarda en:

- %APPDATA%\VibeDLNA\settings.json
- %APPDATA%\VibeDLNA\VibeDLNA.log

El inicio con Windows usa:

- HKCU\Software\Microsoft\Windows\CurrentVersion\Run

Debe activarse y desactivarse sin permisos de administrador, actualizar la ruta del ejecutable si cambia de ubicación y respetar las opciones de abrir minimizada y cerrar hacia la bandeja.

El guardado debe ser atómico. Si la configuración está corrupta, el servicio debe conservar una copia de respaldo y mostrar un aviso en vez de perder silenciosamente las carpetas.

Las rutas compartidas no deben desaparecer porque una unidad externa esté temporalmente desconectada.

## servidor DLNA y streaming

El servidor actual incluye:

- anuncio y descubrimiento SSDP/UPnP;
- descripción /description.xml;
- servicios de contenido;
- navegación de carpetas y archivos;
- respuestas GET, HEAD y rangos HTTP;
- protección contra traversal mediante object IDs y validación de la raíz;
- coherencia entre interfaz de red seleccionada, HTTP y SSDP;
- filtrado de videos, audio e imágenes por extensión;
- reescaneo automático con debounce;
- límites para cabeceras y cuerpo HTTP;
- registro persistente de inicio, detención, anuncios y errores.

La configuración actual utiliza Port: 0, por lo que Windows asigna un puerto aleatorio en cada inicio. SSDP puede anunciarlo, pero algunas TVs cachean la dirección anterior. Un puerto DLNA fijo es un posible trabajo futuro y no debe implementarse sin probar migración, conflictos de puerto y compatibilidad.

Los nombres con espacios deben seguir escapándose en la URL. Para un archivo como video loco.mp4, la URL debe usar video%20loco.mp4, mientras que DIDL-Lite debe conservar el título humano. El servidor agrega:

- Content-Disposition: inline; filename="video loco.mp4"
- filename*=UTF-8''video%20loco.mp4

No quitar el escape RFC de la URL: una URL con espacios sin escapar puede romper solicitudes HTTP.

El servidor no convierte ni transcodifica videos. La TV o el reproductor cliente debe soportar el contenedor y codec original.

### miniaturas

- La caché vive en `%LOCALAPPDATA%\VibeDLNA\ThumbnailCache`.
- Cada archivo JPG se identifica con el SHA-256 del contenido completo, nunca con la ruta.
- El servidor anuncia `upnp:albumArtURI` en DIDL-Lite y sirve el recurso por `/thumbnail/<sha256>.jpg` con caché HTTP de larga duración.
- El servidor calienta la caché en segundo plano al iniciar y vuelve a intentarlo al reescanear. Si el proveedor de miniaturas de Windows no puede obtener un fotograma para un formato, se guarda una imagen de respaldo para que el cliente no quede sin recurso.
- La caché no tiene todavía una política de limpieza automática: conservar entradas antiguas permite reutilizar una miniatura después de mover o volver a agregar el video.

## compilación y entrega

El usuario prefiere que la compilación de entrega se haga en GitHub, no depender de una publicación local.

Workflow:

- .github\workflows\build-windows.yml
- se ejecuta con push a main, pull request hacia main o ejecución manual;
- también se activa al enviar etiquetas `v*`; en ese caso usa la versión de la etiqueta;
- runner: windows-latest;
- configura .NET 8;
- instala Inno Setup;
- ejecuta build-release.ps1;
- publica el ejecutable autocontenido win-x64;
- genera el instalador si Inno Setup está disponible;
- genera `artifacts/release/VibeDLNA-windows.zip` y `VibeDLNA-windows.zip.sha256`;
- sube el artefacto VibeDLNA-windows.
- para una etiqueta `v*`, un segundo job crea la GitHub Release y adjunta el ZIP, el checksum y el instalador.

El último workflow conocido con éxito corresponde al commit 567d409:

- run de GitHub Actions: 30213677763
- nombre: Build VibeDLNA on GitHub Actions
- resultado: success
- fecha del run: 2026-07-26

El artefacto esperado contiene:

- artifacts/publish/VibeDLNA.exe
- artifacts/installer/VibeDLNA-Setup-<version>.exe
- artifacts/release/VibeDLNA-windows.zip
- artifacts/release/VibeDLNA-windows.zip.sha256

Al 2026-09-11 el repositorio GitHub todavía no tiene releases ni etiquetas publicadas. Por eso la sección Actualizaciones puede mostrar que no hay una release disponible hasta que se publique la primera etiqueta `v...` mediante GitHub Actions.

El script local build-release.ps1 existe para diagnóstico y es el mismo flujo usado por GitHub. No subir bin, obj, publish ni artifacts al repositorio. Esas salidas son regenerables y están ignoradas por .gitignore.

La firma Authenticode es opcional y requiere un certificado instalado. No pedir ni almacenar certificados o secretos en el chat.

## pruebas conocidas

El proyecto de pruebas usa xUnit y cubre, entre otros:

- normalización y migración de configuración;
- conservación de carpetas y al menos un tipo de medio;
- enumeración y filtrado de medios;
- rechazo de object IDs fuera de la carpeta compartida;
- reconocimiento de extensiones comunes;
- escape XML del nombre visible;
- generación de Content-Disposition con nombres legibles y UTF-8;
- respuesta HTTP real de medios con nombre legible;
- reutilización de caché por checksum de contenido y anuncio DIDL de miniaturas;
- análisis de etiquetas de versión del actualizador.

En esta modificación se ejecutaron 13 pruebas locales y pasaron todas. También se ejecutó `.\build-release.ps1 -Version v1.0.1`: generó correctamente el ejecutable autocontenido, `VibeDLNA-Setup-1.0.1.exe`, `VibeDLNA-windows.zip` y su SHA-256. La validación de GitHub Actions todavía queda pendiente porque los cambios no se han enviado al remoto.
No se probó aún la extracción de un fotograma con un video real de cada formato ni la reproducción de la miniatura en una TV física. La generación real depende del proveedor de miniaturas/codecs instalados en Windows; el fallback sí está contemplado por código.

## pendientes y riesgos conocidos

1. Evaluar un puerto DLNA fijo para reducir problemas de caché de algunas TVs.
2. Probar Content-Disposition en más modelos de TV.
3. Revisar visualmente modo oscuro, modo claro, Mica, bandeja y diálogo de opciones.
4. Mantener la compilación de entrega en GitHub Actions.
5. Mejorar la documentación de instalación y diagnóstico de red.
6. Si se decide migrar a WinUI 3, separar el trabajo en shell visual, opciones, bandeja, temas, empaquetado y validación; no reescribir el backend DLNA sin necesidad.
7. Publicar una primera etiqueta `v1.0.1` o superior cuando el usuario autorice la entrega, y verificar el workflow, la Release, los assets y la consulta real del actualizador.
8. Probar la caché y `albumArtURI` contra los modelos de TV/reproductor DLNA de uso real.

## reglas para futuros agentes

- trabajar en D:\Users\SP4MM3R\Documents\Codex\VibeDLNA;
- responder en español;
- revisar git status antes de editar;
- conservar cambios del usuario y archivos no versionados salvo autorización clara;
- no usar la antigua ruta Documents\New project 3\VibeDLNA;
- no eliminar binarios, carpetas o documentos sin validar el objetivo exacto;
- no introducir fondos negros en controles, diálogos o bandeja;
- no usar switches nativos sin comprobar su pintado completo;
- no inventar endpoints ni cambiar el contrato DLNA sin actualizar pruebas;
- no transcodificar sin una decisión explícita;
- preferir GitHub Actions para la compilación de entrega;
- no afirmar que una TV física o un PC limpio fueron probados si no lo fueron;
- hacer cambios pequeños y descriptivos;
- publicar cambios solo después de validar diff, workflow y artefacto;
- mantener VibeDLNA, VibeM3U, VibeDLNA Player y VibeSubtitle como proyectos separados.

## comandos de referencia

Desde D:\Users\SP4MM3R\Documents\Codex\VibeDLNA:

- revisar estado: git status --short --branch
- revisar historial: git log --oneline --decorate -12
- ejecutar pruebas: dotnet test .\VibeDLNA.Tests\VibeDLNA.Tests.csproj -c Release
- ejecutar publicación de diagnóstico: .\build-release.ps1
- listar workflows: gh run list --repo SPxMM3R1/VibeDLNA
- revisar un workflow: gh run view <RUN_ID> --repo SPxMM3R1/VibeDLNA

## estado de cierre de este contexto

La limpieza del 2026-09-11 retiró únicamente los mockups generados que no eran necesarios para compilar. El código, la configuración, las pruebas, los recursos, el instalador, el workflow y el historial Git permanecen intactos; los cambios locales actuales agregan caché de miniaturas y actualización desde GitHub. Este archivo en minúsculas es el contexto vigente y sustituye al archivo de nombre mayúsculo.
