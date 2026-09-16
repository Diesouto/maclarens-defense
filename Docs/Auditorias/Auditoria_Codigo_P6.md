Estado actual: codigo base cerrado y validado estaticamente. La integracion de servicios
queda bloqueada hasta instalar Authentication/Relay y configurar el proyecto en Unity Editor.
# Auditoria de codigo para P6

## Objetivo

Definir la parte de P6 que se puede implementar directamente en codigo sin depender de Unity Editor para prefabs, escenas o referencias serializadas. Esta lista es la base de trabajo programatico del multijugador y se ejecuta en el orden de la arquitectura actual del juego.

## Regla principal

No se reescribe el gameplay ni se crea una implementacion offline paralela. Se toma el loop validado en singleplayer y se convierte en una arquitectura host-authoritative con:

- servidor/host propietario del estado del gameplay
- clientes enviando intenciones y leyendo estado sincronizado
- el mismo flujo `request -> validate -> apply`
- balance por numero de jugadores centralizado

## Orden de trabajo de codigo

### Bloque 0 - Base de autoridad y networking

#### T0.1 - Congelar ownership por sistema

Objetivo:
- dejar documentado y codificado el ownership final de cada sistema antes de agregar Netcode.

Sistemas a cerrar:
- `GameStateManager`
- `RunManager`
- `MoneyManager`
- `QuotaManager`
- `LootRegistry`
- `LootItem`
- `PlayerInventory`
- `TrainCargo`
- `LootDeliveryPoint`
- `TrainSplineFollower`
- `EnemySpawner`
- `EnemyController`
- `ThreatManager`
- `Health`

Regla:
- El host decide estado, el cliente solo envia intenciones y presenta el resultado.

#### T0.2 - Crear capa de red base sin tocar gameplay

Objetivo:
- preparar la infraestructura para autenticacion, relay y lobby sin mezclar codigo de gameplay con red.

Tareas:
- crear `NetworkBootstrapper` / `NetworkSessionManager`
- crear `LobbyManager` con lista de jugadores conectados
- crear `RelayJoinCodeManager`
- crear `PlayerSlotState` para nombre + ready + isHost
- crear `SessionState` con `Lobby`, `Starting`, `Running`, `Ended`

No incluir aun:
- logica de inventario
- spawn de loot
- IA del enemigo
- run-state gameplay
Implementado en codigo:
- `NetworkBootstrapper`: ciclo host/client, approval de 1-4 jugadores y cierre local del cliente.
- `NetworkSessionManager`: estado replicado, lista de jugadores, ready, start validado por host,
	eventos de entrada/salida y cierre de sesion cuando desaparece el host.
- La sesion es el `LobbyManager` de esta fase; no se crea una segunda fuente de verdad.

Pendiente externo al codigo disponible:
- `RelayJoinCodeManager`: requiere los paquetes de Unity Services Relay/Authentication, que aun no
	estan en `Packages/manifest.json`.

### Bloque 1 - Lobby y spawn de jugadores
Estado actual: base de codigo cerrada; integracion jugable pendiente de Relay y Editor.

#### T1.1 - Host/Join por codigo

Objetivo:
- soportar un host y clientes conectados usando la misma arquitectura para 1/2/3/4 jugadores.

Tareas:
- `TryCreateRelaySession()` y `TryJoinRelaySession(code)`
- `OnClientConnected` / `OnClientDisconnected`
- `HostStartGame()` validado solo en el host
- `LobbyState` replicado a todos los clientes
- `PlayerJoined` / `PlayerLeft` events
Estado: `OnClientConnected`/`OnClientDisconnected`, lista replicada, ready y start host-only estan
implementados. Host/Join por codigo Relay queda pendiente de servicios y UI.

#### T1.2 - Spawn de NetworkPlayer

Objetivo:
- cada cliente tiene un `NetworkObject` con su propio `PlayerController` local y un estado visible para los demas.

Tareas:
- `NetworkPlayer` de referencia
- `SpawnPlayerForClient(ulong clientId)` en el host
- `OwnerClientId` para ownership local del jugador
- posicion/rotacion sincronizadas
- camara local del cliente siempre sobre su propio personaje
- `PlayerController` local no debe controlar el personaje del resto
Estado: implementado en `NetworkPlayerSpawner` y `NetworkPlayer`. El componente desactiva
`PlayerController`, input, interaccion y camara en copias no propietarias. `NetworkTransform` queda
como dependencia del prefab para la sincronizacion de transform.

#### T1.3 - Lobby disconnect rules

Objetivo:
- cerrar el lobby sin dejar jugadores fantasma ni estados rotos.

