# MacLarens Defense - MVP Backlog

## Leyenda de estado

- <span style="color:#2f81f7">✅ Texto azul</span>: tarea implementada y validada en Play Mode (aceptada).
- Texto normal: tarea pendiente, parcial o sin validar todavia.

## Como usar este backlog

Cada item debe convertirse en una tarjeta independiente y cerrarse solo cuando tenga validacion jugable, no cuando el script compila.

Formato recomendado de tarjeta:

```text
[Sistema] Nombre corto
Resultado esperado
Criterio de aceptacion
```

## Orden estricto de implementacion

No se debe saltar de bloque salvo que el bloque anterior ya tenga validacion jugable.

1. Cerrar interaccion base.
2. Cerrar inventario, pickup y drop.
3. Cerrar deposito, quota y decision de escapar.
4. Cerrar pueblo, puntos de loot y repetibilidad simple.
5. Cerrar threat, enemigo base y extraccion.
6. Cerrar Story Mode, failure states, day loop y build singleplayer estable.
7. Migrar a online host-authoritative con Relay.
8. Añadir hazards, segundo enemigo y contenido extra solo despues de lo anterior.

## P0 - Ahora mismo

### Arquitectura e interaccion

- `[Content] Auditar POLYGON Western Pack para props MVP`
  Resultado: lista cerrada de objetos que serviran como loot, decorado y tren.
  Aceptacion: existe una shortlist de 5-8 props de loot y 1 composicion inicial de pueblo sin necesidad de buscar mas packs.
  Estado: cerrada. Ver [Auditoria de contenido western](Auditorias/Auditoria_Contenido_Western.md).

- `[Animation] Bloquear set Mixamo de placeholder`
  Resultado: locomotion, hit, death y recovery definidos para no cambiar animator cada semana.
  Aceptacion: existe un set minimo elegido para jugador y un set base para enemigo humanoide.
  Estado: cerrada como decision de contenido. Clips importados y familias base, armada y carry decididas; `Getting Up` corregido a no-loop. La validacion de avatares, root motion y conexion de capas se difiere hasta que P1/P4 necesiten esos estados. Ver [Set de animaciones placeholder](Set_Animaciones_Placeholder.md).

- `[Interaction] Crear IInteractable`
  Resultado: un contrato comun para loot, tren, puertas y extraccion.
  Aceptacion: un objeto de prueba puede ser interactuado sin tocar `PlayerController`.
  Estado: implementado en `Assets/_Project/Scripts/Interaction/IInteractable.cs`; prueba reproducible documentada en [Prueba de interaccion base](Auditorias/Prueba_Interaccion_Base.md).

- `[Player] Crear PlayerInteractor`
  Resultado: raycast de interaccion, deteccion de target y prompt generico.
  Aceptacion: el jugador ve prompt, pulsa interact y se ejecuta la accion correcta.
  Estado: implementado en `Assets/_Project/Scripts/Player/PlayerInteractor.cs`; playtest documentado en [Prueba de interaccion base](Auditorias/Prueba_Interaccion_Base.md).

- `[UI] Conectar prompt de interaccion`
  Resultado: feedback visual minimo para interacciones.
  Aceptacion: el prompt cambia al mirar objetos interactuables y desaparece al salir.
  Estado: implementado en `Assets/_Project/UI/InteractUI.cs`; playtest documentado en [Prueba de interaccion base](Auditorias/Prueba_Interaccion_Base.md).

- `[Structure] Reorganizar carpetas por dominio`
  Resultado: base de scripts preparada para crecer.
  Aceptacion: los scripts nuevos de loot, train e interaction ya viven en su dominio.
  Estado: cerrada. Existen `Core`, `Interaction`, `Player`, `Loot`, `Train`, `Enemy`, `World`, `UI` y `Networking` como destinos de dominio.

## P1 - Loot prototype

### Datos y pickup

- `[Loot] Crear LootDataSO`
  Resultado: datos authoring para nombre, valor, icono y prefab.
  Aceptacion: existen al menos 5 assets de loot con valores distintos.
  Estado: implementado en `Assets/_Project/ScriptableObjects/Loot/LootDataSO.cs`; existen 6 assets con valores distintos (`LiquorSO` 25, `CopperBar` 50, `SilverBar` 100, `RevolverSO` 150, `GoldBarSO` 200 y `VaultSO` 500).

- `[Loot] Crear LootItem interactuable`
  Resultado: representacion en mundo que usa `LootDataSO`.
  Aceptacion: al interactuar intenta entrar en inventario y desaparece del mundo si entra.
  Nota de diseño: los objetos pesados, como la caja fuerte, iniciaran transporte visible en mano y bloquearan sprint y arma; ver [Arquitectura MVP](Arquitectura_MVP.md).
  Estado: implementado en `Assets/_Project/Scripts/Loot/LootItem.cs`, con reactivacion del mismo objeto del mundo al soltar o lanzar y soporte de `BreakableOnImpact`.

### Inventario y drop

- `[Inventory] Crear PlayerInventory de 4 slots`
  Resultado: add, remove, has space, total value.
  Aceptacion: no permite sobrellenar, informa valor correcto y expone evento de cambio.
  Estado: implementado en `Assets/_Project/Scripts/Player/PlayerInventory.cs`, con `ItemInstance` por slot, quinto item en mano cuando la mochila esta llena, municion persistente por instancia y drop/throw al mundo.

