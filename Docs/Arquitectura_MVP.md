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

- `RunManager`: controla estados de partida, dia actual, transiciones success/fail y reinicio de run.
- `QuotaManager`: controla cuota actual, valor depositado y si ya se desbloqueo la opcion de escapar.
- `ThreatManager`: controla amenaza actual, thresholds y eventos de escalado.

### Player

- `PlayerInputHandler`: solo lectura de input. No decide reglas de gameplay.
- `PlayerMotor`: movimiento, gravedad, sprint, salto y crouch si aplica.
- `PlayerHealth`: vida, daño, muerte, eventos y posible estado spectator mas adelante.
- `PlayerInteractor`: raycast, prompt, validacion simple y llamada a `Interact`.
- `PlayerInventory`: slots, add, remove, drop, query de valor total transportado.
- `Weapon`: disparo, recarga, consumo de municion y hit processing.
- `PlayerCarryState`: estado del objeto llevado en la mano; aplica restricciones de movimiento y combate y controla la representacion visual.

### Loot

- `LootDataSO`: catalogo de item, valor, icono, prefab y tuning visual basico.
- `LootItem`: representacion en mundo, implementa `IInteractable`, conoce su `LootDataSO`.
- `LootSpawnPoint`: punto marcado donde puede aparecer loot.
- `LootSpawner`: puebla la escena al inicio de la run.

### Transporte de objetos

Algunos objetos de loot tendran transporte especial en mano. Una caja fuerte es el primer ejemplo: ocupa un slot del inventario, tiene una representacion visible en las manos, reduce la velocidad y bloquea sprint y uso del arma mientras se lleva.

Reglas del MVP:

- `LootDataSO` define si el objeto se puede llevar en mano y sus restricciones de transporte; no guarda el estado vivo.
- `PlayerInventory` posee la ocupacion del slot y solicita iniciar o terminar el transporte.
- `PlayerCarryState` aplica el estado runtime y expone `IsCarrying`, `MovementSpeedMultiplier` y `CanUseWeapon`.
- `PlayerMotor` consulta la restriccion de velocidad; no decide que objeto se esta llevando.
- `Weapon` rechaza disparo y recarga mientras `CanUseWeapon` sea falso.
- La malla visible del objeto se instancia o activa bajo un `CarryAnchor` de manos. El objeto del mundo se desactiva mientras esta en inventario para evitar duplicados.
- Soltar es una operacion atomica: primero se valida la posicion y despues se libera el slot; si no hay posicion valida, el objeto permanece en la mano.
- La UI de inventario representa el slot ocupado y el HUD puede mostrar el objeto llevado, pero ninguna UI decide las restricciones.

El transporte no se mezcla con el contrato `IInteractable`: interactuar puede iniciar el transporte, pero el estado de llevarlo pertenece al jugador y al inventario. En multiplayer, el host validara pickup, drop, slot y restricciones; la representacion visual sera una consecuencia del estado sincronizado.

### Train

- `TrainCargo`: recibe loot del jugador y notifica valor depositado.
- `TrainDeparture`: comprueba condiciones de salida y gestiona countdown.
- `TrainInteractable`: si hace falta, encapsula prompts y acciones del tren.

### Enemy

- `EnemyController` o `EnemyAI`: persecucion, navegacion y ataque al jugador.
- `EnemySpawner`: instancias controladas por threat y por presupuesto de spawn.
- `EnemyHealth`: si se separa de `Health`, mantiene responsabilidad de enemigo.
- `EnemyRagdoll`: visual y post-mortem, nunca ownership de reglas.

### UI

- `InteractUI`: prompt de uso y barras de progreso puntuales.
- `InventoryUI`: slots y valor transportado.
- `QuotaUI`: cuota, cargo y estado de salida.
- `ThreatUI`: lectura de amenaza actual.
- `GameStateUI`: success, fail, next day y estados globales.

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
    -> LootItem se consume o se desactiva
    -> InventoryUI se refresca
    -> ThreatManager.AddLootThreat
```

### Deposit

```text
PlayerInteractor
    -> TrainCargo.Interact
    -> PlayerInventory.DepositAll o DepositSelected
    -> QuotaManager.AddCargoValue
    -> QuotaUI se refresca
    -> TrainDeparture reevalua si puede salir
```

### Escape

```text
Players aboard
    -> TrainDeparture countdown
    -> RunManager.ResolveRun
    -> Success o fail
    -> Next day o reset
```

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
- El tren no necesita moverse de verdad antes del freeze.
- El pueblo debe resolverse en una sola escena jugable principal.
- La representacion fisica del loot en el tren es opcional; el valor depositado no lo es.

## Deuda tecnica que no debemos comprar

- `PlayerController` convertido en god class
- UI que calcula reglas de gameplay
- Scripts que mezclan input, estado, presentacion y persistencia
- Refactors masivos del shooter actual sin necesidad inmediata
- Multiples sistemas de spawn antes de validar uno solo
- `FindGameObjectsWithTag` como solucion permanente para gameplay critico