Tareas:
- host desconectado => cerrar sesion y volver a menu
- cliente desconectado antes del inicio => quitarlo de lobby
- cliente desconectado durante run => manejar abandono segun el contrato del juego
Estado: salida antes de iniciar elimina la entrada y el objeto de red; la desconexion del host marca
la sesion como `Ended`. El contrato de abandono durante `Run` pertenece al Bloque 6 y no se duplica
en esta capa.

## Cierre de Bloques 0 y 1 (codigo)

Validacion realizada: el analisis estatico no reporta errores en:
- `Assets/_Project/Scripts/Networking/NetworkBootstrapper.cs`
- `Assets/_Project/Scripts/Networking/NetworkSessionManager.cs`
- `Assets/_Project/Scripts/Networking/NetworkPlayerSpawner.cs`
- `Assets/_Project/Scripts/Networking/NetworkPlayer.cs`

No se puede declarar cierre jugable hasta completar en Unity Editor:
- registrar `NetworkPlayer` como prefab de red y asignar `NetworkObject`/`NetworkTransform`;
- crear la instancia de `NetworkSessionManager` y `NetworkPlayerSpawner` en la escena;
- asignar prefab y puntos de spawn;
- instalar/configurar Unity Services Authentication + Relay y crear el flujo UI de Host/Join.

### Bloque 2 - Estado de jugador y vida

Estado actual: base de autoridad de vida implementada en codigo; cuerpo recuperable y validacion
jugable siguen pendientes de integracion de prefab y escena.

#### T2.1 - Input local, autoridad de vida

Objetivo:
- separar input local del estado vital del personaje.

Tareas:
- `PlayerInputHandler` local solo emite inputs
- `Health` sincronizado y autoritativo en host
- `TakeDamage` y `Die` ejecutados en host
- `Revive` solo en host

#### T2.2 - Cuerpo recuperable

Objetivo:
- dejar preparada la muerte como estado de juego y no como animacion local.

Tareas:
- `PlayerBodyState` con `IsDead`, `IsRecoverable`, `IsAbandoned`, `BodyOwnerId`
- `BodyCarrier` con validacion autoritativa del transporte
- `BodyRecoveryManager` en host
- penalizacion por abandono en `QuotaManager` desde host

Implementado:
- `NetworkHealth` replica `CurrentHealth` con escritura exclusiva del servidor.
- `Health` enruta el daño del propietario a `RequestDamageServerRpc`; el host aplica daño, muerte,
  ragdoll y revive.

### Bloque 3 - Inventario, loot y economia

Estado actual: pickup y estado publico tienen base server-authoritative; drop, deposito y cierre de
dia requieren RPCs especificos en los siguientes pasos del bloque.

#### T3.1 - Inventario server-authoritative

Objetivo:
- impedir duplicado de loot y sobrellenado por cliente.

Tareas:
- `PlayerInventory.SetInventoryState` desde host
- `RequestPickupLootServerRpc` / `RequestDropLootServerRpc`
- `TryAddToInventory` y `TryRemoveFromInventory` con validacion del host
- `TryThrowSelected` validado por host
- `HasSpace`, `CanAdd`, `IsFull` resueltos por host

#### T3.2 - Loot server-authoritative

Objetivo:
- que la existencia del loot y su pickup sean un unico estado global.

Tareas:
- `LootItem` con `NetworkObject` propio si hace falta, o factoring de ownership por host
- `LootRegistry` como fuente de verdad del estado activo
- `RegisterLoot` / `UnregisterLoot` en host
- `RequestPickupServerRpc` validando distancia, disponibilidad, slot, `IsCollected`
- `RequestDropLootServerRpc` validando si el loot pertenece al jugador

#### T3.3 - Economia del equipo

Objetivo:
- dinero y quota movidos a una sola fuente de verdad.

Tareas:
- `MoneyManager` con `NetworkVariable<int>` `TeamMoney`
- `QuotaManager` con `NetworkVariable<int>` `DebtPaid`, `DebtRemaining`, `CurrentQuota`, `CurrentCargoValue`, `DeliveredValue`
- `TryPayCurrentQuota` ejecutado en host
- `OnMoneyChanged`, `OnQuotaProgressChanged` emitidos desde host
- `FinishDay` validado solo en host

Implementado:
- `NetworkLootItem.RequestPickupServerRpc` resuelve el jugador por `SenderClientId`, valida el
	inventario del host y evita doble pickup mediante `NetworkVariable<bool>`.
