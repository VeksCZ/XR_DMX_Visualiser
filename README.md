# DMX Visualiser

[Čeština](#čeština) · [English](#english)

---

## Čeština

3D vizualizér DMX světel pro **SoundSwitch**. Přijímá Art-Net přímo ze SoundSwitche a v reálném čase ukazuje, co dělají světla – bez nutnosti mít rig zapojený. Hodí se na přípravu show doma, ladění scén a kontrolu patche.

### Funkce
- **Art-Net přímo ze SoundSwitche** – aplikace se v SoundSwitchi objeví jako Art-Net zařízení *DJ Visualizer*.
- **Windows aplikace** – virtuální sál s DJ stolem, několik pohledů kamery, demo režim bez SoundSwitche, mlha z hazeru.
- **Seznam světel** – adresy, režimy, zobrazení/skrytí jednotlivých světel, detail kanálů.
- **Kalibrace pohyblivých hlav** – pan/tilt offset, invertování, jedním klikem „současná pozice = střed parketu“ (SS Stage Center).
- **Předvolby sestav** – „Moje“ a „Kolega“ jdou načíst jedním tlačítkem.
- **Profily světel v JSON** – nové světlo přidáš souborem do složky `Profiles`, bez nového buildu. Popis formátu: [Docs/PROFILES.md](Docs/PROFILES.md).
- **Sestava jako soubor** – export / import celé sestavy (světla, adresy, kalibrace, umístění, vlastní profily) pro kolegu nebo jiný počítač.
- **Libovolný počet světel** – přidávání, odebírání a řazení v seznamu, vlastní umístění každého světla ve scéně.
- **Automatické aktualizace** – Windows verze si sama stáhne nové vydání z GitHubu.
- **Meta Quest 3** – nativní VR aplikace: menu v brýlích, ovladače, pohyb pákami a teleport, volitelně passthrough a naskenovaná místnost s umístěním DJ stolu.

### Podporovaná světla
| Světlo | Režim |
|---|---|
| Chauvet GigBar Move ILS (EU) | 52ch |
| ADJ Pocket Pro | 13ch |
| Battery Par (uplight) | 10ch |
| Pixel Tube 360 | 12ch / 40ch (8 px) |
| Hurricane Haze 1DX / 4D | 1ch / 2ch |
| Light4Me Black Par 30x3W | 9ch |
| BeamZ DerbyStrobe | 6ch |
| BeamZ MHL820 Double Helix | 18ch |

Rozložení kanálů odpovídá profilům v knihovně SoundSwitche. Další světla jde přidat vlastním profilem (viz [Docs/PROFILES.md](Docs/PROFILES.md)).

### Instalace
**Windows:** stáhni `DMXVisualiser-vX.Y.Z-win64.zip` z [Releases](../../releases), rozbal a spusť `DMXVisualiser.exe`. Aplikace je portable – nastavení (`settings.json`), vlastní profily (`Profiles`) a sestavy (`Rigs`) jsou v její složce; složku přenášej celou, samotné exe nefunguje. PC musí být ve stejné síti jako SoundSwitch (nebo na stejném PC).

**Quest 3:** zapni na Questu vývojářský režim a nainstaluj APK z [Releases](../../releases):
```
adb install -r -g DMXVisualiser-vX.Y.Z-quest3.apk
```
Quest musí být na stejné Wi-Fi jako počítač se SoundSwitchem. Menu otevřeš levým tlačítkem ≡.

### Sestavení ze zdrojáků
Unity 6000.3.9f1 (URP). Build přes menu *Tools → Visualizer* nebo z příkazové řádky:
```
Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod BuildVisualizer.Build
Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod BuildQuest.BuildAndInstall
```

---

## English

A 3D DMX lighting visualiser for **SoundSwitch**. It receives Art-Net straight from SoundSwitch and shows in real time what your lights are doing – no rig needed. Useful for preparing shows at home, tuning scenes and checking your patch.

### Features
- **Art-Net directly from SoundSwitch** – the app shows up in SoundSwitch as the Art-Net device *DJ Visualizer*.
- **Windows app** – virtual hall with a DJ table, several camera views, demo mode without SoundSwitch, haze from the hazer.
- **Fixture list** – addresses, modes, show/hide per fixture, channel detail.
- **Moving head calibration** – pan/tilt offset, invert, one click "current position = dance floor centre" (SS Stage Center).
- **Rig presets** – "Mine" and "Colleague" load with a single button.
- **JSON fixture profiles** – add a new fixture by dropping a file into the `Profiles` folder, no rebuild needed. Format: [Docs/PROFILES.md](Docs/PROFILES.md).
- **Rig as a file** – export / import the whole rig (fixtures, addresses, calibration, placement, custom profiles) for a colleague or another PC.
- **Any number of fixtures** – add, remove and reorder fixtures, custom placement of each fixture in the scene.
- **Automatic updates** – the Windows version downloads new releases from GitHub by itself.
- **Meta Quest 3** – native VR app: in-headset menu, controllers, stick locomotion and teleport, optional passthrough and scanned room with DJ table placement.

### Supported fixtures
| Fixture | Mode |
|---|---|
| Chauvet GigBar Move ILS (EU) | 52ch |
| ADJ Pocket Pro | 13ch |
| Battery Par (uplight) | 10ch |
| Pixel Tube 360 | 12ch / 40ch (8 px) |
| Hurricane Haze 1DX / 4D | 1ch / 2ch |
| Light4Me Black Par 30x3W | 9ch |
| BeamZ DerbyStrobe | 6ch |
| BeamZ MHL820 Double Helix | 18ch |

Channel layouts follow the profiles in the SoundSwitch fixture library. More fixtures can be added with a custom profile (see [Docs/PROFILES.md](Docs/PROFILES.md)).

### Installation
**Windows:** download `DMXVisualiser-vX.Y.Z-win64.zip` from [Releases](../../releases), unzip and run `DMXVisualiser.exe`. The app is portable – settings (`settings.json`), custom profiles (`Profiles`) and rigs (`Rigs`) live in its folder; move the whole folder, the exe alone does not run. The PC must be on the same network as SoundSwitch (or the same PC).

**Quest 3:** enable developer mode on the Quest and install the APK from [Releases](../../releases):
```
adb install -r -g DMXVisualiser-vX.Y.Z-quest3.apk
```
The Quest must be on the same Wi-Fi as the SoundSwitch computer. Open the menu with the left ≡ button.

### Building from source
Unity 6000.3.9f1 (URP). Build via *Tools → Visualizer* or from the command line:
```
Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod BuildVisualizer.Build
Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod BuildQuest.BuildAndInstall
```
