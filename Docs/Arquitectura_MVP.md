# MacLarens Defense - Arquitectura MVP

## Objetivo tecnico

Montar una arquitectura pequena, clara y extensible que permita llegar rapido al MVP sin bloquear el paso posterior a NGO + Relay.

La palabra clave no es "escalable a cualquier cosa". La palabra clave es "suficientemente estable para construir el loop completo sin rehacerlo cada semana".

## Principios de arquitectura

- Una clase, una responsabilidad principal.
- El estado runtime vive en componentes de runtime, no en UI ni en ScriptableObjects.
- Los ScriptableObjects contienen datos de tuning y catalogos, no estado mutable de la run.
- La UI observa estado y dispara intenciones; no decide reglas.
- Las interacciones se resuelven por capacidad (`IInteractable`), no por `if` gigantes dentro del jugador.
- El codigo singleplayer debe quedar preparado para un futuro servidor autoritativo.

## Topologia online objetivo

La implementacion objetivo no es full mesh P2P.

La arquitectura recomendada para el proyecto es:

- Host como `listen server`
- Host autoritativo para gameplay
- Clientes enviando intenciones
- NGO para objetos y RPCs
- Relay para conectividad
- Authentication para inicializacion de servicios

Decisiones derivadas:

- No usar autoridad distribuida para gameplay del MVP.
- No dejar que clientes creen, destruyan o valoren loot por su cuenta.
- No dejar AI ni threat en clientes.
- No basar la correccion del juego en sincronizacion de fisicas emergentes entre peers.

## Estrategia de escenas

Para el MVP, el camino mas corto es este:

- `Bootstrap`: opcional al principio; obligatorio si aparecen servicios persistentes.
- `MainMenu`: minimo antes del freeze.
- `Town_Western_01`: escena principal jugable que contiene pueblo + tren + zona segura.
- `Sandbox_Loot`: escena minima de validacion para pickup, drop y deposit.
- `Sandbox_Combat`: escena minima de validacion para threat y enemigos.

Decision recomendada:

> Antes del freeze no crear una escena de tren separada con viaje real. El tren debe funcionar como zona segura y punto de extraccion dentro de la misma escena jugable principal.

Eso elimina complejidad de scene flow mientras el juego todavia esta probando si su loop base es divertido.

## Estructura recomendada de scripts

```text
Assets/_Project/Scripts/
  Core/
  Interaction/
  Player/
  Loot/
  Train/
  Enemy/
  World/
  UI/
  Networking/
```

`Networking/` puede permanecer vacia hasta la fase 6, pero deja claro desde ya donde ira la logica de NGO.

## Ownership de sistemas

### Core

- `GameStateManager`: mantiene solo `Menu`, `Run`, `Success` y `Fail`, emite `OnStateChanged` y bloquea gameplay fuera de `Run`.
- `RunManager`: controla dia actual, fases funcionales de `Run`, cierre del dia, team wipe y reset de run.
- `MoneyManager`: unica fuente de `TeamMoney`; aplica ventas y gastos del equipo.
- `QuotaManager`: controla deuda total, deuda restante, cuota base/efectiva, modificadores, dias restantes y pago de cuota al cerrar el dia. `DebtPaid` es el progreso real de deuda; `DeliveredValue` es solo valor entregado desde el tren.
- `ThreatManager`: controla amenaza actual, thresholds y eventos de escalado.

Las fases funcionales (`MacLarens`, `TravelingToTown`, `Town`, `LeavingTown`, `ReturningToMacLarens`, `ResolvingDay`) pertenecen a `RunManager`, no son estados globales. `GameStateManager` es el unico que cambia a `Success` o `Fail`.

### Player

- `PlayerInputHandler`: solo lectura de input. No decide reglas de gameplay.
- `PlayerMotor`: movimiento, gravedad, sprint, salto y crouch si aplica.
- `PlayerHealth`: vida, daño, muerte, eventos y posible estado spectator mas adelante.
- `PlayerInteractor`: raycast, prompt, validacion simple y llamada a `Interact`.
- `PlayerInventory`: slots, add, remove, drop, throw, query de valor total transportado y estado activo.
- `ItemInstance`: estado runtime por objeto; hoy conserva al menos referencia al objeto del mundo y municion por arma.
- `ItemHolder`: representacion visual del item activo en mano y handoff entre mano y mundo.
- `PlayerPoseController`: decide pose, hold points y parametros de animator segun el item activo.
- `Weapon`: disparo, recarga, consumo de municion y hit processing usando la `ItemInstance` activa.