- `NetworkInventoryState` replica slots visibles y valor total desde el inventario del servidor.
- `NetworkEconomyState` replica dinero, deuda, cuota, cargo y valor entregado desde los managers
	existentes sin crear una segunda fuente de verdad.

Pendiente de codigo:
- request/validate/apply de drop, throw, deposito, compras y `FinishDay`;
- registro networkado de loot spawned dynamically y sincronizacion de inventario completo para que
	el cliente reconstruya objetos `ItemInstance` tras pickup.

### Bloque 4 - Tren y dia

Estado actual: base de autoridad de tren, entrega y resolucion de dia implementada en codigo;
la sincronizacion visual y la validacion jugable requieren configuracion de prefabs/escena.

#### T4.1 - Train movement autoritativo

Objetivo:
- el tren no se simula por cliente: host decide avance, arrival y salida.

Tareas:
- `TrainState` con `CurrentStation`, `Destination`, `IsMoving`, `CurrentSpeed`
- `TrainStateNetworkData` sincronizado por `NetworkVariable`
- `TrainDeparture` y `TrainSplineFollower` leyendo estado del host
- `TownExitMarker` evento del host

Implementado:
- `NetworkTrainState` replica estacion, destino, movimiento, velocidad y distancia.
- La salida del tren solo inicia la coroutine en el host; los clientes usan
	`RequestDepartureServerRpc`.

#### T4.2 - Cargo y entrega

Objetivo:
- el acumulado del tren debe representar loot real y no inventario local.

Tareas:
- `TrainCargo` calculando estado desde host
- `LootDeliveryPoint` validando solo lo que existe en host
- `DeliverLootServerRpc` y `TryDeliver` idempotente
- `MoneyManager.AddMoney` en host

Implementado:
- `NetworkLootDelivery` valida referencia de loot, distancia y entrega idempotente en el host.
- `LootDeliveryPoint` conserva la regla existente de acreditar dinero y despacha el objeto
	networkado mediante `NetworkObject.Despawn`.

#### T4.3 - Resolucion del dia

Objetivo:
- `Finish Day` y `Success`/`Fail` son una sola decision de host.

Tareas:
- `RequestFinishDayServerRpc()`
- validacion: `RunPhase.ResolvingDay`, dinero suficiente, no doble cierre
- `ApplyDayResolution()` en host
- `AdvanceDay`, `SetSuccess`, `SetFail` solo en host

Implementado:
- `FinishDayInteractable` usa `RequestFinishDayServerRpc` cuando la escena esta networkada.
- `NetworkTrainState` valida la fase `ResolvingDay` antes de llamar a `RunManager.FinishDay`.

Pendiente de codigo/editor:
- replicar la jerarquia de cargo y parentado visual del loot en todos los clientes;
- añadir el request/validate/apply de salida tambien a cualquier UI de tren que no pase por
	`TrainDeparture`;
- configurar `NetworkTransform`, `NetworkObject` y referencias serializadas en escena;
- validar aceleracion, town exit, entrega y resolucion de dia en Play Mode.

### Bloque 5 - Enemigos y threat

Estado actual: base host-authoritative implementada en codigo; la replicacion visual y el balance
final requieren configuracion de prefabs/escena y Play Mode.

#### T5.1 - Enemy spawn y AI autoritativos

Objetivo:
- que los enemigos no se creen ni comporten de forma divergente en clientes.

Tareas:
- `EnemySpawner` haciendo spawn solo en host
- `EnemyController` leyendo objetivo y atacando bajo host
- `EnemyState` replicado por `NetworkBehaviour` o `NetworkVariable` basico
- `Damage` y `Death` autoritativos

Implementado:
- `EnemySpawner` solo simula y crea enemigos en el host cuando NGO esta escuchando.
- Los enemigos networkados se spawnean desde el host y `NetworkEnemyState` replica muerte y ataque.
- `EnemyController` no ejecuta AI en copias cliente.
- `NetworkHealth` permite que el propietario o el servidor solicite/aplique dano; los enemigos
	server-owned reciben la validacion en host.

#### T5.2 - Threat por numero de jugadores

Objetivo:
- que la amenaza se aproxime a la dificultad real del grupo.

Tareas:
- `PlayerCountBalanceConfig` con valores para 1/2/3/4
- `ResolveCurrentThreatProfile()` en host
- `EnemySpawner` y `ThreatManager` leyendo la config centralizada
- no dispersar `if (playerCount == 2)` a lo largo del proyecto

Implementado:
- `NetworkThreatState` replica threat, nivel, jugadores conectados y enemigos vivos.
- `ResolveMultiplier` centraliza multiplicadores para 1/2/3/4 jugadores (1.0/1.25/1.5/1.75).
- `ThreatManager` aplica el multiplicador solo en el host, sin alterar la ruta singleplayer.