- `[Inventory] Implementar drop al mundo`
  Resultado: el jugador puede soltar loot y recuperarlo despues.
  Aceptacion: el item vuelve a existir en escena con su dato correcto y sin duplicarse.
  Estado: implementado. El drop reactiva el `LootItem` real asociado a la `ItemInstance`; el throw usa carga, crosshair y fisicas sobre ese mismo objeto.

- `[UI] Crear InventoryUI minima`
  Resultado: slots y valor transportado visibles.
  Aceptacion: el HUD se refresca en pickup y drop sin intervencion manual.
  Estado: implementado como HUD minimo por iconos y highlight de slot activo en `Assets/_Project/UI/InventoryUI.cs`. El valor transportado sigue calculandose en `PlayerInventory`, pero no se muestra ahora mismo por la decision actual de UX.

- `[Scene] Crear Sandbox_Loot`
  Resultado: espacio de prueba para pickup, drop y edge cases.
  Aceptacion: permite validar todo el milestone 1 en menos de 2 minutos.
  Estado: existe el area de prueba `Assets/_Project/Scenes/Test/InventoryTestScene.unity`, usada para iterar pickup, inventario, throw y UI.

## P2 - Train jugable, quota y economia

### Economia (implementado)

- `[Train] Crear TrainCargo`
  Resultado: punto de deposito fisico conectado a la quota.
  Aceptacion: transfiere valor desde el jugador a quota sin contar doble.
  Estado: implementado en `Assets/_Project/Scripts/Train/TrainCargo.cs`. Trigger generico (`Collider`); `LootItem` fisicos en escena se reparentan al propio transform del vagon al entrar y se desparentan al salir (regresion detectada y corregida: el refactor de "single source of truth" habia eliminado el reparentado).

- `[Core] Crear QuotaManager`
  Resultado: quota actual, cargo actual y evento de cambio.
  Aceptacion: puede responder si la cuota ya fue alcanzada.
  Estado: implementado en `Assets/_Project/Scripts/Core/QuotaManager.cs`.

- `[Core] Crear RunManager`
  Resultado: dia actual, resultado de run y reset de flujo.
  Aceptacion: Day 1, Day 2 y Day 3 se configuran sin hardcode disperso.
  Estado: implementado en `Assets/_Project/Scripts/Core/RunManager.cs`.

- `[Train] Crear accion Return to MacLarens`
  Resultado: la salida puede activarse en cualquier momento con countdown de 5s.
  Aceptacion: al completarse llama `RunManager.AdvanceDay()` y dispara `OnTrainDeparted`.
  Estado: implementado en `Assets/_Project/Scripts/Train/TrainDeparture.cs`. Tiene `OnArrived()` publico como hook para el `TrainController`.

### Tren jugable (implementado)

- `[Train] Crear composicion visual del tren`
  Resultado: locomotora + vagon + area de cargo en escena.
  Aceptacion: el tren existe como objeto fisico navegable, no solo como trigger.
  Estado: implementado y colocado en `MainScene`.

- `[Train] Crear TrainController`
  Resultado: maquina de estados `AT_STATION / TRAVELLING` que mueve el tren por el spline.
  Aceptacion: el tren sale de MacLarens, recorre el rail y llega al pueblo de forma automatica al recibir la orden de salida.
  Estado: cubierto funcionalmente por `TrainSplineFollower` (`IsMoving`, `CurrentStation`) en vez de una clase `TrainController` dedicada.

- `[Train] Crear rail spline circular`
  Resultado: recorrido cerrado `MacLarens -> Town -> MacLarens` con Unity Splines.
  Aceptacion: el `TrainController` puede evaluar posicion y rotacion en cualquier punto del spline.
  Estado: implementado en `Assets/_Project/Scripts/Train/TrainSpline.cs`.

- `[Train] Crear estaciones MacLarens y Town`
  Resultado: zonas de docking donde el tren para y se reactiva `TrainDeparture`.
  Aceptacion: al llegar a una estacion el tren se detiene y `OnArrived()` reactiva la interaccion de salida.
  Estado: implementado como marcadores de distancia (`townPosition`/`macLarensPosition`) en `TrainSplineFollower`; `TrainDeparture.GetPrompt()` ahora tambien refleja la estacion actual.

- `[Train] Movimiento automatico del tren`
  Resultado: el tren sigue el spline sin fisica de rieles; el `TrainRoot` se posiciona y rota por `EvaluatePosition`.
  Aceptacion: los jugadores reparentados al `TrainRoot` viajan dentro sin codigo adicional.
  Estado: implementado; el reparentado de jugadores se resolvio con `TrainPassenger`/`TrainPassengerArea` (reemplaza el enfoque anterior de inyeccion de velocidad en `PlayerMotor`, descartado por drift).

- `[Train] Animacion de ruedas`
  Resultado: las ruedas giran a velocidad proporcional al desplazamiento del tren.
  Aceptacion: la rotacion es convincente visualmente; no bloquea el milestone de P2.
  Estado: implementado en `Assets/_Project/Scripts/Train/TrainWheelSpin.cs`.

- `[UI] Crear QuotaUI`
  Resultado: mostrar cuota, cargo y estado del dia.
  Aceptacion: el jugador sabe cuanto falta y si ya puede activar la salida.
  Estado: implementado, separado en `QuotaUI` (cuota/estado) y `CargoValueUI` (valor en transito).

