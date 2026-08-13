/* 

I'd keep one HUD script.

Not a UI manager.

A HUD.

HUD
│
├── HealthUI
├── AmmoUI
├── MoneyUI
├── WaveUI
├── CrosshairUI
└── InteractionUI

Its only responsibility is to find the local player's components when the game starts and wire them together.

For example:

hud.Initialize(localPlayer);

Inside:

Player
│
├── Health
├── Weapon
└── Economy

*/