### Loot

- `LootDataSO`: catalogo de item, valor, icono, prefab de mundo, prefab de mano y perfil de pose.
- `LootItem`: representacion en mundo, implementa `IInteractable`, conoce su `LootDataSO` y se asocia a una `ItemInstance` concreta.
- `LootSpawnPoint`: punto marcado donde puede aparecer loot.
- `LootSpawner`: puebla la escena al inicio de la run.
- `BreakableOnImpact`: opcional en props fragiles; rompe el objeto si recibe un impacto suficientemente fuerte.

### Transporte de objetos

Algunos objetos de loot tendran transporte especial en mano. Una caja fuerte es el primer ejemplo: ocupa un slot del inventario, tiene una representacion visible en las manos, reduce la velocidad y bloquea sprint y uso del arma mientras se lleva.

Reglas del MVP:

- `LootDataSO` define perfil de pose, prefab de mundo y opcionalmente prefab visual de mano; no guarda estado vivo.
- `ItemInstance` guarda el estado runtime del objeto, incluida la referencia a su `LootItem` del mundo y la municion si es arma.
- `PlayerInventory` posee la ocupacion del slot y decide si el item esta en mochila o en mano.
- `PlayerMotor` consulta si el item activo es pesado para aplicar multiplicador de velocidad y bloqueo de sprint.
- `Weapon` rechaza disparo y recarga si no hay arma activa y sincroniza su municion con la `ItemInstance` activa.
- `PlayerPoseController` decide la pose de carry, pistol y shotgun y el hold point correspondiente.
- `ItemHolder` mantiene la representacion visual en mano. Si el item tiene `HeldPrefab`, usa esa visual; si no, cae al prefab de mundo o al arma residente del jugador.
- El objeto real del mundo se desactiva al recogerlo y se reactiva al soltarlo o lanzarlo; no se duplica para la interaccion fisica.
- El lanzamiento cargado usa el crosshair, mantiene una barra de progreso hasta soltar o cancelar y reaplica fisicas sobre el mismo objeto del mundo.
- La UI de inventario representa iconos y slot activo; la UI de municion depende del arma activa, no de la existencia de un `Weapon` visible.

El transporte no se mezcla con el contrato `IInteractable`: interactuar puede iniciar el transporte, pero el estado de llevarlo pertenece al jugador y al inventario. En multiplayer, el host validara pickup, drop, slot y restricciones; la representacion visual sera una consecuencia del estado sincronizado.

### Train

- `TrainCargo`: conserva los `LootItem` fisicos dentro del tren y expone `CargoValue` calculado a partir de ellos.
- `LootDeliveryPoint`: procesa cada `LootItem` entregado, lo retira del mundo/cargo y añade su valor a `MoneyManager` mediante una operacion idempotente.
- `TrainDeparture`: comprueba condiciones de salida y gestiona countdown.
- `TrainSplineFollower`: expone `townExitMarker` como `Transform` serializado, muestra su posicion con Gizmo, emite `OnTownExitReached` al cruzarlo y `TrainDeparture` alimenta la transicion de fase.
- `TrainInteractable`: si hace falta, encapsula prompts y acciones del tren.

### Extraction

- `TownExtractionResolver`: procesa una sola vez el abandono del pueblo, solicita despawn de jugadores/enemigos, limpia loot dentro del trigger, resetea threat y destruye entidades temporales configuradas.
- `InTownTrigger`: trigger general de Town que informa de jugadores y loot dentro del area; conserva el nombre de clase temporalmente para no romper referencias serializadas de Unity.

### Enemy

- `EnemyController` o `EnemyAI`: persecucion, navegacion y ataque al jugador.
- `EnemySpawner`: instancias controladas por threat y por presupuesto de spawn.
- `EnemyHealth`: si se separa de `Health`, mantiene responsabilidad de enemigo.
- `EnemyRagdoll`: visual y post-mortem, nunca ownership de reglas.

