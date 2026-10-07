# TODOs pendientes en el editor

Lista de tareas que requieren Unity Editor (prefabs, escena, referencias serializadas) y que no
se pueden completar solo con cambios de codigo. Añadir aqui cualquier tarea nueva mientras no haya
acceso al editor; ir tachando/moviendo a "Hecho" segun se completen y validen en Play Mode.

## P5.0 - Loop de cuotas cronometradas (2026-10-01)

- [ ] Crear el asset `RunConfig` (Create > MacLarens > Run Config), ajustar `quotaAmounts`,
      `quotaTimeLimits`, `infiniteGrowth`, `timeCarryOverFraction` (0.5) y `defaultQuotasToWin`, y
      asignarlo en `RunManager.runConfig` de `MainScene`. Sin asset se usan los valores por defecto.
- [x] `QuotaTimerUI` ya está en el Canvas del HUD con `timerText` enlazado. El prefab tenía
      `visibilityRoot` apuntando al mismo GameObject del script, lo que lo desactivaba para
      siempre; el código ahora redirige ese caso al objeto del texto. Debe permanecer oculto en
      MacLarens y mostrarse al iniciar el primer viaje a Town.
- [x] `CargoValueUI`: automatico. Esta en el `Canvas` world-space de `Train_3` y apaga ese
      `Canvas` (texto + fondo) fuera de `Town`. `visibilityRoot` solo si se quiere otro objeto.
- [ ] Objeto de Finish Day: ahora es `PayQuotaInteractable` + `QuotaStatusUI` (renombrados
      manteniendo GUID). Revisar que siguen enlazados, renombrar el GameObject y el texto del
      cartel. `QuotaStatusUI` usa un campo nuevo `quotaStatusFormat` (el antiguo se ignora).
- [ ] `MacLarensOwner`: la ocultacion es automatica (usa el `Canvas` hijo del prefab). Solo falta
      actualizar las frases serializadas, que aun hablan de deuda/dias.
- [ ] `GameStateUI`: revisar `successTitle`/`successCauseText` serializados (hablan de deuda) y
      asignar `statsText` si el panel es authored (si no, se crea uno dentro de `panelRoot`).
- [x] `MenuScene`: Singleplayer y Host/Join reutilizan el mismo panel de configuracion y el mismo
      `lobbyQuotasInput`/`lobbyQuotasHintText`. Singleplayer lo abre en modo offline, sin crear
      sala Relay/NGO; en lobby solo el host puede editarlo. No crear un segundo campo.
- [ ] `RunManager` de `MainScene`: los campos antiguos `dayQuotas`/`failOnAnyMissedQuota` y
      `QuotaManager.totalDebt` quedan huerfanos en la escena; se limpian al guardar.
- [ ] Playtest:
  - Comprar pocion y lazo empezando con $100.
  - Canvas del Owner oculto al empezar, visible al hablar, oculto a los 10 s.
  - Lanzar la pocion junto a un jugador herido: cura y no aparece el error de `MeshCollider`.
  - Temporizador oculto hasta la primera salida; no se reinicia en viajes de ida y vuelta.
  - Pagar la cuota: sube la siguiente y el temporizador arranca en la siguiente salida.
  - Agotar el tiempo en el pueblo: `Fail` inmediato, todos mueren, mensaje de tiempo agotado.
  - Ganar con 1 y 3 cuotas; modo infinito (0) no termina.
  - Pantalla final con tiempo, dinero recaudado y kills/muertes/resurrecciones por jugador.
  - Cliente: mismo temporizador, cuota y estadisticas que el host; solo el host edita las cuotas
    del lobby.

## Interaccion, respawn y flavour (2026-10-01)

- [ ] `InteractUI` del Canvas: asignar opcionalmente `keyHintText` (un TMP aparte para `[E]`). Si se
      deja vacio, el hint se antepone como primera linea de `interactText`. Ajustar
      `interactKeyLabel`, `availableColor` y `blockedColor` (gris por defecto).
- [ ] `TrainDeparture` de `Train_3`: tunear `departureHoldDuration` (2 s por defecto). La barra de
      hold y el countdown de salida ahora solo los ve el jugador que acciona la palanca; el resto
      queda bloqueado por `NetworkTrainState.IsCountdownActive`.
