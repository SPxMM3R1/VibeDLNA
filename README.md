# VibeDLNA

App de Windows para compartir carpetas como servidor DLNA/UPnP en la red local.

## Uso

1. Abre `VibeDLNA.exe`. La ventana tiene cinco secciones en el riel izquierdo:
   - **Inicio**: el botón de encendido comparte o detiene el servidor; la dirección para la TV se copia con **Copiar**; **Reescanear** vuelve a revisar las carpetas. Muestra cuántos videos, canciones e imágenes hay, cuántas miniaturas están listas, lo que se está viendo ahora y los últimos movimientos.
   - **Carpetas**: agrega o quita carpetas compartidas, con su estado (lista, escaneando o no disponible), y elige qué se comparte: video, música, imágenes y detección de cambios.
   - **Reproductores**: los equipos que pidieron algo al servidor y lo que cada uno dejó a medio ver.
   - **Actividad**: el registro de la sesión, con filtros por tipo (reproducción, navegación, servidor, errores) y búsqueda. El registro completo queda en `%APPDATA%\VibeDLNA\VibeDLNA.log`.
   - **Opciones**: nombre en la TV, dirección, compartir al abrir la app, inicio con Windows, cerrar a la bandeja, evitar que el PC se suspenda mientras alguien ve algo, y actualizaciones. Cada cambio se guarda al instante.
2. Cambia entre modo claro y oscuro con el botón ☾ / ☀ de la barra de título.
3. Cerrar la ventana la deja en el área de notificación si la opción está activada. Desde la bandeja puedes mostrarla, iniciar o detener el servidor, o salir.

## Compilacion

Cada cambio enviado a `main` se prueba y se compila automaticamente en GitHub Actions sobre Windows. El resultado queda disponible en la ejecucion del workflow como artefacto `VibeDLNA-windows`, con el ejecutable autocontenido y el instalador.

Para compilar manualmente en una maquina Windows con .NET 8 instalado:

Para crear una publicacion autocontenida de un solo archivo y ejecutar las pruebas:

```powershell
.\build-release.ps1
```

El ejecutable queda en `artifacts\publish\VibeDLNA.exe`. Si Inno Setup 6 esta instalado, el mismo comando genera `artifacts\installer\VibeDLNA-Setup-1.0.0.exe`.

Para preparar una release versionada, usa una etiqueta con formato `vMAJOR.MINOR.PATCH`:

```powershell
.\build-release.ps1 -Version "v1.0.1"
```

El comando tambien genera `artifacts\release\VibeDLNA-windows.zip` y su archivo `.sha256`. En GitHub, el workflow publica esos paquetes automaticamente cuando se envia una etiqueta `v...` al repositorio.

La firma Authenticode es opcional y requiere un certificado instalado:

```powershell
.\build-release.ps1 -SigningCertificateThumbprint "HUELLA_DEL_CERTIFICADO"
```

### Modo de desarrollo (solo Debug)

En una compilación Debug, con la variable `VIBEDLNA_DEV=1`, la app usa otro mutex, la carpeta `%APPDATA%\VibeDLNA-dev`, la caché de miniaturas `%LOCALAPPDATA%\VibeDLNA-dev`, el puerto 8211 y el nombre «VibeDLNA DEV». No escribe el inicio con Windows. Así puede correr junto a una instalación real sin compartir su configuración. En Release no existe.

## Notas

- Si una carpeta compartida está en un disco que se monta tarde (USB o red), al abrir con Windows la app espera: revisa cada 5 s y comparte en cuanto aparece. Si solo faltaba una parte, comparte lo disponible y reinicia el servidor cuando llega lo demás. Detener el servidor a mano deja de esperar.
- Maximizada, la ventana ocupa el área de trabajo del monitor y no tapa la barra de tareas.
- La primera vez, Windows puede pedir permiso de firewall. Permite acceso en red privada para que la TV lo encuentre.
- La app anuncia el servidor por DLNA/UPnP y sirve video, musica e imagenes comunes.
- No convierte ni transcodifica archivos: el reproductor debe soportar el formato y codec del archivo original.
- Las miniaturas de los medios compatibles se construyen y conservan en `%LOCALAPPDATA%\VibeDLNA\ThumbnailCache`. El nombre del archivo usa el SHA-256 del contenido completo, por lo que un medio duplicado, renombrado o movido a otra carpeta reutiliza la misma miniatura. Windows las prepara en segundo plano, al iniciar y después de cada reescaneo, mediante su proveedor nativo de miniaturas. El servidor solo anuncia `albumArtURI` cuando el JPG ya está listo; si Windows no puede obtener una imagen, el contenido sigue navegable sin artwork. No existe una ruta HTTP de generación bajo demanda.
- En **Opciones > Actualizaciones**, la app consulta la ultima release publica de `SPxMM3R1/VibeDLNA`. Si encuentra una version superior, descarga el ZIP publicado, comprueba su tamano y SHA-256 cuando GitHub lo proporciona, reemplaza el ejecutable al cerrar y vuelve a abrir VibeDLNA. Si se distribuye una release sin ZIP, tambien puede iniciar el instalador `VibeDLNA-Setup-*.exe`.
- El registro de actividad persistente queda en `%APPDATA%\VibeDLNA\VibeDLNA.log`.
- El icono de la ventana, barra de tareas y bandeja usa la identidad visual oscura de VibeDLNA.
- En Windows 11 compatible, la ventana activa Mica automaticamente. En otras versiones usa el fondo propio de la app.
