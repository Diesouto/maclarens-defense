# TODOs pendientes en el editor

Lista de tareas que requieren Unity Editor (prefabs, escena, referencias serializadas) y que no
se pueden completar solo con cambios de codigo. Añadir aqui cualquier tarea nueva mientras no haya
acceso al editor; ir tachando/moviendo a "Hecho" segun se completen y validen en Play Mode.

## P5.8 - Cuerpo recuperable y penalizacion

- [x] Añadir `PlayerBody` y `BodyCarrier` al prefab `Assets/_Project/Prefabs/Player.prefab`.
- [x] Asignar `BodyCarrier.carryPoint` a un socket de manos/pecho del rig (con offset razonable
      para que el cuerpo cargado no atraviese al jugador ni la camara).
- [ ] Ajustar `BodyCarrier.followSpeed` en Play Mode (el prefab conserva el valor por defecto 12; si el cuerpo cargado
      vibra o se queda muy atras al girar rapido, tunear aqui).
- [x] Crear un `BodyRecoveryManager` en la jerarquia de managers de `MainScene` y asignar
      `macLarensRespawnPoint` (la instancia de escena apunta a `RespawnPoint`).
- [ ] Confirmar en el Editor que `RespawnPoint` esta dentro de la zona segura de MacLarens.
- [x] Configurar `abandonedBodyQuotaPenalty` en 500.
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
- [x] Añadir el componente a los elementos world-space elegidos: `MacLarensOwner`, tren y punto de
      entrega de loot.
- [ ] Comprobar en Play Mode que `targetCamera` resuelve bien (por defecto usa `Camera.main`; solo
      hace falta asignarlo a mano si hay varias camaras activas a la vez).
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
- [x] `MainScene` y `MenuScene` estan incluidas y habilitadas en `EditorBuildSettings`. Mantener
      `MainScene` como escena de gameplay y `MenuScene` como selector Single/Multiplayer y lobby.
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

- [x] Instalar los paquetes de Netcode for GameObjects, Authentication y Multiplayer/Relay.
- [x] Implementar Host/Join por codigo con Authentication, Relay y `UnityTransport` en
      `RelayJoinCodeManager`.
- [ ] Vincular el proyecto a Unity Services y configurar/verificar el entorno de Relay en Dashboard.
- [x] Asignar `NetworkManager` y `UnityTransport` existentes a `NetworkBootstrapper`; verificar
      que la `NetworkConfig` del `NetworkManager` exista.
- [x] Mantener `NetworkSessionManager` en un GameObject separado con `NetworkObject`.
- [x] Añadir `NetworkObject`, `NetworkTransform` y `NetworkPlayer` a `Player.prefab` y registrar
      el prefab en `DefaultNetworkPrefabs`.
- [x] Asignar `Player.prefab` y spawn transforms en `NetworkPlayerSpawner` de `MainScene`.
- [x] Conectar en `MultiplayerMenuController` los GameObjects de pantalla y controles:
      `mainMenuScreen`, `multiplayerScreen`, `joinScreen`, `lobbyScreen`; botones de Singleplayer,
      Multiplayer, Host, Join, Back, Connect, Previous/Next Character, Ready, Start y Leave;
      campos TMP de nombre/codigo, indice de personaje, estado, codigo de sala, conteo y lista.
- [x] Asignar `Player.prefab` y `PlayerModelPosition` al preview; el selector instancia el prefab
      bajo el ancla y activa solo el modelo `Character_*` seleccionado.
- [ ] Probar que Host abre el lobby y genera/enseña el codigo Relay; Cliente permite introducirlo;
      lobby replica jugadores/Ready y Start del host carga `MainScene` para todos.
- [x] Configurar NGO scene management y registro de `Player.prefab` desde `NetworkBootstrapper`.
- [ ] Verificar en Play Mode que solo el propietario activa input, interaccion y camara.
- [ ] Playtest de lobby en 1/2/3/4 jugadores: entradas, salidas, ready, start y host desconectado.
- [ ] En `MenuScene`, cambiar nombre y personaje desde Multiplayer; comprobar que el perfil aparece
      correctamente en la lista de jugadores y se conserva al iniciar la partida.
- [ ] Probar `Jugar` sin NGO: debe aparecer una instancia local del jugador, su Cinemachine virtual
      camera debe renderizar por el Camera + Brain de `MainScene`, y la vista no debe indicar que no
      hay camaras.
- [ ] Playtest de spawn y movimiento: cada cliente controla solo su personaje y todos ven el
      transform sincronizado, el modelo elegido y el nombre del Canvas `PlayerName` sobre cada jugador.

## P6 - Bloques 2 y 3 de networking

- [x] Añadir `NetworkHealth` y `NetworkInventoryState` al prefab del jugador.
- [ ] Validar ownership y lectura de slots/valor replicados por la UI; probar daño, muerte, ragdoll
      y revive en Play Mode.
