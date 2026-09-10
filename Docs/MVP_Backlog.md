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
  Estado: implementado. `LootSpawner.Restock()` existia pero no se llamaba desde ningun sitio; ahora se suscribe a `RunManager.OnDayChanged`.

## P4 - Threat y enemigos

- `[Enemy] Redirigir EnemyController hacia jugador`
  Resultado: el enemigo persigue jugador en vez de booze.
  Aceptacion: detecta al jugador y aplica presion funcional.
  Estado: implementado en `Assets/_Project/Scripts/Enemy/EnemyController.cs` (persigue al jugador activo mas cercano vía `PlayerController.ActivePlayers`, ataca con cooldown).

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

## P6 - Multiplayer despues del freeze

- `[Network] Instalar Authentication + Relay`
  Resultado: base de servicios para host y join por codigo.
  Aceptacion: el proyecto puede crear o unirse a una sesion Relay sin pasos manuales fuera del flujo previsto.

- `[UI] Crear UI para que un jugador hostee y otro pueda introducir el código para unirse + lobby con empezar partida??`
  Resultado: UI de multijugador añadida al Menú Principal.
  Aceptacion: .

- `[Network] Host + Join con Relay`
  Resultado: dos jugadores conectan por codigo.
  Aceptacion: un cliente entra en la partida del host y comparte el mismo estado.

- `[Network] Hacer inventario server authoritative`
  Resultado: pickup, drop y deposit se validan en servidor.
  Aceptacion: no se puede duplicar loot con acciones del cliente.

- `[Network] Hacer loot server authoritative`
  Resultado: existencia y ownership de loot sincronizados.
  Aceptacion: todos ven el mismo objeto desaparecer, caer y depositarse.

- `[Network] Hacer AI y threat server authoritative`
  Resultado: enemigos y ritmo de presion unificados.
  Aceptacion: clientes no divergen en spawns ni comportamiento.

- `[Network] Sincronizar cuota, cargo y salida del tren`
  Resultado: todos leen el mismo progreso de run.
  Aceptacion: finalizar run funciona para host y clientes.

- `[Network] Sincronizar knockback, incapacitacion y recovery basicos`
  Resultado: el juego queda preparado para hazards fisicas simples sin estados imposibles.
  Aceptacion: un empujon o caida controlada no desincroniza posicion ni control entre host y cliente.

- `[UI] Crear selector de personaje en el maclarens??`
  Resultado: El jugador puede cambiar su personaje.
  Aceptacion: El jugador puede cambiar su personaje y todos los jugadores ven el personaje seleccionado (activar o desactivar el gameobject correspondiente, todos los personajes se encuentran dentro del root del modelo).

## P7 - Polish posterior

- `[Combat] Anadir shotgun o rifle`
- `[Enemy] Anadir más enemigos solo si los primeros son estables`
- `[Loot] Ampliar objetos y objetos utilizables (pociones, lazo para agarrar cosas, dinamita...)`
- `[Hazard] Añadir plantas rodadoras explosivas si la build ya es estable`
- `[Enemy] Prototipar cactus observador solo despues de cerrar hazards simples`
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