Pendiente de codigo/editor:
- registrar prefabs de enemigos con `NetworkObject`, `NetworkEnemyState`, `NetworkHealth` y
	`NetworkTransform`;
- confirmar que los spawns dinamicos usan prefabs registrados y que el NavMesh es identico en todos
	los peers;
- validar dano, muerte, conteo, threat y curva 1/2/3/4 en Play Mode.

### Bloque 6 - GameState, cuerpo y desconexiones

Estado actual: estado global y run phase replicados en codigo; desconexion host/client y cuerpo
recuperable requieren integracion de escena y Play Mode para considerarse cerrados.

#### T6.1 - Estado global sincronizado

Objetivo:
- `Success`, `Fail`, `Menu` y `Run` deben ser un estado compartido.

Tareas:
- `GameStateManager` con `NetworkVariable<GameState>`
- `SetFail(FailCause)` desde host
- `SetSuccess()` desde host
- `RestartRun` / `ReturnToMainMenu` si se decide desde host o desde codigo de return al menu

Implementado:
- `NetworkGameState` replica `GameState` y `FailCause`, y bloquea mutaciones directas de clientes.
- `NetworkRunState` replica `CurrentDay` y `RunPhase`, y bloquea mutaciones de `RunManager` en
	clientes.

#### T6.2 - Desconexiones

Objetivo:
- manejar clientes desaparecidos sin romper la partida.

Tareas:
- `OnClientDisconnect` en el host
- `ResolveDisconnectedPlayerState` segun si estaba vivo, muerto, cargando cuerpo, en tren o en MacLarens
- respetar rules de team wipe, abandono y penalizacion

Implementado:
- `BodyRecoveryManager` solo resuelve cuerpos en el host cuando NGO esta activo.
- Al desconectarse un cliente, `NetworkPlayerSpawner` marca como perdido su cuerpo muerto antes
	del despawn; la sesion elimina su entrada.

Pendiente de codigo/editor:
- cierre/retorno al menu visible para todos tras desconexion del host;
- estado de abandono para un jugador vivo desconectado durante `Run`;
- RPCs y autoridad completa de inventario, cargo, economia, compra y drop;
- Relay/Authentication, prefabs, escenas y gates de Play Mode.

## Veredicto de conversion multijugador del juego actual

No se puede confirmar que todas las funcionalidades actuales hayan quedado convertidas. La base de
autoridad y los estados publicos estan preparados, pero estos sistemas siguen teniendo mutaciones
locales o integracion incompleta:

| Sistema | Estado actual |
| --- | --- |
| Lobby/session | Base NGO implementada; Relay/UI/editor pendientes |
| Player spawn/camera/input | Codigo de ownership implementado; prefab/transform/playtest pendientes |
| Health/damage | Host-authoritative base implementada; prefab y validacion pendientes |
| Body recovery | Logica host-only base implementada; spawn/desconexion completa pendientes |
| Train/departure | Requests y estado replicado implementados; cargo visual y playtest pendientes |
| Enemy AI/spawn | Host-only base implementada; prefabs, transform y playtest pendientes |
| Threat | Estado y multiplicador replicados; calibracion y UI de cliente pendientes |
| GameState/run phase | Replicados y bloqueados para mutacion cliente |
| PlayerInventory | Estado visible replicado, pero drop/throw y cambios completos aun locales |
| Loot pickup | Pickup host-validado implementado; drops/spawns dinamicos aun incompletos |
| TrainCargo/delivery | Delivery host-validada implementada; cargo y salida de objetos requieren red completa |
| MoneyManager/QuotaManager | Snapshot replicado; compras, pago, cargo y mutaciones no tienen RPC completo |
| Finish Day | Request host implementado; resultado global depende de economia autoritativa pendiente |

Por tanto, hemos convertido la arquitectura y una parte importante del runtime, pero no todas las
funcionalidades jugables. El cierre real exige completar los RPCs de inventario/economia/cargo,
registrar y cablear prefabs/escenas, instalar Relay/Authentication y ejecutar los gates 1/2/3/4.

## Conversion de fuentes de verdad - estado actualizado

Implementado en codigo desde el ultimo corte:
- `NetworkInventoryAuthority`: seleccion, drop y throw llegan al host; el cliente no puede aplicar
	esos cambios directamente cuando el objeto esta networkado.