- [x] Añadir `NetworkLootItem` a los prefabs de loot existentes.
- [ ] Confirmar `NetworkObject` y registro de prefabs de loot, incluidos objetos creados
      dinamicamente por drops/spawns.
- [x] Añadir `NetworkEconomyState` a `MainScene`.
- [ ] Confirmar referencias de `NetworkEconomyState` a `MoneyManager` y `QuotaManager` y validar
      convergencia de estado en Play Mode.
- [ ] Playtest con 2 y 4 jugadores: pickup simultaneo del mismo loot, inventario lleno, muerte,
      revive y convergencia de dinero/cuota/cargo entre host y clientes.

## P6 - Bloque 4 de networking

- [x] Añadir `NetworkTrainState` y `NetworkCargoState` al prefab `Train_3`.
- [x] Añadir `NetworkLootDelivery` al prefab del punto de entrega.
- [ ] Confirmar `NetworkObject`/`NetworkTransform`, referencias a `TrainSplineFollower`,
      `TrainDeparture` y `RunManager`, y registro de prefabs networkados en `NetworkManager`.
- [ ] Confirmar que `TrainCargo` y el punto de entrega usan colliders/rigidbodies compatibles con
      la autoridad del host y que el parentado visual del cargo se replica correctamente.
- [ ] Playtest con 2 y 4 jugadores: salida simultanea, aceleracion, town exit, entrega sin doble
      cobro, llegada a MacLarens y `Finish Day` validado por el host.

## P6 - Bloque 5 de networking

- [x] Añadir `NetworkEnemyState` y `NetworkHealth` a los prefabs actuales `Bandit`, `Skeleton` y
      `Zombie`.
- [ ] Confirmar `NetworkObject`, `NetworkTransform` y registro en NetworkPrefabs para cada prefab
      de enemigo que pueda spawnear `EnemySpawner`.
- [ ] Añadir `NetworkThreatState` al objeto de managers networkado y comprobar referencias a
      `ThreatManager` y `EnemySpawner`.
- [ ] Confirmar que los enemigos dinamicos se crean solo desde el host y que el NavMesh, capas,
      hitboxes y colliders producen el mismo resultado visual en clientes.
- [ ] Playtest 1/2/3/4 jugadores: threat, densidad, persecucion, ataque, dano, muerte, despawn y
      convergencia de `AliveEnemyCount` sin enemigos fantasma.

## P6 - Bloque 6 de networking

- [x] Añadir `NetworkRunState` y `NetworkEconomyState` a `MainScene`.
- [ ] Añadir `NetworkGameState` al objeto de managers y validar que solo el host cambia
      `GameState`, `FailCause`, dia y fase.
- [ ] Confirmar que la desconexion de un jugador muerto aplica abandono/penalizacion solo una vez.
- [ ] Definir y probar el contrato de un jugador vivo desconectado durante `Run` y el retorno al
      menu cuando se pierde el host.
- [ ] Ejecutar QA final de P6 con 1/2/3/4 jugadores y registrar los resultados antes de marcar la
      conversion multijugador como terminada.

## P6 - Cierre de conversion pendiente

- [x] Añadir `NetworkInventoryAuthority` al prefab del jugador.
- [ ] Validar drop/throw con 2 y 4 jugadores y conectar visualmente los `ItemInstance` replicados.
- [ ] Validar parentado, salida y valor del cargo en red.
- [x] Añadir `NetworkPurchaseAuthority` a los prefabs actuales de `ShopStand`.
- [ ] Validar compras simultaneas.
- [ ] Registrar prefabs de loot dinamico con `NetworkObject`; validar drop, pickup, entrega y
      despawn sin duplicados.
- [ ] Confirmar en Play Mode que `MoneyManager`, `QuotaManager` y `LootRegistry` solo mutan en host.
- [ ] Validar movimiento server-authoritative: velocidad, colisiones, teleport y desync durante
      una run con latencia real.
- [x] Añadir `NetworkWeaponAuthority` al prefab del jugador.
- [ ] Validar disparo, ammo, reload y hitbox; mantener muzzle/hit effects como presentacion local.
- [x] Añadir `NetworkBodyCarrier` al prefab del jugador.
- [ ] Validar carry/drop/throw y recuperacion de cuerpos entre clientes.
- [ ] Verificar el raycast server-authoritative de armas: linea de vision, alcance, ammo y headshot
      deben coincidir entre host y clientes.
- [ ] Verificar que los objetos networkados se despawnean con NGO y que ningun `Destroy` local deja
      fantasmas tras extraccion, muerte, entrega o desconexion.
- [ ] Validar pose y desparentado de cargo en clientes cuando el loot entra y sale del tren.
- [x] Añadir `NetworkBreakable` a algunos prefabs destructibles actuales de loot.
- [ ] Revisar si falta algun prefab destructible y validar que el host produce el reemplazo roto y
      los clientes reciben el despawn.
- [ ] Confirmar que `LootSpawner` y `TownExtractionResolver` existen en un objeto networkado o en
      la escena host-authoritative y que no se ejecutan duplicados en clientes.
