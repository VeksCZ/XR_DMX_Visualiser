# Profily světel / Fixture profiles

[Čeština](#čeština) · [English](#english)

---

## Čeština

Každé světlo nebo kus vybavení v DMX Visualiseru je popsané **profilem**, což je jeden JSON soubor. Profil říká, jak světlo vypadá a co znamenají jeho DMX kanály. Nové světlo tak přidáš bez nového buildu aplikace.

### Kde profily jsou
- **Vestavěné** profily jsou součástí aplikace.
- **Vlastní** profily patří do složky `Profiles` vedle `DMXVisualiser.exe`. Soubor se stejným `id` jako vestavěný profil ho nahradí.
- Profily, které sestava používá, se ukládají i do `settings.json` a do exportované sestavy. Proto se dostanou do Questu (Nastavení → Quest → Přenést nastavení) i ke kolegovi.

### Jak přidat nové světlo
1. V *Nastavení → Světla* vyber podobné světlo a klikni na **Uložit profil jako soubor k úpravě**. Profil se uloží do `Profiles` a složka se otevře.
2. Soubor přejmenuj a uprav: hlavně `id`, `name`, kanály a vzhled.
3. Klikni na **Načíst znovu**, pak **+ Přidat** a vyber nové světlo.
4. Chyby v souborech se ukážou pod tlačítky profilů.

### Sdílení sestavy
*Nastavení → Světla → Sestava jako soubor → Exportovat* uloží do složky `Rigs` jeden soubor. Obsahuje světla, adresy, kalibraci hlav, umístění i vlastní profily. Kolega ho dá do své složky `Rigs` a klikne na **Importovat…**. Importovat jde i celý `settings.json`.

### Struktura profilu
```json
{
  "id": "vyrobce-model",            // jednoznačné, malými písmeny
  "name": "Model",
  "manufacturer": "Výrobce",
  "kind": "light",                  // light / hazer / prop
  "placement": "uplight",           // výchozí místo ve scéně (viz níže)
  "notes": "poznámka v nastavení",
  "parts":  [ ... ],                // svítící části
  "shapes": [ ... ],                // pevné tvary (tělo, třmen, stojánek)
  "modes":  [ ... ]                 // DMX režimy
}
```

**Části (`parts`)** – `type` a nepovinné parametry; 0 nebo vynechané = výchozí hodnota:

| type | co to je | parametry |
|---|---|---|
| `par` | wash par | `housing` (round/box), `size`, `box` [š,v,h], `leds` (1 nebo 3), `beam`, `field`, `beamLength`, `intensity` |
| `derby` | derby (vějíř paprsků R/G/B/W) | `coverage`, `beams`, `beam`, `beamLength`, `brightness`, `lensCols`, `lensRows`, `mirror` |
| `mover` | moving head | `beam`, `beamLength`, `intensity`, `brightness`, `panRange`, `tiltRange` |
| `tube` | pixel tuba | `length`, `segments`, `diameter`, `intensity` |
| `laser` | mřížkový laser | `coverage`, `columns`, `rows`, `beamLength`, `brightness` |
| `strobe` | strobo (4 LED) | `intensity` |
| `derbystrobe` | derby + strobo panel (BeamZ) | – |
| `helix` | dvě naklápěcí lišty (BeamZ Helix) | `beam`, `beamLength`, `brightness`, `intensity`, `tiltRange` |

Každá část má také `name`, `pos` [x,y,z] v metrech a `rot` [x,y,z] ve stupních. Světlo svítí po své ose +Z.

**Tvary (`shapes`)**: `shape` (cube / cylinder / sphere), `pos`, `size`, `rot`, `color` ("#rrggbb", bez barvy je černá). U válce je `size` = [průměr, výška, průměr].

**Režimy (`modes`)**: `name`, `channels` (počet kanálů) a `controls`. Každé ovládání má `fn` (funkci), `ch` (kanál od 1) a `part` (index části od 0). Podle funkce přibývá `fine` (jemný kanál), `sub`, `max`, `min`, `ranges`.

| fn | význam |
|---|---|
| `dimmer` | jas části (bez něj svítí naplno) |
| `red` `green` `blue` `white` `amber` `uv` | barevné LED (`sub` = lišta helixu) |
| `strobe` | strobo; bez `ranges` je 0–9 vypnuto a 10–255 1–20 Hz |
| `shutter` | závěrka hlavy: rozsahy `closed`, `open`, `strobe`, `pulse`, `random`, `rampUp`, `rampDown` |
| `pan`, `tilt` | pohyb (16bit s `fine`); `tilt` se `sub` = lišta helixu |
| `ptSpeed` | rychlost motorů (0 = nejrychleji, `invert` otočí) |
| `color` | barevné kolo / barva laseru / kolo derby (rozsahy barev) |
| `gobo` | gobo: `gobo` (a = číslo), `gobos` (a–b, `step` hodnot na gobo), `cycle` (a = počet, b = rychlost) |
| `rotation` | otáčení derby / laseru; `max` °/s; bez `ranges` platí 0 stop, 1–127 CW, 128 stop, 129–255 CCW |
| `ledLevel` | jas jedné LED stroba (`sub` = LED) |
| `program`, `programSpeed` | vestavěné programy: `jump`, `fade`, `pulse`, `auto`, `derbyAuto`, `helixShow`; rychlost `min`–`max` |
| `colorMacro`, `background`, `effect`, `effectSpeed` | barevná makra a efekty pixel tuby |
| `pixels` | přímé pixely: `count` pixelů, `layout` např. "RGBWA" |
| `ptMacro`, `ptMacroSpeed` | pohybová makra (Pocket Pro) |
| `haze` | výkon hazeru (`kind: "hazer"`) |
| `none` | kanál bez vlivu na vizualizaci |

**Rozsahy (`ranges`)**: `from`, `to` (DMX hodnoty), `type` a parametry. Barevné typy jsou:
- `color` (`color`)
- `split` (`color`, `color2`)
- `hue` (`a`–`b`, `sat`)
- `rainbow` (`a` = rychlost)
- `cycle` (`colors`, `a` = kroky za s; bez `colors` se střídají kombinace RGBW)
- `slots` (`colors` rozdělené rovnoměrně přes rozsah)
- `off`

Barva může být "#rrggbb", nebo "#rrggbbww", kde poslední dvojice je bílá LED (derby).

**Místa ve scéně (`placement`)**:

| placement | kde světlo stojí |
|---|---|
| `stand` | stativ za stolem (`mountHeight` = výška) |
| `tableCorner` | rohy zobrazeného stolu |
| `tube` | stojánky vedle stolu |
| `uplight` | podlaha u zadní stěny, svítí na zeď |
| `trussTop`, `trussHang`, `trussCenter` | rampa nad stolem kolegy: nahoře na krajích, visí pod příčkou, uprostřed |
| `table`, `speaker` | stůl, repro |
| `floor` | řada na podlaze |

Každé světlo v sestavě jde navíc umístit ručně: *Nastavení → Světla → Umístění → Vlastní*.

**Vybavení bez DMX** (`"kind": "prop"`) má místo `modes` objekt `prop`:
```json
"prop": { "type": "table", "size": [1.27, 1.155, 0.61], "lycra": "#ebebe6" }
"prop": { "type": "speaker", "size": [0.39, 0.62, 0.35], "standHeight": 1.55, "lycra": "#ebebe6" }
```

### Příklad: jednoduchý RGBW par, 8 kanálů
```json
{
  "id": "muj-par-rgbw",
  "name": "Můj par RGBW",
  "kind": "light",
  "placement": "uplight",
  "parts": [ { "type": "par", "housing": "box", "box": [0.12, 0.12, 0.15], "beam": 25, "field": 40 } ],
  "modes": [ { "name": "8ch", "channels": 8, "controls": [
    { "fn": "dimmer", "ch": 1 }, { "fn": "red", "ch": 2 }, { "fn": "green", "ch": 3 },
    { "fn": "blue", "ch": 4 }, { "fn": "white", "ch": 5 }, { "fn": "strobe", "ch": 6 },
    { "fn": "none", "ch": 7 }, { "fn": "none", "ch": 8 } ] } ]
}
```
Hotové profily najdeš v repozitáři, ve složce `Assets/Visualizer/Resources/Profiles`.

---

## English

Every fixture or piece of equipment in DMX Visualiser is described by a **profile**, which is a single JSON file. The profile says what the fixture looks like and what its DMX channels do. That means you can add a new fixture without a new build of the app.

### Where profiles live
- **Built-in** profiles ship with the app.
- **Custom** profiles go into the `Profiles` folder next to `DMXVisualiser.exe`. A file with the same `id` as a built-in profile replaces it.
- Profiles used by a rig are also stored in `settings.json` and in exported rigs. That is how they reach the Quest (Settings → Quest → Push settings) and other people.

### Adding a new fixture
1. In *Settings → Fixtures*, select a similar fixture and click **Save profile as an editable file**. The profile is written to `Profiles` and the folder opens.
2. Rename the file and edit it: mainly `id`, `name`, the channels and the looks.
3. Click **Reload**, then **+ Add** and pick the new fixture.
4. Errors in profile files show up under the profile buttons.

### Sharing a rig
*Settings → Fixtures → Rig as a file → Export* saves a single file into the `Rigs` folder. It contains the fixtures, addresses, head calibration, placement and custom profiles. The other person puts it into their `Rigs` folder and clicks **Import…**. A whole `settings.json` can be imported too.

### Format
The format is described in the Czech section above. The keys are in English:
- `parts` are the fixture's light-emitting parts:
  - `par`, `derby`, `mover`, `tube`, `laser`, `strobe`, `derbystrobe`, `helix`
- `shapes` are the fixed geometry.
- `modes` list the DMX modes. Each mode holds `controls`, each with:
  - `fn` (function)
  - `ch` (channel, counted from 1)
  - `part` (index of the part, from 0)
  - optional `ranges` (DMX value ranges, each with a `type`)
- `placement` is the default spot in the scene.

The built-in profiles in `Assets/Visualizer/Resources/Profiles` are complete working examples:
- GigBar: multi-part fixture with movers, derbies, pars, laser and strobe
- Pocket Pro: colour wheel, gobos and shutter
- Pixel Tube: macros, effects and pixel mode
- BeamZ DerbyStrobe and Helix: built-in programs