- `NetworkPurchaseAuthority`: compra validada por distancia, dinero, fase y capacidad en el host.
- `TrainCargo`: entrada, salida y eliminacion de cargo solo modifican cuota en el host.
- `NetworkCargoState`: replica ids de objetos en cargo y valor acumulado.
- `MoneyManager`, `QuotaManager` y `LootRegistry`: bloquean mutaciones de clientes networkados.
- `LootItem.CreateDroppedLoot`: los drops creados por el servidor se registran y spawnean como
	`NetworkObject` cuando el prefab lo permite.

La fuente de verdad del core loop queda asi:

```text
Cliente -> RPC de intencion
Host -> valida distancia, fase, capacidad y propiedad
Host -> modifica Inventory/Loot/Cargo/Money/Quota
Host -> replica snapshot y referencias NetworkObject
Clientes -> presentan el estado recibido
```

Limitaciones que no se pueden cerrar sin Editor/Play Mode:
- los prefabs deben incluir los componentes NetworkObject requeridos y estar registrados;
- los drops dinamicos solo pueden spawnear si su prefab tiene NetworkObject;
- la reconstruccion visual completa de `ItemInstance` y held items necesita referencias de prefab;
- Relay, Authentication, UI de lobby, escenas y pruebas reales siguen pendientes.

Funcionalidades que aun requieren una conversion de codigo posterior:
- sincronizacion visual de held items, animadores, ragdoll y efectos sigue siendo local/presentacion;
- el estado de abandono se replica antes del despawn, pero la politica de sustitucion/penalizacion
	de un jugador vivo durante `Run` aun necesita una decision de diseño y Play Mode.

Implementado desde este corte:
- `NetworkWeaponAuthority` enruta fire/reload al host; ammo, raycast y dano se aplican en el arma
	server-owned del jugador.
- `NetworkBodyCarrier` enruta pickup, drop y throw; `BodyCarrier` ejecuta fisica solo en host.
- `NetworkPlayer.IsAbandoned` marca desconexiones antes del despawn.
- `LootSpawner` y `TownExtractionResolver` ejecutan spawn y limpieza solo en el host.
- `NetworkBreakable` y `BreakableOnImpact` despawnean destruibles networkados desde el host.
- `NetworkPlayer` aplica una validacion basica de desplazamiento maximo para detectar teleports o
	velocidades imposibles.

Conclusion: las fuentes de verdad del core loop economico, armas, cuerpos, spawn, extraccion y
destruibles ya estan conectadas al host. El trabajo restante es de integracion y validacion, salvo
la politica de abandono de jugadores vivos y la sincronizacion visual de presentacion.

## Principios de implementacion para codigo

### 1. Request / Validate / Apply

Cada accion de gameplay debe seguir este esquema:

```text
Cliente -> RequestServerRpc
Host -> Validate request
Host -> Apply authoritative state
Host -> Sync network state
Clientes -> Read synchronized state / render local feedback
```

### 2. NetworkVariables para estado visible

Usar `NetworkVariable` para:
- `GameState`
- `TeamMoney`
- `CurrentQuota`
- `DebtRemaining`
- `EnemyAliveCount` si hace falta
- `TrainState`
- `Reinforced value` de estado publico

### 3. ServerRpc para intenciones

Usar RPC para:
- pickup / drop
- deposit
- purchase
- finish day
- train departure request
- enemy hit / damage request si hace falta

### 4. Local-only para presentacion

No integrar en red:
- camera / aim / look
- UI animation
- visual effects
- local input smoothing
- crosshair

## Bloque de trabajo recomendado para empezar

### Fase A - red base

1. `NetworkBootstrapper`
2. `LobbyManager`
3. `RelayBridge`
4. `Player spawn`

### Fase B - estado de partida

5. `GameStateManager` networked
6. `RunManager` host-authoritative
7. `QuotaManager` networked
8. `MoneyManager` networked

### Fase C - gameplay real

9. `PlayerInventory` authority
10. `LootRegistry` / `LootItem` authority
11. `TrainCargo` / `TrainDeparture` authority
12. `ThreatManager` / `EnemySpawner` authority

### Fase D - cierre

13. `Death/body recovery` authority
14. `Disconnection` handling
15. `PlayerCountBalanceConfig` central
16. QA gate 1/2/3/4

## Criterio de cierre de la auditoria de codigo

La parte de codigo de P6 queda cerrada cuando cada uno de estos bloques tiene un propietario claro, una ruta de validacion y una respuesta clara a:

- quien es la autoridad
- que estado es visible para todos
- que accion es una peticion del cliente
- que sistema no debe sincronizarse

La implementacion debe seguir la regla de no sobre-red: no se sincroniza todo, solo el estado que realmente afecta al loop del juego.