## Milestone P2

> El jugador aparece en MacLarens, entra al tren, viaja fisicamente hasta el pueblo por el rail, puede bajarse, recoger loot, volver al tren y regresar a MacLarens. Sin enemigos todavia.
> Estado: todas las piezas de sistema existen; falta playtest end-to-end documentado para cerrar el milestone.

## P3 - Pueblo y loop singleplayer

- `[World] Montar Town_Western_01`
  Resultado: pueblo pequeno, legible y denso.
  Aceptacion: el jugador encuentra 4-6 puntos de interes claros en una vuelta corta.
  Estado: contenido de escena, no verificable desde el codigo.

- `[Loot] Crear LootSpawnPoint`
  Resultado: puntos authoring para colocar botin.
  Aceptacion: el spawner puede rellenar la escena sin referencias manuales una a una.
  Estado: implementado en `Assets/_Project/Scripts/Loot/LootSpawnPoint.cs`.

- `[Loot] Crear LootSpawner`
  Resultado: reparto aleatorio simple por partida.
  Aceptacion: reiniciar run cambia parte del reparto de loot.
  Estado: implementado en `Assets/_Project/Scripts/Loot/LootSpawner.cs`, con distribucion ponderada por distancia a puntos usados recientemente.

- `[Loot] Reposicion parcial de loot entre dias`
  Resultado: el loot sobrante del dia anterior persiste y se suman nuevos spawns hasta un limite.
  Aceptacion: el jugador nota diferencia de densidad entre dias sin necesitar procedural generation.
  Estado: implementado. `LootSpawner.Restock()` se suscribe a `RunManager.OnPhaseChanged` y repone al entrar en `RunPhase.TravelingToTown`; solo el host spawnea en multijugador.

## P4 - Threat y enemigos

- `[Enemy] Redirigir EnemyController hacia jugador`
  Resultado: el enemigo persigue jugador en vez de booze.
  Aceptacion: detecta al jugador y aplica presion funcional.
  Estado: implementado en `Assets/_Project/Scripts/Enemy/EnemyController.cs` (persigue al jugador vivo mas cercano vía `PlayerController.ActivePlayers` filtrado por `IsAlive`, ataca con cooldown). El registro de jugadores incluye copias remotas, asi que el host tambien persigue a los clientes.

- `[Enemy] Crear EnemySpawner`
  Resultado: spawns controlados por presupuesto de amenaza.
  Aceptacion: los enemigos aparecen en puntos validos y no saturan el mapa sin control.
  Estado: implementado en `Assets/_Project/Scripts/Enemy/EnemySpawner.cs`. `maxAliveEnemies`/`spawnInterval` ya no son fijos: se leen en vivo de `ThreatSpawnSettings[]` (uno por `ThreatLevel`) segun `ThreatManager.Instance.CurrentLevel`. Los puntos se filtran por `EnemySpawnPoint.IsValid()` (activo, `ThreatLevel` minimo, cooldown de reuso, sin jugador demasiado cerca) y se elige el de mayor `GetScore()` (mas lejos de cualquier jugador) en vez de uno aleatorio.

- `[Enemy] Crear Hitbox`
  Resultado: enemigos reciben más daño si son disparados en la cabeza.
  Aceptacion: los enemigos reciben distinto daño dependiendo del lugar donde se les dispara.
  Estado: implementado en `Assets/_Project/Scripts/Enemy/Hitbox.cs` + multiplicador de headshot en `Weapon.cs`.

- `[Core] Crear ThreatManager`
  Resultado: scalar de amenaza y thresholds simples.
  Aceptacion: recoger loot aumenta threat y el HUD se actualiza.
  Estado: implementado en `Assets/_Project/Scripts/Core/ThreatManager.cs`. MVP: `+threatPerSecond` constante mientras la escena esta activa, `+lootPickupThreat` una unica vez por item de loot (deduplicado via `ItemInstance.HasTriggeredThreat`). Expone `CurrentLevel` (`ThreatLevel` en `Assets/_Project/Scripts/Core/ThreatLevel.cs`, calculado con 4 umbrales configurables) y eventos `OnThreatChanged`/`OnThreatLevelChanged`. Se resetea a 0 en `RunManager.OnDayChanged`. Sin decay por matar enemigos (decision explicita de diseno).

- `[UI] Crear ThreatUI`
  Resultado: lectura clara del nivel de peligro.
  Aceptacion: el jugador entiende cuando se esta sobreexponiendo.
  Estado: implementado en `Assets/_Project/UI/ThreatUI.cs` (mismo patron que `QuotaUI`: texto + barra opcional, se suscribe a `ThreatManager.OnThreatChanged`).

- `[Train] Implementar Board Train y countdown`
  Resultado: extraccion legible y con tension.
  Aceptacion: volver al tren no finaliza al instante; hay una ventana de riesgo corta.
  Estado: implementado. Countdown de `TrainDeparture` (Fase 2) + aceleracion/deceleracion progresiva de `TrainSplineFollower` (ease-in/ease-out via `accelerationDistance`/`decelerationDistance`).