- [ ] `PlayerBody` de `Player.prefab`: revisar `respawnGroundMask`, `groundSnapHeight` y
      `respawnGroundOffset` (el revive abandonado ahora siempre teletransporta: punto asignado ->
      pose de spawn inicial -> aviso en consola).
- [ ] `MultiplayerMenuController`: `previewLayer` (-1 = usar las layers del prefab). Si se quiere
      aislar el preview con una camara dedicada, crear la layer y asignarla aqui.
- [ ] `LootDataSO` de consumibles (`PotionSO`, licores): ajustar `useDuration` (3 s por defecto).
- [ ] `PlayerController`: `maximumDrunkStacks` (3 por defecto); las borracheras ahora suman duracion
      e intensidad.
- [ ] `LassoTool` de `Player.prefab`: ajustar `chargeDuration`, `minimumRange` y
      `minimumChargeForceFraction`.
- [ ] `BreakableOnImpact` de las botellas/barriles: ajustar `debrisForce`, `debrisRadius` y
      `debrisTorque` para que los trozos salgan disparados.
- [ ] `MoneyUI`: ajustar `countDuration`, `gainColor`, `lossColor` y `neutralColor`.
- [ ] Playtest: prompt `[E]` en dos lineas, prompt gris con motivo (sin dinero / sin espacio /
      municion llena), hold del tren que se resetea al soltar, beber con hold de 3 s, dos borracheras
      seguidas, lazo a media carga vs carga completa, romper una botella sin errores de consola y
      contador de dinero subiendo en verde / bajando en rojo.

## P5.8 - Cuerpo recuperable y penalizacion

- [x] Añadir `PlayerBody` y `BodyCarrier` al prefab `Assets/_Project/Prefabs/Player.prefab`.
- [x] Asignar `BodyCarrier.carryPoint` a un socket de manos/pecho del rig (con offset razonable
      para que el cuerpo cargado no atraviese al jugador ni la camara).
- [ ] Ajustar `BodyCarrier.followSpeed`, `maxFollowSpeed` y `snapDistance` en Play Mode: el cuerpo
      cargado conserva una pose cinematica, sin simular las articulaciones del ragdoll.
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
      - Morir en Town -> aparece el prompt "Cargar Cuerpo" para otro jugador.
      - Cargar el cuerpo hasta el tren y llegar a MacLarens -> revive sin penalizacion, en el punto
            de respawn de MacLarens.
  - Morir y no ser cargado (se abandona en Town) -> al llegar a MacLarens revive en
            `macLarensRespawnPoint` y se aplica `lostBodyMoneyPenalty` al dinero.
  - Provocar un wipe completo (todos los jugadores muertos) -> `GameState.Fail` inmediato, sin
    esperar al cierre del dia.
  - Cargar un cuerpo y usar el input de soltar (drop) manteniendo pulsado -> cargar el lanzamiento
    y soltarlo para lanzar el cadaver (igual que lanzar un item pesado).
  - Dejar caer un cuerpo (vivo o ya muerto) por un `DeathFloor` -> se marca perdido y revive con
    penalizacion aunque el tren no haya salido de Town todavia.
- [x] Implementado en codigo: `TrainCargo` congela la fisica del cadaver soltado y conserva la
      pose de sus huesos relativa al vagon. No se reparenta el `NetworkObject` del jugador.

## Cadaveres, lazo y abandonados (2026-10-07)

- Owner: el host valida la recogida, el lazo y la muerte de abandonados. Los cuerpos son
  presentacion local; la recogida se replica y la pose de soltado se envia relativa al vagon.
- Estado: muerto libre -> cargado o arrastrado -> fijado en carga. Recoger, ocultar o reanimar
  cancela el arrastre; reanimar libera el anclaje y devuelve el rig al Animator.
- Eventos: `Health.OnDeath` y `OnRevived` restauran/ocultan las capas del modelo local.
  `OnTownExitReached` ejecuta la muerte y la recuperacion en el host.
- [ ] Revisar `InTownTrigger.dieWhenLeftBehind` en escena: activado por defecto; conserva el
      valor del antiguo `killPlayersOnDeparture`. Desactivarlo solo omite la muerte, no cambia
      la limpieza del pueblo ni transporta a los supervivientes de vuelta.
- [ ] Confirmar que el volumen del pueblo incluye tejados y zonas jugables; la comprobacion
      usa la posicion real, no la salida de uno de los colliders del jugador.
