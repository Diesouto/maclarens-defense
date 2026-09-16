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

## P5.16 - UI del mundo apuntando al jugador

- Script listo: `Assets/_Project/UI/WorldSpaceBillboard.cs`. Rota el transform hacia la camara en
  `LateUpdate` (mismo orden que usa `PlayerController` para su propia camara).
- [ ] Añadir el componente a los Canvas world-space que se quieran (prompts sobre NPCs, dialogo de
      `MacLarensOwner`, nameplates, etc.) y comprobar que `targetCamera` resuelve bien (por defecto
      usa `Camera.main`; solo hace falta asignarlo a mano si hay varias camaras activas a la vez).
- [ ] Ajustar `lockYAxisOnly` por elemento: activado (por defecto) mantiene el elemento vertical;
      desactivarlo si se quiere un billboard que tambien incline segun la altura de la camara.

## P5.17 - Separar partes del cuerpo visibles por la camara propia

- Script listo: `Assets/_Project/Scripts/Player/PlayerBodyVisibility.cs`. Usa la layer
  `CameraHidden` que ya existe reservada en `ProjectSettings/TagManager.asset` (no hacia falta
  crearla) y excluye esa layer del `cullingMask` de la camara del propio jugador; las camaras de
  otros jugadores/espectador no se tocan, así que ellos siguen viendo el cuerpo completo.
- [ ] **Importante**: esto depende de que el modelo del jugador tenga renderers separados por
      parte del cuerpo (cabeza/torso/brazos/piernas vs manos/pies). Si el modelo es un unico
      `SkinnedMeshRenderer` fusionado (un solo mesh para todo el cuerpo), este enfoque por layer no
      puede ocultar solo una region del mismo renderer: haria falta separar el mesh en el rig
      (tipico en packs modulares tipo Synty POLYGON) o usar un viewmodel de manos/pies dedicado.
      Revisar el rig de `Player.prefab` primero para confirmar que aplica.
- [ ] Si el modelo si esta separado por partes: añadir `PlayerBodyVisibility` a `Player.prefab`,
      asignar `playerCamera` y rellenar `hiddenFromOwnCameraRoots` con los renderers de
      cabeza/torso/brazos/piernas (dejando fuera manos y pies).
- [ ] Playtest: la camara del jugador solo debe mostrar sus propias manos y pies; mirando a otro
      jugador (o en una futura camara de espectador) el cuerpo debe verse completo.

## P5.9 - Team wipe y cuota fallida / P5.10 - Deuda pagada

- Codigo listo: `GameStateManager` ahora tiene `FailCause` (`TeamWipe`/`QuotaFailed`), `SetFail(cause)`,
  `RestartRun()` (recarga la escena activa) y `ReturnToMainMenu()` (carga `mainMenuSceneName`,
  por defecto `"MenuScene"`). `RunManager` tiene un nuevo toggle `failOnAnyMissedQuota`: en `false`
  (por defecto) solo falla en el ultimo dia sin cuota pagada; en `true` falla inmediatamente el
  primer dia que no se alcance, para poder probar cual es mas divertido sin tocar codigo.
- [ ] **Importante**: `Assets/_Project/Scenes/MainScene.unity` y `MenuScene.unity` no estan en
      `Build Settings` (solo aparece `SampleScene`). `SceneManager.LoadScene` falla si la escena no
      esta en la lista, incluso en el Editor. Añadir ambas escenas en
      `File > Build Profiles > Scene List` (o el menu equivalente) antes de probar Restart/Main Menu.
- [ ] Crear el panel de `GameStateUI` (`Assets/_Project/UI/GameStateUI.cs`) en el Canvas principal:
      un `panelRoot` desactivado por defecto, texto de titulo, texto de causa y dos botones
      (Restart -> `RestartRun()`, Main Menu -> `ReturnToMainMenu()`, ya conectados via `onClick` en
      el propio script). Es deliberadamente minimo (sin estilo) para no adelantar trabajo de
      `P5.13`; ese ticket puede reemplazar/mejorar visualmente este panel sin tocar la logica.
- [ ] Decidir y fijar `failOnAnyMissedQuota` tras probar ambas variantes (`P5.14` de balance).
- [ ] Playtest:
  - Provocar un team wipe -> `Fail` inmediato con causa "team wipe" visible.
  - Fallar la cuota en el ultimo dia (con `failOnAnyMissedQuota=false`) -> `Fail` con causa "quota".
  - Fallar la cuota en un dia intermedio con `failOnAnyMissedQuota=true` -> `Fail` inmediato.
  - Pagar toda la deuda -> `Success`; comprobar que `ShopStand`/`FinishDayInteractable`/
    `TrainDeparture` dejan de aceptar interaccion (ya gatean por `IsRunActive`, deberia funcionar
    solo con el cambio de estado).
  - Pulsar Restart -> la escena se recarga limpia (sin loot/enemigos/threat/dinero residual).
  - Pulsar Main Menu -> carga `MenuScene` y sale del flujo de run.


## P6 - Bloques 0 y 1 de networking

- [ ] Instalar y configurar Unity Services Authentication + Relay; anadir los paquetes necesarios
      al proyecto y crear `RelayJoinCodeManager` con Host/Join por codigo.
- [ ] Crear o localizar el objeto de escena con `NetworkManager`, `NetworkBootstrapper` y
      `NetworkSessionManager`.
- [ ] Crear el prefab networkado del jugador con `NetworkObject`, `NetworkTransform` y
      `NetworkPlayer`; registrarlo en `NetworkManager.NetworkPrefabs`.
- [ ] Crear un objeto con `NetworkPlayerSpawner`, asignar el prefab networkado y los puntos de
      spawn, y comprobar que solo el propietario activa input, interaccion y camara.
