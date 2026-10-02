BORROWED TIME - CLEANED ART PACK
All magenta backgrounds removed (transparent PNG). Everything is scaled so 1 tile = 64 px.

UNITY IMPORT (select all sprites -> Inspector -> Apply):
  Texture Type: Sprite (2D and UI)
  Pixels Per Unit: 64        (so one tile = 1 Unity unit)
  Filter Mode: Point (no filter)
  Compression: None
  Pivot: Bottom Center for Player, GymLeader, Creatures, Trees, Buildings, Props
  Tiles: Pivot Center
Backgrounds/BattleBackground.png is 960x540 (use PPU 100 or fit it to the camera).
UI pieces: in the Sprite Editor set a 9-slice border so they stretch cleanly.

FOLDERS
  Environment/Tiles      16 ground tiles (64x64)
  Environment/Trees      8 trees and bushes
  Environment/Buildings  House_Small, House_Large, Gym, Shop, Temple
  Environment/Props      12 props (mailbox, sign, fence, rock, barrel, crate, lamp, pot, water tower, bench, stairs, portal)
  Player/                Player_<down|left|right|up>_<0-3>  (64x80, 4 walk frames each)
  GymLeader/             same layout
  Creatures/             12 creatures + _Evolved forms + LastEmber_Front/Back
  UI/                    DialogBox, MenuPanel, StatusBar_Diamond, StatusBar_Round
  Backgrounds/           BattleBackground

CREATURE NAMES (reading order of your sheets, placeholders - rename freely):
  Emberfox, Magmound, Wickling, Finlet, Axoril, Cascadon, Leafox, Bloomhog, Boulderon, Geodin, Zappaw, Staticle

NOTE: AI tiles are not perfectly seamless. If you see faint seams in the map, enable
a tiny sprite Extrude/Padding in the Sprite Atlas, or use Tile Palette with 'Gap' 0.