- `[Train] Implementar aceleracion progresiva de salida`
  Resultado: el tren ofrece una ultima oportunidad corta para subirse.
  Aceptacion: existe una ventana de riesgo legible en la que llegar tarde aun puede salvar la run.
  Estado: implementado en `TrainSplineFollower` (arranque con `SmoothStep`/`minCreepSpeed`, muy lento al inicio para dar tiempo a subirse).

## P5 - MVP freeze

- `[P5.1][Core] Crear GameStateManager`
  Resultado: cuatro estados globales (`Menu`, `Run`, `Success`, `Fail`) con entrada, salida y evento `OnStateChanged`.
  Aceptacion: `Fail` y `Success` detienen gameplay, spawning e interacciones; ningun sistema usa una fase funcional del run como estado global.

- <span style="color:#2f81f7">✅ `[P5.2][Core] Separar RunManager y flujo funcional del run`</span>
  Resultado: esqueleto jugable `MacLarens -> Town -> MacLarens -> Finish Day`, con fases funcionales dentro de `Run`.
  Aceptacion: cada transicion tiene owner, validacion y siguiente paso; el primer milestone puede recorrer el flujo con botones o datos temporales sin softlocks.
  Estado: validado en Play Mode. Las transiciones `MacLarens -> TravelingToTown -> Town -> LeavingTown -> ReturningToMacLarens -> ResolvingDay` ocurren via gameplay real (sin botones de debug).

- `✅ [P5.3][Economy] Crear MoneyManager de equipo`
  Resultado: `TeamMoney`, `AddMoney()` y `TrySpendMoney()` como unica fuente del efectivo comun.
  Aceptacion: no existe dinero individual; vender y comprar modifican el bote y todas las operaciones pasan por este manager.

- `✅ [P5.4][Core] Convertir QuotaManager en deuda y cuotas`
  Resultado: deuda total, deuda restante, cuota base, modificadores de cuota, deuda pagada y dias restantes separados de `CargoValue` y `TeamMoney`.
  Aceptacion: `Finish Day` calcula la cuota efectiva, comprueba el dinero disponible, descuenta el pago de `TeamMoney` y suma exactamente ese pago a `DebtPaid`; `DeliveredValue` no se usa como deuda pagada.

- `✅ [P5.5][Train] Completar entrega física de loot en MacLarens`
  Resultado: los `LootItem` fisicos que entran en `LootDeliveryPoint` se retiran del mundo/cargo y su valor se añade a `TeamMoney`.
  Aceptacion: cada `LootItem` se procesa una sola vez, `CargoValue` se recalcula y queda en cero cuando se entrega todo, sin convertir directamente un total agregado como atajo.
  Estado: implementacion realizada.

- `✅ [P5.6][World] Crear fase MacLarens y Finish Day`
  Resultado: zona segura con venta, compras, curacion/municion, preparacion y accion explicita de cierre.
  Aceptacion: el equipo puede vender y comprar antes de cerrar; al pulsar `Finish Day` se bloquean nuevas compras y el resultado de la cuota queda fijado.
  Estado: `FinishDayInteractable` implementado para `ResolvingDay`. `ShopStand` implementado: interactuable que entrega un `LootDataSO` y descuenta su `Price` de `TeamMoney`, disponible durante `RunPhase.MacLarens` y `RunPhase.ResolvingDay` (ambas representan estar en la zona segura) con dinero y espacio de inventario suficientes. Falta colocar y probar los objetos en escena.

- <span style="color:#2f81f7">✅ `[P5.7][Extraction] Resolver abandono del pueblo al partir el tren`</span>
  Resultado: la salida del tren cierra la expedicion y el fog limpia jugadores atrasados, cuerpos, enemigos, loot restante y entidades temporales antes del regreso.
  Aceptacion: ninguna entidad temporal del pueblo afecta al siguiente dia y el tren continua a MacLarens sin softlock.
  Estado: validado en Play Mode. `TownExitMarker`, `TownExtractionResolver` y la limpieza (jugadores, loot, enemigos, threat) funcionan correctamente y la fase pasa a `ResolvingDay` al llegar a MacLarens.

- <span style="color:#2f81f7">✅ `[P4/P5][Threat] Activar threat exclusivamente dentro de Town`</span>
  Resultado: el threat aumenta durante `RunPhase.Town`, se resetea al abandonar Town y permanece detenido durante viajes, extraccion y resolucion.
  Aceptacion: el valor no aumenta en `MacLarens`, `TravelingToTown`, `LeavingTown`, `ReturningToMacLarens` ni `ResolvingDay`; vuelve a cero al cruzar `TownExitMarker`.
  Estado: validado en Play Mode.

- `[P5.8][Player] Implementar cuerpo recuperable y penalizacion`
  Resultado: la muerte deja un cuerpo transportable; cuerpo a bordo revive al llegar al MacLarens y cuerpo abandonado respawnea alli con modificador de cuota.
  Aceptacion: la resolucion distingue cuerpo recuperado y abandonado; el wipe se evalua por separado y provoca `Fail` inmediato.

- `[P5.9][Fail] Implementar team wipe y cuota fallida`
  Resultado: todas las derrotas llegan a `GameState.Fail` con causa y resultado visibles.
  Aceptacion: el wipe es inmediato; la cuota solo causa fallo al cerrar el ultimo dia sin dinero suficiente; `Restart` recarga una run limpia y `Main Menu` sale del flujo.