- [ ] Crear la UI de lobby para Host, Join, ready, lista de jugadores y Start solo para host.
- [ ] Playtest de lobby en 1/2/3/4 jugadores: entradas, salidas, ready, start y host desconectado.
- [ ] Playtest de spawn y movimiento: cada cliente controla solo su personaje y todos ven el
      transform sincronizado.

## P6 - Bloques 2 y 3 de networking

- [ ] Añadir `NetworkHealth` al prefab networkado del jugador y comprobar que `NetworkObject` y
      ownership estan configurados; validar daño, muerte, ragdoll y revive en Play Mode.
- [ ] Añadir `NetworkInventoryState` al prefab del jugador y comprobar que la UI puede leer los
      slots y el valor replicado desde el host.
- [ ] Añadir `NetworkLootItem` y `NetworkObject` a los prefabs de loot que deban existir en red;
      registrar tambien los objetos creados dinamicamente por drops/spawns.
- [ ] Añadir `NetworkEconomyState` al objeto de managers networkado y comprobar referencias a
      `MoneyManager` y `QuotaManager` en la escena.
- [ ] Playtest con 2 y 4 jugadores: pickup simultaneo del mismo loot, inventario lleno, muerte,
      revive y convergencia de dinero/cuota/cargo entre host y clientes.

## P6 - Bloque 4 de networking

- [ ] Añadir `NetworkObject`, `NetworkTransform` y `NetworkTrainState` al objeto networkado del
      tren; asignar `TrainSplineFollower`, `TrainDeparture` y `RunManager`.
- [ ] Configurar `NetworkObject` y `NetworkLootDelivery` en el punto de entrega, y registrar los
      prefabs de loot networkados en `NetworkManager.NetworkPrefabs`.
- [ ] Confirmar que `TrainCargo` y el punto de entrega usan colliders/rigidbodies compatibles con
      la autoridad del host y que el parentado visual del cargo se replica correctamente.
- [ ] Playtest con 2 y 4 jugadores: salida simultanea, aceleracion, town exit, entrega sin doble
      cobro, llegada a MacLarens y `Finish Day` validado por el host.

## P6 - Bloque 5 de networking

- [ ] Añadir `NetworkObject`, `NetworkTransform`, `NetworkEnemyState` y `NetworkHealth` a cada
      prefab de enemigo que pueda spawnear `EnemySpawner`; registrarlos en NetworkPrefabs.
- [ ] Añadir `NetworkThreatState` al objeto de managers networkado y comprobar referencias a
      `ThreatManager` y `EnemySpawner`.
- [ ] Confirmar que los enemigos dinamicos se crean solo desde el host y que el NavMesh, capas,
      hitboxes y colliders producen el mismo resultado visual en clientes.
- [ ] Playtest 1/2/3/4 jugadores: threat, densidad, persecucion, ataque, dano, muerte, despawn y
      convergencia de `AliveEnemyCount` sin enemigos fantasma.

## P6 - Bloque 6 de networking

- [ ] Añadir `NetworkObject`, `NetworkGameState` y `NetworkRunState` al objeto de managers; validar
      que solo el host cambia `GameState`, `FailCause`, dia y fase.
- [ ] Configurar `BodyRecoveryManager` en escena y confirmar que la desconexion de un jugador
      muerto aplica abandono/penalizacion solo una vez.
- [ ] Definir y probar el contrato de un jugador vivo desconectado durante `Run` y el retorno al
      menu cuando se pierde el host.
- [ ] Ejecutar QA final de P6 con 1/2/3/4 jugadores y registrar los resultados antes de marcar la
      conversion multijugador como terminada.

## P6 - Cierre de conversion pendiente

- [ ] Añadir `NetworkInventoryAuthority` al prefab del jugador y validar drop/throw con 2 y 4
      jugadores; conectar visualmente los `ItemInstance` replicados.
- [ ] Añadir `NetworkCargoState` al tren y validar parentado, salida y valor de cargo.
- [ ] Añadir `NetworkPurchaseAuthority` a cada `ShopStand` networkado y validar compras simultaneas.
- [ ] Registrar prefabs de loot dinamico con `NetworkObject`; validar drop, pickup, entrega y
      despawn sin duplicados.
- [ ] Confirmar en Play Mode que `MoneyManager`, `QuotaManager` y `LootRegistry` solo mutan en host.
- [ ] Instalar Authentication + Relay y crear Host/Join por codigo.
- [ ] Validar movimiento server-authoritative: velocidad, colisiones, teleport y desync durante
      una run con latencia real.
- [ ] Añadir `NetworkWeaponAuthority` al prefab del jugador y validar disparo, ammo, reload y
      hitbox; mantener muzzle/hit effects como presentacion local.
- [ ] Añadir `NetworkBodyCarrier` al prefab del jugador y validar carry/drop/throw y recuperacion
      de cuerpos entre clientes.
- [ ] Verificar el raycast server-authoritative de armas: linea de vision, alcance, ammo y headshot
      deben coincidir entre host y clientes.
- [ ] Verificar que los objetos networkados se despawnean con NGO y que ningun `Destroy` local deja
      fantasmas tras extraccion, muerte, entrega o desconexion.
- [ ] Validar pose y desparentado de cargo en clientes cuando el loot entra y sale del tren.
- [ ] Añadir `NetworkBreakable` a los prefabs destruibles que formen parte del gameplay y validar
      que el host produce el reemplazo roto y los clientes reciben el despawn.
- [ ] Confirmar que `LootSpawner` y `TownExtractionResolver` existen en un objeto networkado o en
      la escena host-authoritative y que no se ejecutan duplicados en clientes.
