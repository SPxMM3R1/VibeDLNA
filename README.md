# VibeDLNA

App de Windows para compartir carpetas como servidor DLNA/UPnP en la red local.

## Uso

1. Abre `VibeDLNA.exe`.
2. Selecciona la carpeta que quieres compartir.
3. Ajusta el nombre que vera la TV o reproductor.
4. Marca las opciones que quieras:
   - Iniciar el servidor al abrir la app.
   - Iniciar con Windows.
   - Abrir minimizada cuando inicia con Windows.
   - Quedar en el area de notificacion al cerrar la ventana.
5. Cambia entre modo claro y oscuro con el boton de sol/luna.
6. Guarda opciones y pulsa **Iniciar**.

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

## Notas

- La primera vez, Windows puede pedir permiso de firewall. Permite acceso en red privada para que la TV lo encuentre.
- La app anuncia el servidor por DLNA/UPnP y sirve video, musica e imagenes comunes.
- No convierte ni transcodifica archivos: el reproductor debe soportar el formato y codec del archivo original.
- Las miniaturas de los medios compatibles se construyen y conservan en `%LOCALAPPDATA%\VibeDLNA\ThumbnailCache`. El nombre del archivo usa el SHA-256 del contenido completo, por lo que un medio duplicado, renombrado o movido a otra carpeta reutiliza la misma miniatura. Windows las prepara en segundo plano, al iniciar y después de cada reescaneo, mediante su proveedor nativo de miniaturas. El servidor solo anuncia `albumArtURI` cuando el JPG ya está listo; si Windows no puede obtener una imagen, el contenido sigue navegable sin artwork. No existe una ruta HTTP de generación bajo demanda.
- En **Opciones > Actualizaciones**, la app consulta la ultima release publica de `SPxMM3R1/VibeDLNA`. Si encuentra una version superior, descarga el ZIP publicado, comprueba su tamano y SHA-256 cuando GitHub lo proporciona, reemplaza el ejecutable al cerrar y vuelve a abrir VibeDLNA. Si se distribuye una release sin ZIP, tambien puede iniciar el instalador `VibeDLNA-Setup-*.exe`.
- El registro de actividad persistente queda en `%APPDATA%\VibeDLNA\VibeDLNA.log`.
- El icono de la ventana, barra de tareas y bandeja usa la identidad visual oscura de VibeDLNA.
- En Windows 11 compatible, la ventana activa Mica automaticamente. En otras versiones usa el fondo propio de la app.