- `[P5.10][Success] Implementar deuda pagada`
  Resultado: `DebtRemaining == 0` lleva a `GameState.Success`.
  Aceptacion: el pago final se descuenta del bote, no se puede comprar ni continuar la run despues y se ofrecen `Play Again` y `Main Menu`.

- `✅ [P5.11][Story] Crear Owner de MacLarens`
  Resultado: NPC interactuable que muestra dialogos ciclicos sin ownership de reglas ni de estado de la run.
  Aceptacion: los textos avanzan al interactuar (`texto 1 -> texto 2 -> texto 3 -> texto 1`); el dia, dinero y deuda se muestran en un componente separado (`FinishDayStatusUI`), no en el Owner.
  Estado: `MacLarensOwner` simplificado a dialogo puro (sin leer RunManager/QuotaManager/MoneyManager). `FinishDayStatusUI` implementado en el objeto de Finish Day, se actualiza por eventos (`OnDayChanged`, `OnMoneyChanged`, `OnDebtChanged`, `OnQuotaProgressChanged`) y al activarse, no solo al interactuar..

- `✅ [P5.12][UI] Completar HUD de Story Mode`
  Resultado: deuda, dinero, dia/cuotas, threat, inventario y `Cargo Value` son legibles y separados.
  Aceptacion: los valores se actualizan por eventos; los modificadores muestran feedback temporal, por ejemplo `Quota: $2,000 [+ $500 - jugador abandonado]`, sin que la UI calcule reglas.

- `[P5.13][UI] Completar UI del juego base`
  Resultado: Menú principal con opciones Play y Exit, pantalla de Success con Restart y Exit y pantalla de Defeat con Restart y Exit.
  Aceptacion: las pantallas son funcionales y aparecen cuando corresponde.

- `[P5.14][Balance] Tunear economia, cuotas, dias y threat del Story Mode`
  Resultado: la run ofrece decisiones de riesgo reales y una ruta posible de victoria.
  Aceptacion: se valida el flujo completo, una cuota persistente entre dias, un modificador por abandono, un team wipe, un fallo del ultimo dia y un pago de deuda completo.

- `[P5.15][Build] Generar build interna estable`
  Resultado: vertical slice portable y demostrable.
  Aceptacion: se puede jugar de inicio a fin sin usar el editor para arreglar nada.

- `[P5.18][Items] Consumibles y recarga de municion`
  Resultado: pocion (beber = vida completa; lanzarla y romperla = 50% a los jugadores en el radio), booze (beber = 10% de vida + camara borracha unos segundos) y recarga de municion comprando el arma que ya se lleva en mano a mitad de precio.
  Aceptacion: beber con `Fire` consume el item y cura en host; romper la pocion cura en area; el prompt del `ShopStand` cambia a "Refill" con el arma en mano y la municion se sincroniza con el owner.
  Estado: codigo implementado (`LootDataSO` campos de consumible, `PlayerInventory.TryConsumeActive`, `PlayerController.ApplyDrunk`, `BreakableOnImpact` curacion en area, `ShopStand` refill). Falta crear el asset/prefab de pocion, pasar `LiquorSO` a `Consumable` y validar en Play Mode.

## P6 - Multiplayer host-authoritative (despues del freeze singleplayer)

Regla de bloque: no se abre P6 hasta que P5 este cerrado y validado en Play Mode. Ninguna tarea de P6 reimplementa gameplay: reutiliza el mismo flujo request -> validate -> apply que ya usa singleplayer, con el host como validador unico.

### Checklist ejecutable de tareas de P6

#### P6.0 - Congelar ownership map

- `[Network] Auditar y congelar ownership de sistemas`
  Resultado: tabla escrita de que sistema es server-owned, client-owned o solo presentacion, cubriendo Core, Player, Loot, Train, Enemy y GameState.
  Aceptacion: ningun sistema queda ambiguo; la tabla se usa como checklist en el resto de P6 y no se reabre salvo bloqueo real.
  Estado: ownership congelado en [Auditoria de ownership P6](Auditorias/Auditoria_Ownership_P6.md).

#### P6.1 - Servicios + lobby

- `[Network] Instalar Authentication + Relay + NGO`
  Resultado: servicios de Unity Gaming Services inicializados al arrancar el juego.
  Aceptacion: el proyecto autentica sesion anonima y puede crear/solicitar un join code de Relay sin pasos manuales fuera del flujo previsto.
  Estado: base NGO implementada y validada estaticamente en `NetworkBootstrapper` y `NetworkSessionManager`; Authentication/Relay pendiente por paquetes y configuracion.

- `[UI] Crear flujo de lobby (Host / Join / Start)`
  Resultado: pantalla de multijugador en el Menu Principal: crear partida (genera codigo), unirse con codigo, lista de jugadores conectados.
  Aceptacion: el host ve entrar a cada cliente en tiempo real; `Start` solo lo puede pulsar el host y solo con al menos 1 jugador presente.
  Estado: codigo base de lobby implementado: lista replicada, ready, start host-only y desconexion previa al inicio. Falta UI y validacion en Play Mode.

- `[Network] Resolver desconexion antes de iniciar`
  Resultado: un jugador que abandona el lobby antes de `Start` se retira sin romper el estado de los demas.
  Aceptacion: el lobby sigue siendo usable con cualquier combinacion de entradas/salidas de 1 a 4 jugadores antes del inicio.
  Estado: codigo de desconexion previa al inicio implementado; falta validacion jugable.