- [ ] Host + cliente: soltar cadaveres en cada vagon, arrancar, acelerar y tomar curvas;
      deben conservar la pose relativa, sin atravesar paredes ni perderse bajo el suelo.
- [ ] Recoger otra vez el cuerpo fijado, soltarlo fuera y reanimarlo con pocion durante la
      recogida o el arrastre. No deben quedar anclajes ni movimientos pendientes.
- [ ] Lazo: arrastrar un cadaver desde fuera y dentro del tren, incluyendo un obstaculo;
      el cuerpo se mueve entero con velocidad limitada. No permite robar un cuerpo cargado.
- [ ] Abandonados: dejar un jugador vivo en Town y otro en el tren; al cruzar la salida,
      solo muere el abandonado, suelta inventario y revive con penalizacion al regresar.
      Repetir con el superviviente sobre el techo del tren y con el ajuste desactivado.
- [ ] Cliente muerto: al cargar su cuerpo debe verlo completo desde la camara de muerte;
      al revivir vuelve a la ocultacion de primera persona. Repetir tras varias muertes.
- Validacion pendiente: compilacion de Unity, generacion de RPC por NGO y Play Mode con
  host/cliente. La revision de errores de VS Code no sustituye estas pruebas.

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

## P5.18 - Consumibles y recarga de municion

- [x] `Liquor(S/M/L)SO` como `Consumable` (cura 10/15/20% + camara borracha 8/12/20 s).
- [x] `PotionSO` y prefab `Potion` (con `NetworkLootItem` y `NetworkRigidbody` añadidos en la revision del 2026-10-01).
- [ ] Poner la pocion en `LootSpawnPoint`s o en un `ShopStand` (ahora mismo nada la referencia).
- [ ] Playtest: beber pocion/booze (host y cliente), lanzar y romper pocion junto a otro jugador,
      comprar municion con el arma en mano en su `ShopStand`.

## P7 - Explosivos y botellas rotas

- [x] `NitroExplosion` y `GunpowderBarrelExplosion` creados con VFX.
- [ ] Añadir `sfx` a ambos `ExplosionEffectSO`.
- [ ] Revisar `GunpowderBarrelExplosion.radius` (ahora 20: tumba a medio pueblo; recomendado 6-8).
- [x] Prefab `NitroglicerinBottle` (añadidos `BreakableOnImpact` y `NetworkLootItem` en la revision).
- [x] Prefab `GunpowderBarrel` (añadidos `NetworkLootItem` y `NetworkRigidbody`). Tiene
      `BreakableOnImpact`: explota si se lanza o cae fuerte; quitarlo si no se quiere.
- [ ] Poner nitro y barril en `LootSpawnPoint`s o colocarlos en escena (nada los referencia).
- [x] Botellas de licor con dos mitades (`brokenPrefab` + `brokenPrefabs`). Las piezas se crean ahora
      siempre como debris local y se les quitan los componentes de red al instanciarlas.
- [ ] Opcional: quitar `NetworkObject`/`NetworkTransform`/`NetworkRigidbody` de `BottleBroken*` y
      sacarlos de `DefaultNetworkPrefabs` (ya no hacen falta).
- [ ] Playtest: disparar el barril (host y cliente), lanzar nitro, cadena de barriles, enemigos
      muertos por la explosion, jugador tumbado que se levanta y jugador que muere tumbado.

## P7 - Rodadora, lazo y fantasma

- [x] Prefab `Tumbleweed` (`attackDistance` 1.2, `attackVerticalReach` 1.5), registrado en red.
- [ ] Añadir `Tumbleweed` a `EnemySpawner.enemyPrefabs` en `MainScene`.
- [ ] Darle a la rodadora su propio `ExplosionEffectSO` con `threatAdded` 0 (ahora usa `NitroExplosion`).
- [x] `LassoTool` en la raiz de `Player.prefab`; cuerda y lazo final se crean en runtime con `LineRenderer`.
- [x] `Weapons/Lasso.prefab` como prefab de mundo y mano (`LootItem`, `NetworkLootItem`,
      `NetworkTransform`, `Rigidbody`, `NetworkRigidbody`) y `LassoSO` (`Tool`, precio 75).
