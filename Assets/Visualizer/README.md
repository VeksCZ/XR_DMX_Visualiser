# Visualizer – krok 1: scéna a modely světel

## Instalace
1. Unity Hub → nový projekt, šablona **Universal 3D (URP)**, Unity 6.
2. Celou složku `Visualizer` zkopíruj do `Assets/`.
3. URP asset (Project Settings → Graphics → výchozí URP asset → jeho Renderer):
   **Rendering Path = Forward+**. Jinak Unity vykreslí jen pár světel na objekt.
4. Ve scéně: Create Empty → přidej komponenty **SceneBuilder** a **DemoDriver**.
   Do SceneBuilderu přetáhni shadery `Visualizer/Shaders/Beam` a `Emissive`.
5. Bloom (aby čočky a LED zářily): v Hierarchy Volume → Global Volume → Add Override → Bloom (Threshold ~1, Intensity ~1).
6. Play.

## Ovládání
Pravé tlačítko myši = rozhlížení, WASD = pohyb, Q/E = dolů/nahoru, Shift = rychleji.
Haze se mění v SceneBuilderu za běhu.

## Co je kde
- `Fixtures/GigBar.cs` – bar 1100×144×449 mm, pozice efektů na tyči jsou nahoře v Inspectoru (uprav podle reality).
- `Fixtures/MovingHead.cs` – pan 540°, tilt 180°, 17°, gobo, simulace rychlosti motorů.
- `Fixtures/ParLight.cs` – pary Gigbaru (22°/33°) i battery pary (box, úhel odhadnut na 25°).
- `Fixtures/Derby.cs`, `Laser.cs`, `Strobe.cs` – efekty Gigbaru.
- `Fixtures/PixelTube.cs` – tuba 1 m, 8 segmentů RGBWA (40ch). 80ch mód = `tubeSegments = 16`.
- Každé světlo má veřejné hodnoty (dimmer, barva, pan…) – do nich bude v dalším kroku zapisovat Art-Net přijímač. Teď je plní `DemoDriver`.

## Art-Net ze SoundSwitche
1. Na stejný GameObject přidej ještě **ArtNetReceiver** a **DmxPatch**.
2. Play. Unity se v síti ohlásí jako Art-Net zařízení „DJ Visualizer“.
3. V SoundSwitchi zapni Art-Net a „DJ Visualizer“ přiřaď Universe One.
4. Windows při prvním spuštění zeptá na firewall – povol (UDP 6454).
5. Jakmile chodí data, demo se samo vypne. Když přestanou, demo se zase pustí.

Adresy v DmxPatch odpovídají projektu 2502.ssproj:
GigBar Move ILS (EU) 200 (52ch), battery pary 110/120/130/140 (10ch),
tuby 300/350/400/450 (12ch, celá tuba jedna barva).

Pokud Unity běží na stejném PC jako SoundSwitch a data nechodí, může se
o port 6454 přetahovat se SoundSwitchem – pak zkus Unity na jiném PC/Questu.