- `[QA] Gate 1/2/3/4 del lobby`
  Resultado: validacion del lobby en 1, 2, 3 y 4 jugadores (host incluido) antes de pasar a la partida.
  Aceptacion: se puede crear, unirse, ver lista actualizada y lanzar la partida sin errores ni jugadores fantasma.
  Estado: pendiente de Play Mode en 1/2/3/4 jugadores; no se cierra sin Editor y Relay.

#### P6.2 - Jugador de red y salud

- `[Network] Spawnear jugador de red con ownership por cliente`
  Resultado: cada cliente controla su propio jugador; el host valida posicion/estado, camara y animator quedan locales.
  Aceptacion: cada cliente ve moverse a los demas sin poder mover el personaje de otro cliente.
  Estado: `NetworkPlayerSpawner` y `NetworkPlayer` implementados; falta prefab/scene wiring, `NetworkTransform` configurado y validacion de movimiento en Play Mode.

- `[Network] Sincronizar salud, muerte y cuerpo recuperable`
  Resultado: `Health` y muerte son autoritativos en host; el cuerpo recuperable de P5.8 se replica para todos.
  Aceptacion: un jugador muere, todos ven el mismo cuerpo, y solo el host resuelve `Revive()` y la penalizacion al llegar a MacLarens.
  Estado: base de salud autoritativa implementada en `NetworkHealth`; la muerte y el revive se
  replican (`Health.ApplyReplicatedHealth` dispara `OnDeath`/`OnRevived` en clientes), el cuerpo
  abandonado se oculta via `NetworkPlayer.IsBodyHidden` y el teleport de respawn lo ejecuta el owner
  (`NetworkPlayer.TeleportFromServer`). Falta el rig de ragdoll en `Player.prefab` y validar en Play Mode.

- `[QA] Gate de movimiento y muerte`
  Resultado: validacion de movimiento, camara y muerte/recuperacion en sesiones de prueba.
  Aceptacion: no hay divergencia entre clientes tras 10+ minutos de partida.
  Estado: pendiente.

#### P6.3 - Inventario, loot y economia

- `[Network] Inventario server-authoritative`
  Resultado: pickup, drop, throw y deposito viajan como intencion de cliente y se validan/aplican en host.
  Aceptacion: ningun cliente puede duplicar loot ni superar los 4 slots manipulando su input local.
  Estado: `NetworkInventoryState` replica por `ItemId` (slots, item en mano como slot -1 y municion)
  y cada cliente reconstruye su `PlayerInventory` via `ApplyReplicatedState` usando `LootCatalog`;
  `NetworkInventoryAuthority` valida en host seleccion, drop y throw. Falta validar en Play Mode.

- `[Network] Loot y `LootRegistry` sincronizados`
  Resultado: existencia, spawn y ownership temporal de cada `LootItem` viven en el host y se replican a todos.
  Aceptacion: todos los clientes ven el mismo objeto desaparecer al recogerse y reaparecer al soltarse, sin duplicados.
  Estado: `NetworkLootItem.IsCollected` se actualiza en pickup y drop del host (tambien los del
  propio host). Falta añadir `NetworkTransform` + `NetworkRigidbody` a los prefabs de loot para que
  los clientes vean caidas y lanzamientos, y validar en Play Mode.

- `[Network] `MoneyManager`/`QuotaManager` server-authoritative`
  Resultado: `TeamMoney`, deuda, cuota efectiva y modificadores se calculan y aplican solo en host.
  Aceptacion: venta, compra y `Finish Day` producen el mismo resultado para todos los clientes al mismo tiempo.
  Estado: los managers usan `NetworkRole.IsClientOnly` para bloquear mutaciones en clientes y
  `NetworkEconomyState` aplica dinero, cuota, cargo y entregado en clientes
  (`MoneyManager.ApplyReplicatedMoney`, `QuotaManager.ApplyReplicatedState`). Falta Play Mode.

- `[QA] Gate de inventario y economia`
  Resultado: validacion por 2 y 4 jugadores de looteo, venta, compra y sincronizacion de inventario.
  Aceptacion: no hay loot duplicado, dinero duplicado ni inventario desincronizado.
  Estado: pendiente.

#### P6.4 - Tren, entrega y dia

- `[Network] Sincronizar `TrainCargo`/`LootDeliveryPoint``
  Resultado: deposito fisico y entrega a `TeamMoney` resueltos en host; movimiento del tren por el spline replicado a todos.
  Aceptacion: cualquier cliente que deposite loot lo ve reflejado igual en todos los clientes y en la cuota.
  Estado: la entrega solo se resuelve en host y despawnea el loot entregado. El cargo ya no se
  reparenta en clientes: `NetworkCargoState` fija cada item relativo al vagon en `LateUpdate`.
  Falta validar cargo/entrega en Play Mode.

- `[Network] Sincronizar `TrainDeparture` (countdown y aceleracion)`
  Resultado: decision de salida, countdown y aceleracion progresiva son un unico estado replicado, no un timer local por cliente.
  Aceptacion: todos los clientes ven el mismo countdown y el mismo instante de salida.
  Estado: `NetworkTrainState` replica distancia, velocidad, estacion y movimiento; los clientes
  siguen el spline con `TrainSplineFollower.SetReplicatedState` y el countdown se muestra en todos
  via RPC. Los jugadores sobre el tren se sincronizan relativos al vagon (`NetworkTrainRider`).
  Falta Play Mode.

