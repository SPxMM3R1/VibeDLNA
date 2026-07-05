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

## Ejecutable

El ejecutable publicado queda en:

`bin\Release\net8.0-windows\win-x64\publish\VibeDLNA.exe`

## Notas

- La primera vez, Windows puede pedir permiso de firewall. Permite acceso en red privada para que la TV lo encuentre.
- La app anuncia el servidor por DLNA/UPnP y sirve video, musica e imagenes comunes.
- No convierte ni transcodifica archivos: el reproductor debe soportar el formato y codec del archivo original.
- El icono de la ventana, barra de tareas y bandeja usa la identidad visual oscura de VibeDLNA.
- En Windows 11 compatible, la ventana activa Mica automaticamente. En otras versiones usa el fondo propio de la app.