### UI

- `InteractUI`: prompt de uso y barras de progreso puntuales.
- `InventoryUI`: slots y valor transportado.
- `QuotaUI`: cuota efectiva, modificadores, cargo y estado de salida.
- `ThreatUI`: lectura de amenaza actual.
- `GameStateUI`: success, fail, next day y estados globales.
- `StoryHUD`: deuda, dinero comun, dia y cuota; observa managers y no decide reglas.
- `MacLarensOwner`: dialogo ciclico puro, sin leer ni depender de `RunManager`/`QuotaManager`/`MoneyManager`.
- `FinishDayStatusUI`: vive en el objeto de Finish Day; muestra dia, dinero, deuda y cuota, se refresca por eventos (`OnDayChanged`/`OnMoneyChanged`/`OnDebtChanged`/`OnQuotaProgressChanged`) y en `OnEnable`, no solo al interactuar.

## Contrato de interaccion recomendado

La interfaz minima propuesta por el roadmap es valida, pero conviene enriquecerla ligeramente para no acoplar prompt y validacion al jugador:

```csharp
public interface IInteractable
{
    bool CanInteract(PlayerInteractor interactor);
    string GetPrompt(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);
}
```

Ventajas:

- El prompt vive en el objeto interactuable.
- El propio objeto decide si esta habilitado.
- El jugador no necesita saber si interactua con loot, puertas, tren o NPC.

## Flujo de datos principal

### Pickup

```text
PlayerInteractor
    -> LootItem.Interact
    -> PlayerInventory.TryAdd
    -> LootItem real se desactiva y la ItemInstance conserva su referencia
    -> InventoryUI se refresca
    -> ThreatManager.AddLootThreat
```

### Drop / Throw

```text
Drop input held
    -> PlayerController carga fuerza y muestra barra
    -> Release throw
    -> PlayerInventory.TryThrowSelected
    -> LootItem real se reactiva frente al jugador
    -> Rigidbody recibe impulso dirigido por el crosshair
    -> InventoryUI y ammo UI se refrescan
```

### Deposit

```text
PlayerInteractor
    -> TrainCargo.Interact
    -> PlayerInventory.DepositAll o DepositSelected
    -> LootDeliveryPoint procesa cada LootItem fisico
    -> LootItem se retira del cargo/mundo y su valor se suma una vez a TeamMoney
    -> CargoValue se recalcula y queda en cero cuando no queda ningun item
    -> MoneyManager emite cambio
    -> MacLarens permite vender, comprar y preparar
```

### Finish Day

```text
Player interactua con Finish Day
    -> RunManager valida que el tren esta en MacLarens y no hay una resolucion activa
    -> QuotaManager calcula EffectiveQuota = BaseQuota + Modifiers
    -> QuotaManager comprueba TeamMoney >= EffectiveQuota
    -> MoneyManager descuenta EffectiveQuota
    -> QuotaManager suma EffectiveQuota a DebtPaid y consume el dia
    -> DebtRemaining == 0 ? Success : siguiente dia/cuota
    -> Si es ultimo dia y no alcanza : Fail
```

### Escape

```text
Players aboard
    -> TrainDeparture countdown
    -> RunManager cambia la fase a LeavingTown
    -> TrainSplineFollower cruza TownExitMarker
    -> ExtractionResolver determina jugadores/cuerpos recuperados y abandonados
    -> InTownTrigger limpia jugadores, loot y entidades temporales; EnemyController limpia enemigos
    -> ThreatManager resetea threat y detiene su incremento fuera de Town
    -> RunManager cambia la fase a ReturningToMacLarens
    -> Train llega a MacLarens y cambia a ResolvingDay
    -> El cierre de MacLarens resuelve el dia; salir del pueblo no paga la cuota
```

### Observadores de estado

```text
Run / Money / Quota / Threat / Inventory
    ├── FinishDayStatusUI lee estado y se refresca por eventos
    └── StoryHUD lee estado y muestra valores/modificadores temporales
```

`MacLarensOwner` solo cicla dialogo estatico; no lee estado de la run. `FinishDayStatusUI` y la UI no modifican dinero, deuda, fases, muerte ni condiciones de victoria/derrota.