- `[Network] Sincronizar extraccion, fog y limpieza de Town`
  Resultado: `TownExtractionResolver` corre solo en host y replica el resultado (abandonados, recuperados, limpieza de loot/enemigos).
  Aceptacion: la resolucion es identica para todos los clientes en la misma partida.
  Estado: `TownExtractionResolver` y los spawns/limpieza de gameplay se ejecutan solo en host;
  falta wiring y validacion de fog/resultado visual en Play Mode.

- `[QA] Gate de tren y extraccion`
  Resultado: validacion de salida, abandono, penalizacion y resolucion de dia durante run completa.
  Aceptacion: un cliente atrapado al salir del tren no rompe la partida del resto.
  Estado: pendiente.

#### P6.5 - AI y threat

- `[Network] `ThreatManager`/`EnemySpawner` autoritativos en host`
  Resultado: threat, spawn y comportamiento de `EnemyController` se calculan solo en host; transform/estado/ataques se replican.
  Aceptacion: ningun cliente ve enemigos o niveles de threat distintos entre si.
  Estado: `EnemySpawner`, `EnemyController` y `ThreatManager` solo simulan en host;
  `NetworkThreatState` (en el objeto `NetworkEconomyState` de `MainScene`) aplica threat y nivel en
  clientes. Falta configurar prefabs de enemigos y validar en Play Mode.

- `[Balance] Escalar threat y densidad de enemigos por numero de jugadores`
  Resultado: `ThreatTuningSO`/`EnemySpawner` leen un multiplicador segun jugadores conectados (1/2/3/4) en vez de un valor fijo.
  Aceptacion: la presion por jugador se mantiene comparable entre 1 y 4 jugadores (ajuste per-capita con techo, no escalado lineal sin limite).
  Estado: `NetworkThreatState` centraliza multiplicadores 1/2/3/4 y replica threat, nivel,
  jugadores y enemigos vivos. Falta calibracion jugable.

- `[QA] Gate 1/2/3/4 de amenaza`
  Resultado: comprobacion de la curva de threat/enemigos en las 4 configuraciones de jugadores.
  Aceptacion: la amenaza es jugable en todas las configuraciones sin trivializar ni romper la partida.
  Estado: pendiente de Play Mode; la ruta de codigo y el estado replicado ya estan preparados.

#### P6.6 - Game state y desconexiones

- `[Network] Sincronizar `GameStateManager`/`RunManager` para todos`
  Resultado: fases de `RunManager` y estados de `GameStateManager` (`Success`/`Fail`) son un unico valor replicado por el host.
  Aceptacion: todos los clientes entran y salen de `Success`/`Fail` en el mismo instante y ven la misma pantalla.
  Estado: `NetworkGameState` (colocado en el objeto `NetworkEconomyState` de `MainScene`) y
  `NetworkRunState` replican estado global, dia y fase; falta validar Success/Fail en Play Mode.

- `[Network] Reconexion o abandono durante la run`
  Resultado: un cliente desconectado durante `Run` no bloquea al resto; su jugador pasa a cuerpo abandonado o estado inerte segun corresponda.
  Aceptacion: la partida sigue siendo terminable por el resto del equipo con 1, 2 o 3 jugadores restantes.
  Estado: base de despawn y marcado de cuerpo muerto implementada; falta resolver jugador vivo,
  host desconectado, retorno al menu y validacion en Play Mode.

- `[QA] Gate de desconexion`
  Resultado: pruebas forzadas de desconexion de cliente y host en Town, tren y MacLarens.
  Aceptacion: el resto del equipo sigue jugando o cerrando correctamente la partida.
  Estado: pendiente; requiere Relay, escena configurada y sesiones reales.

#### P6.7 - Balance final y cierre de fase

- `[Core] Crear config de balance por numero de jugadores`
  Resultado: fuente unica (ScriptableObject o extension de los tuning existentes) que ajusta cuota base, threat y densidad de enemigos segun 1/2/3/4 jugadores.
  Aceptacion: cambiar el numero de jugadores en el lobby ajusta estos valores automaticamente sin tocar otros sistemas.
  Estado: pendiente.

- `[QA] Gate final de P6`
  Resultado: run completa jugada en 1, 2, 3 y 4 jugadores sin desincronizacion grave.
  Aceptacion: sin duplicar loot/dinero, sin desincronizar cuota/threat/tren y sin softlock al desconectar un cliente.
  Estado: pendiente.

### Regla de prioridad

> **Estado de P6:** los bloques 0-6 tienen base de codigo, pero P6 no esta cerrado como milestone
> jugable. Relay, prefabs, escenas, politica de jugador vivo desconectado, presentacion visual y QA
> siguen abiertos.

- Primero se cierran sistemas del core loop y autoridad del host.
- Luego se sincronizan UI y lobby.
- Multiplayer no adelanta trabajo de singleplayer si el loop aun no esta probado.
- Contenido extra solo entra cuando el sistema base ya es estable.

## P7 - Polish posterior

