- Add Healing LootItems (booze for example) and think about Ammo LootItems (how would they work with multiple weapons? can only be picked up with a weapon in hand and they get added to that weapon? Should the players only restore their weapons ammo when going back to MacLarens instead??)
- Certain LootItems look weird when carrying them (vault is way too big and appears too high, so it obstructs player vision), maybe we can separate between ItemHoldPointHeavy and ItemHoldPointLight
- Añadir un objeto lasso (permite agarrar objetos/jugadores)

- Train correct behaviour: the PlayerController and PlayerMotor are messing up the transforms from the train when the Player gets parented. If I disable the PlayerController the player will stay correctly on the train and the camera will stay in place. I want to be able to have the player move around the train, get on and off, drop and pick items, etc. while the train is moving. How can we fix this?
- Connect FX[] (dust on ground, smoke on smokestack, wind?...) and sounds (bell hiss when it begins moving and rail sounds) on the train movement 
- Train movement is damped (it starts and ends its movement slowly)
- Check added scripts from P3 and P4 make sense and are correct
- Player camera is "attached to head", so when the player ragdolls the camera has the POV from the player model
- Add a death camera - 3rd person rotating camera targeting the player corpse or alive players, can change the current objective (previous - next) with some keys
- Final gameplay loop (debt, Story/Infinity modes, fog, body recovery) is now formalized in `Roadmap_MVP.md` > "Vision extendida del loop" and `MVP_Backlog.md` > P8; not part of the current MVP freeze.