- [ ] Poner `LassoSO` en un `ShopStand`/`LootSpawnPoint` y ajustar `heldPositionOffset`/`heldRotationOffset`.
- [ ] Borrar `Loot/Lasso.prefab` (sin uso).
- [ ] Prefab `Ghost`: `NetworkObject`, `NetworkTransform`, `Rigidbody`, `GhostController`
      (`banishItem` = `CrossSO`), collider (se convierte a trigger) y modelo semitransparente.
      Registrar en `DefaultNetworkPrefabs` y asignarlo en `EnemySpawner.ghostPrefab`.
- [ ] Asegurar que la cruz aparece en algun `LootSpawnPoint`.
- [ ] Playtest: lazar loot, un barril y a otro jugador; rodadora explotando; fantasma atravesando
      paredes, ignorando balas y muriendo solo al lanzarle la cruz (host y cliente).

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
- [x] Añadir `OwnerNetworkAnimator` a la raíz de `Player.prefab` y asignar su campo `Animator` al
      Animator del rig (sincroniza `Speed` y los bools de pose desde el owner).
- [ ] Ragdoll de `Player.prefab`: el prefab no tiene Rigidbodies ni Joints en los huesos. Usar el
      Ragdoll Wizard sobre el rig (Hips, Spine, Head, brazos, piernas). `CharacterRagdollController`
      los pone kinematic al arrancar e ignora colisiones con el `CharacterController`.
- [ ] Loot en red: en los 20 prefabs de `Prefabs/Loot/` añadir `NetworkTransform` (autoridad server)
      y `NetworkRigidbody`, y desactivar `AutoObjectParentSync` en su `NetworkObject` (si no, NGO
      revierte el parenting de `TrainCargo` y los clientes no ven caídas ni lanzamientos).
- [ ] Verificar en `MainScene` que el GameObject `NetworkEconomyState` tiene ahora también
      `NetworkThreatState` y `NetworkGameState` (añadidos por YAML).
- [ ] Opcional: quitar el `NetworkTransform` redundante de la raíz de `Train_3.prefab` (la raíz no se
      mueve; los clientes siguen el tren vía `NetworkTrainState.CurrentDistance`) y el que hay en el
      GameObject de `RunManager` en `MainScene`.

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
      `ThreatManager` y `EnemySpawner`. (Añadido por YAML al objeto `NetworkEconomyState` de
      `MainScene`; solo falta verificar en el Editor. `EnemySpawner` se resuelve en runtime.)
- [ ] Confirmar que los enemigos dinamicos se crean solo desde el host y que el NavMesh, capas,
      hitboxes y colliders producen el mismo resultado visual en clientes.
- [ ] Playtest 1/2/3/4 jugadores: threat, densidad, persecucion, ataque, dano, muerte, despawn y
      convergencia de `AliveEnemyCount` sin enemigos fantasma.

## P6 - Bloque 6 de networking

- [x] Añadir `NetworkRunState` y `NetworkEconomyState` a `MainScene`.
- [ ] Añadir `NetworkGameState` al objeto de managers y validar que solo el host cambia
      `GameState`, `FailCause`, dia y fase. (Añadido por YAML al objeto `NetworkEconomyState`;
      busca `GameStateManager.Instance` en runtime. Si no spawnea, los clientes se quedan en `Menu`.)
- [ ] Confirmar que la desconexion de un jugador muerto aplica abandono/penalizacion solo una vez.
- [ ] Definir y probar el contrato de un jugador vivo desconectado durante `Run` y el retorno al
      menu cuando se pierde el host.
- [ ] Ejecutar QA final de P6 con 1/2/3/4 jugadores y registrar los resultados antes de marcar la
      conversion multijugador como terminada.

## P6 - Cierre de conversion pendiente

- [x] Añadir `NetworkInventoryAuthority` al prefab del jugador.
- [ ] Validar drop/throw con 2 y 4 jugadores (los `ItemInstance` replicados ya se reconstruyen en
      clientes via `NetworkInventoryState` + `LootCatalog`).
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
- [ ] Validar pose de cargo en clientes cuando el loot entra y sale del tren (los clientes ya no
      reparentan: `NetworkCargoState` fija la pose relativa al vagon en `LateUpdate`).
- [x] Añadir `NetworkBreakable` a algunos prefabs destructibles actuales de loot.
- [ ] Revisar si falta algun prefab destructible y validar que el host produce el reemplazo roto y
      los clientes reciben el despawn.
- [ ] Confirmar que `LootSpawner` y `TownExtractionResolver` existen en un objeto networkado o en
      la escena host-authoritative y que no se ejecutan duplicados en clientes.