- `[Hazards] Nitroglicerina y barril de polvora`
  Resultado: explosivos configurables por `ExplosionEffectSO` (radio, daño con caida, fuerza, knockdown, threat, VFX/SFX). La nitro explota al lanzarla o dispararla; el barril al dispararlo. Daña jugadores y enemigos en el radio, encadena otros explosivos y tumba a los jugadores en ragdoll; se levantan tras unos segundos si siguen vivos.
  Aceptacion: host y cliente ven la misma explosion, el daño solo se aplica en host y el jugador tumbado recupera el control donde cayo.
  Estado: codigo implementado (`Explosive`, `PlayerKnockdown`, `ExplosionEffectSO`, hook en `BreakableOnImpact`, `RequestDetonateServerRpc`). Falta crear assets/prefabs y validar en Play Mode.

- `[Enemy] Planta rodadora explosiva`
  Resultado: enemigo kamikaze que persigue al jugador vivo mas cercano y explota al alcanzarlo; tambien explota al dispararle o al pillarla otra explosion.
  Aceptacion: host y cliente la ven rodar y explotar en el mismo sitio; la explosion usa las mismas reglas que la nitro.
  Estado: codigo implementado (`EnemyController.selfDestruct`, `RollingVisual`). Falta prefab y validar en Play Mode.

- `[Tool] Lazo`
  Resultado: herramienta (`InventoryItemType.Tool`) que con `Fire` lanza una cuerda con alcance: atrae objetos sueltos a las manos del jugador y tumba en ragdoll a otros jugadores hacia el.
  Aceptacion: el host valida item, alcance, objetivo y cooldown; todos ven la cuerda; el jugador lazado se levanta tras el knockdown.
  Estado: codigo implementado (`LassoTool`, cuerda con `LineRenderer` en curva Bezier). Falta asset/prefab y validar en Play Mode.

- `[Combat] Anadir shotgun o rifle`
- `[Loot] Añadir un outline al apuntar a un loot item`
  Resultado: los loot items resaltan visualmente cuando el jugador apunta hacia ellos.
  Aceptacion: al apuntar a un loot item, este se destaca con un outline visible cuando está en rango de interacción.
- `[Loot] Implementar abstracción de items para tener herramientas utilizables`
  Resultado: tenemos un sistema flexible que extiende la lógica existente de LootItems para crear herramientas u objetos utilizables de uno o varios usos con distintas funcionalidades.
  Aceptacion: se pueden crear y usar herramientas desde el inventario, y su comportamiento se refleja correctamente en el juego.
- `[Enemy] Anadir más enemigos solo si los primeros son estables`
  Cactus invencible que se mueve cuando nadie lo mira, planta rodadora que persigue a los jugadores y al estar cerca explota, zombie enano que intenta robar lootItems del tren y llevárselos hasta su escondite, fantasma que atraviesa paredes y solo puede matarse utilizando/lanzandole el LootItem de la cruz

- `[Enemy] Fantasma inmortal que atraviesa paredes`
  Resultado: enemigo sin NavMesh que se mueve en linea recta hacia el jugador y no puede morir (solo repelerse, p. ej. con la cruz).
  Aceptacion: persigue atravesando geometria, ignora el daño y su posicion se replica desde el host.
  Estado: post-MVP (decidido en la revision del 2026-09-30).

- `[Enemy] Cactus que solo se mueve si nadie lo mira`
  Resultado: enemigo que se congela mientras esta en el campo de vision de cualquier jugador vivo.
  Aceptacion: el host comprueba frustum + linea de vision de todos los jugadores y el cactus solo avanza cuando nadie lo ve.
  Estado: post-MVP (decidido en la revision del 2026-09-30).

- `[UI] Crear selector de personaje en el maclarens??`
  Resultado: El jugador puede cambiar su personaje.
  Aceptacion: El jugador puede cambiar su personaje y todos los jugadores ven el personaje seleccionado (activar o desactivar el gameobject correspondiente, todos los personajes se encuentran dentro del root del modelo).
  
- `[P5.16][QoL] Cierta UI del mundo siempre apuntando al jugador`
  Resultado: crear un script que haga que ciertos elementos de la UI del mundo siempre apunten al jugador.
  Aceptacion: se pueden ver los elementos de la UI del mundo apuntando al jugador.

- `[P5.17][QoL] Separar partes del cuerpo del jugador visibles por la cámara`
  Resultado: conseguir que la cámara del jugador solo muestre las manos y pies de su personaje.
  Aceptacion: la cámara del jugador solo muestra sus propias manos y pies de su personaje, y no otras partes del cuerpo. Los demás jugadores serán siempre completamente visibles

- `[Audio] Sonidos de armas, loot, enemigos y tren`
- `[VFX] Muzzle flash, hit, blood y warning de threat`
- `[UI] Refinar HUD final`
- `[UI] Menú de opciones (gráficos, sonido, salir de la partida...)`

## P8 - Post-MVP: Infinity Mode y cooperacion avanzada

- `[Core] Infinity Mode`
  Resultado: cuotas progresivamente mayores sin deuda final, con record de dinero, cuota y dias sobrevividos.
  Aceptacion: separado de Story Mode y sin cambiar sus reglas de victoria o derrota.
- `[UI] Menú de opciones de partida (fuego amigo...)`

## Reglas de prioridad

- Primero se cierran sistemas del core loop.
- Luego se cierran UX minima y failure states.
- Multiplayer no adelanta trabajo de singleplayer si el loop aun no esta probado.
- Contenido extra solo entra cuando el sistema base ya es estable.