## Datos tuneables que si merecen ScriptableObject

- `WeaponDataSO` ya existente
- `LootDataSO`
- `DayQuotaTableSO` o `RunConfigSO`
- `ThreatTuningSO`

Opcionales mas adelante:

- `EnemySpawnTableSO`
- `LootSpawnTableSO`

Regla:

> Ningun ScriptableObject debe almacenar el inventario actual, loot depositado o amenaza viva de la partida.

## Reglas de estado y autoridad

Para no sufrir al pasar a multiplayer, cada sistema nuevo debe dejar clara esta separacion:

- Quien solicita la accion
- Quien valida la accion
- Quien aplica el cambio de estado
- Quien notifica a la UI

Ejemplo recomendado incluso en offline:

```text
Input
    -> RequestPickup
    -> ValidatePickup
    -> ApplyPickup
    -> RaiseInventoryChanged
```

Ese patron reduce la cantidad de refactor necesaria al introducir RPCs o `ServerRpc` mas adelante.

## Politica de fisicas y ragdolls

Las fisicas divertidas entran solo si se pueden controlar con un presupuesto tecnico pequeno.

Reglas:

- El loot del MVP no necesita ser un sistema de fisica compleja en red.
- Las fisicas con impacto jugable real deben ser autoritativas en host cuando llegue multiplayer.
- El ragdoll es presentacion o incapacitacion temporal; no debe convertirse en la fuente de verdad del movimiento base.
- La recuperacion tras un golpe o explosion debe volver al controlador de jugador de forma predecible.

Aplicacion directa:

- `TrainDeparture`: la aceleracion del tren si afecta a empujes, zonas de captura o perdidas de boarding debe resolverla el host.
- `ExplosiveTumbleweed`: puede existir como hazard puntual mas adelante, pero su explosion, radio y fuerza deben nacer desde una fuente autoritativa.
- `ObserverCactusEnemy`: no debe ser el primer enemigo ni el primer experimento de fisicas o visibilidad en red.

## Preparacion para NGO + Relay

Estado actual del proyecto:

- NGO ya esta presente en `Packages/manifest.json`.
- Authentication y Relay deben entrar explicitamente en la fase 6.

Diseñar singleplayer de forma "server-ready" implica esto:

- El loot tiene un owner claro y un ciclo de vida controlado.
- El inventario no se modifica directamente desde UI.
- Enemigos, threat, cuota y cargo tienen una unica fuente de verdad.
- El random de spawns se centraliza en un sistema y no en cada prefab.
- Se evitan decisiones de gameplay basadas en objetos encontrados con `Find*` una y otra vez.
- Las hazards de fisica se tratan como sistemas concretos, no como side effects de rigidbodies sueltos.

Cuando llegue fase 6, la autoridad recomendada es esta:

- Servidor: loot, inventario real, train cargo, quota, threat, enemies y daño.
- Cliente: input, camara, FX locales y presentacion.

## Decisiones concretas para el MVP

- La cuota se alcanza al depositar, no al recoger.
- Llegar a cuota desbloquea salida; no auto-termina la run.
- La entrega convierte loot en `TeamMoney`; `CargoValue` no es dinero ni deuda pagada.
- La cuota se comprueba solo en `Finish Day`, despues de vender y comprar.
- La deuda pagada se descuenta del dinero al cerrar el dia y persiste entre dias.
- Los modificadores de cuota tienen una fuente identificable y se muestran temporalmente, por ejemplo `Quota: $2,000 [+ $500 - jugador abandonado]`.
- Team wipe termina el run inmediatamente; una cuota fallida espera al cierre del ultimo dia.
- El modo historia entra en el MVP; Infinity Mode queda post-MVP.
- El pueblo debe resolverse en una sola escena jugable principal.
- La representacion fisica del loot en el tren es opcional; `CargoValue` y la entrega no lo son.

## Deuda tecnica que no debemos comprar

- `PlayerController` convertido en god class
- UI que calcula reglas de gameplay
- Scripts que mezclan input, estado, presentacion y persistencia
- Refactors masivos del shooter actual sin necesidad inmediata
- Multiples sistemas de spawn antes de validar uno solo
- `FindGameObjectsWithTag` como solucion permanente para gameplay critico