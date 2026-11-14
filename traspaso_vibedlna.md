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
- pendiente: ventana 1.1.0 (usa `Viewing`), Player 0.4.0 (§A y §C en su repo), y prueba en TV fisica.
- pruebas: `VibeDLNA.Tests\AmpliacionDlnaTests.cs`; `dotnet test .\VibeDLNA.Tests\VibeDLNA.Tests.csproj -c Release`.
