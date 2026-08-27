- Player ragdolls on death
- Enemy attack stopping distance
- Add Healing LootItems (booze for example) and think about Ammo LootItems (how would they work with multiple weapons? can only be picked up with a weapon in hand and they get added to that weapon? Should the players only restore their weapons ammo when going back to MacLarens instead??)
- Train movement is damped (it starts and ends its movement slowly)
- Train makes a sound (bell hiss) when it begins moving
- Check added scripts from P3 and P4 make sense and are correct
- Certain LootItems look weird when carrying them (vault is way too big and appears too high, so it obstructs player vision), maybe we can separate between ItemHoldPointHeavy and ItemHoldPointLight
- Connect FX[] on the train movement (dust on ground, smoke on smokestack, wind?...)
- Gun is being equipped fine, animations work but won't damage. Raycast not working??
- Train seems to be getting stuck going from town to maclarens


- Train correct behaviour (both the loot and players on the train behave as if their gameobjects were children of the train. when the train is moving: they move with the train and the player camera turns automatically with the train so it remains "static")
I have the following issue:
I need some help building a train for my Unity 6 game. Right now I have a train system that follows a spline which will be on top of actual rail models. This train will be the main form of transportation for the players. However I can't seem to make the players and their dropped loot to stay inside the train properly. I want them to behave exactly as if they were children of the Train gameobject, so they remain in place if they are not moving and can move around the train switching carriages and moving on top of wagons without the camera or movement feeling weird. However as I'm thinking of making the game multiplayer, I'm not sure if reparenting is a nice workaround so I tried to fix this by code but I'm really stuck.

Can you help me fix the train? You can tell me to delete/simplify or add any files

