# TODOs pendientes en el editor

Lista de tareas que requieren Unity Editor (prefabs, escena, referencias serializadas) y que no
se pueden completar solo con cambios de codigo. Añadir aqui cualquier tarea nueva mientras no haya
acceso al editor; ir tachando/moviendo a "Hecho" segun se completen y validen en Play Mode.

## P5.8 - Cuerpo recuperable y penalizacion

- [ ] Añadir `PlayerBody` y `BodyCarrier` al prefab `Assets/_Project/Prefabs/Player.prefab`.
- [ ] Asignar `BodyCarrier.carryPoint` a un socket de manos/pecho del rig (con offset razonable
      para que el cuerpo cargado no atraviese al jugador ni la camara).
- [ ] Ajustar `BodyCarrier.followSpeed` en Play Mode (valor por defecto 12; si el cuerpo cargado
      vibra o se queda muy atras al girar rapido, tunear aqui).
- [ ] Crear un `BodyRecoveryManager` en la jerarquia de managers de `MainScene`.
  - Asignar `macLarensRespawnPoint` (transform en la zona segura de MacLarens).
  - Ajustar `abandonedBodyQuotaPenalty` (valor por defecto 500).
- [ ] Confirmar que `DeathFloor` (y cualquier otra instancia en escena) sigue teniendo su
      `BoxCollider` marcado como Trigger; el script ahora tambien marca cuerpos como perdidos.
- [ ] Revisar los `Rigidbody`/`Joint` (`CharacterJoint`/`ConfigurableJoint`) del rig de ragdoll del
      jugador: `CharacterRagdollController.RootRigidbody` asume que el rigidbody raiz (cadera/pelvis)
      es el unico sin `Joint` propio: confirmar que el rig cumple ese patron o ajustar el codigo.
- [ ] Playtest end-to-end:
  - Morir en Town -> aparece el prompt "Carry Body" para otro jugador.
  - Cargar el cuerpo hasta el tren y llegar a MacLarens -> revive sin penalizacion, en su posicion
    actual (no en el punto de respawn).
  - Morir y no ser cargado (se abandona en Town) -> al llegar a MacLarens revive en
    `macLarensRespawnPoint` y aparece el modificador de cuota "jugador abandonado".
  - Provocar un wipe completo (todos los jugadores muertos) -> `GameState.Fail` inmediato, sin
    esperar al cierre del dia.
  - Cargar un cuerpo y usar el input de soltar (drop) manteniendo pulsado -> cargar el lanzamiento
    y soltarlo para lanzar el cadaver (igual que lanzar un item pesado).
  - Dejar caer un cuerpo (vivo o ya muerto) por un `DeathFloor` -> se marca perdido y revive con
    penalizacion aunque el tren no haya salido de Town todavia.
- [ ] Pendiente de diseño (no implementado, anotar si se quiere para mas adelante): un cuerpo
      soltado (no cargado en brazos) dentro del vagon del tren no se re-parenta como el loot
      (`TrainCargo`), por lo que no viaja solo; de momento el unico camino soportado es cargarlo
      en brazos hasta el arribo a MacLarens.
