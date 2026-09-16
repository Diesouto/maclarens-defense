# Auditoria de ownership de sistemas para P6

## Objetivo

Congelar la tabla de ownership de los sistemas actuales para que el trabajo de multijugador no reescriba la arquitectura del gameplay ni duplique la logica del loop singleplayer. Esta auditoria es la base de trabajo para P6 y debe considerarse un punto de bloqueo: no se cambia salvo que aparezca un bloqueo real de diseño o de red.

## Principio de autoridad

- El host es la unica fuente de verdad del gameplay relevante.
- Los clientes envian intenciones y leen estado sincronizado.
- La camara, input local, UI, VFX y animacion local quedan en el cliente.
- No se sincroniza nada que pueda derivarse del estado autoritativo del host.
- Los sistemas de run, loot, inventario, train, enemigo, threat, daño y game state son server-owned por defecto.

## Tabla de ownership congelada

| Sistema | Estado actual en P5 | Owner autoritativo | Cliente | Sincronizado | Comentario |
| --- | --- | --- | --- | --- | --- |
| GameStateManager | Global run state | Host | Lee | SI | `Menu`, `Run`, `Success`, `Fail` deben ser un unico estado replicado. |
| RunManager | Fases funcionales de run | Host | Lee | SI | `MacLarens`, `TravelingToTown`, `Town`, `LeavingTown`, `ReturningToMacLarens`, `ResolvingDay` se resuelven en host. |
| QuotaManager | Cuota, deuda, pago y progreso | Host | Lee | SI | `CurrentQuota`, `DebtPaid`, `DebtRemaining`, `DeliveredValue`, `CurrentCargoValue` y modificadores se calculan en host. |
| MoneyManager | TeamMoney compartido | Host | Lee | SI | La compra y venta del equipo se validan en host. |
| LootRegistry | Registro global de loot activo | Host | Lee | SI | Fuente para existencia y duplicado de loot. |
| LootItem | Objeto jugable del mundo | Host | Lee / visual | SI | El estado de existencia, pickup y drop se valida en host. \nNo deben crearse items desde clientes. |
| LootSpawner | Spawn y restock de loot | Host | Lee | SI | Envios de spawns y respawn solo desde host. |
| PlayerInventory | Inventario activo del jugador | Host | Lee | SI | `TryAdd`, `TryThrowSelected`, `TryDropSelected` deben validarse en host. |
| PlayerController | Input + control local del jugador | Cliente local | Owner local | Parcial | Input y camara son locales; los estados de gameplay siguen siendo autoritativos. |
| PlayerMotor | Movimiento y colisiones | Cliente local / host para validacion | Owner local | Parcial | El cliente aplica input; el host decide la verdad ejecutiva del movimiento si se hace networking. |
| PlayerInputHandler | Input | Cliente local | Owner local | No | Solo genera intenciones. |
| Health | Salud, daño y muerte | Host | Lee | SI | `TakeDamage`, `Die`, `Revive` deben ser decisiones del host. |
| Player death / body recovery | Cuerpo recuperable y abandono | Host | Lee | SI | La eliminacion, cuerpo y reaparicion se resuelven por host. |
| EnemyController | IA y ataques | Host | Lee | SI | Enemigos y decisiones de ataque son decisivas en host. |
| EnemySpawner | Spawn de enemigos | Host | Lee | SI | No spawn local en clientes. |
| ThreatManager | Amenaza y niveles | Host | Lee | SI | Solo host decide la presion del mapa. |
| TrainCargo | Cargo del tren y valor en transito | Host | Lee | SI | `ItemsInCargo` y `CargoValue` se manejan en host. |
| LootDeliveryPoint | Entrega de loot a dinero | Host | Lee | SI | El delivery se valida y aplica solo desde host. |
| TrainSplineFollower | Movimiento y estado del tren | Host | Lee | SI | Visual interpolation local permitido, pero autoridad del movimiento es host. |
| TrainCarFollower | Seguimiento del tren | Host | Lee | SI | Sigue la motion del lead train, no simula por si mismo. |
| TrainPassenger / TrainPassengerArea | Montaje de jugadores sobre el tren | Host + local | Owner local | Parcial | Local para la presentacion del carriage delta; la verdad del estado del tren se mantiene en host. |
| IInteractable / PlayerInteractor | Interaccion base | Cliente-Driven + validacion host | Owner local | Parcial | El cliente dispara la interaccion; el host valida la accion. |
| UI / InteractUI / HUD | Visualizacion | Cliente local | Owner local | No | UI no decide reglas ni aplica gameplay. |
| VFX / particles / FX | Presentacion | Cliente local | Owner local | No | Deben ser visuals y no fuente de verdad. |
| Camera / crosshair | Presentacion local | Cliente local | Owner local | No | No forman parte del gameplay sincronizado. |

## Ownership por dominio funcional

### 1. Core gameplay (host-owner)

- `GameStateManager`
- `RunManager`
- `QuotaManager`
- `MoneyManager`
- `ThreatManager`
- `LootRegistry`

Estas clases manejan el estado principal de la partida y deben ser una sola fuente de verdad al activar la run. Cualquier cambio de dia, deuda, threat, quota o success/fail se debe producir desde el host. Los clientes solo leen el valor replicado.

### 2. Player gameplay (owner local + authority host)

- `PlayerController`
- `PlayerInputHandler`
- `PlayerMotor`
- `PlayerInventory`
- `Health`

Las decisiones de input y presentacion son locales. El estado real de salud, inventario y muerte es del host. Esto significa que el cliente puede realizar una intento local, pero el servidor valida y aplica el resultado final.

### 3. Loot y train (host-owner)

- `LootItem`
- `LootSpawner`
- `LootDeliveryPoint`
- `TrainCargo`
- `TrainSplineFollower`
- `TrainCarFollower`
- `TrainPassenger`

Estas clases definen el estado del mapa, del tren y del botin. Si un item o cargo desaparece, el host decide el resultado y todos lo ven igual.

### 4. Enemigos y amenaza (host-owner)

- `EnemyController`
- `EnemySpawner`

El spawn, la IA, el ataque y la muerte del enemigo deben quedar bajo una unica autoridad. Los clientes no spawnean, no atacan y no resuelven amenaza por su cuenta.

## Reglas de red que deben cumplirse a partir de esta auditoria

### Regla 1: no crear estado duplicado de gameplay en cliente

No se valida un valor en cliente si ese mismo valor es determinante para la regla del juego. Ejemplos:
- inventario de un jugador
- dinero compartido
- debt restante
- cargo actual
- existencia de loot
- vida real y muerte
- spawn de enemigos
- fase de run / success / fail

### Regla 2: separar intencion local de aplicacion autoritativa

El flujo correcto es:

```text
Cliente: input / click / interact
    -> host valida
    -> host aplica estado
    -> host sincroniza el resultado
    -> cliente renderiza el resultado
```

### Regla 3: no sincronizar datos derivables

Ejemplos de valores que NO necesitan ser enviados por separado porque ya pueden derivarse del estado autoritativo:
- `TrainCargo.CargoValue` puede derivarse del estado real del cargo.
- `Quest / quota progress` puede calcularse desde `DebtPaid`, `TeamMoney`, cuota vigente y run config.
- `UI` de estado no debe duplicar reglas.

### Regla 4: dejar local solo lo visual

Se mantienen locales:
- input del jugador
- camara local
- crosshair
- animacion local
- particles / FX
- UI de feedback visual
- smoothing / interpolation visual

## Reglas de corte de alcance para P6

- No se introduce una segunda arquitectura offline para singleplayer.
- No se crea gameplay simulado en cliente que luego tenga que revalidarse en host.
- No se reescribe `RunManager`, `QuotaManager`, `GameStateManager`, `ThreatManager` ni la logica de loot para adaptarlos a una estructura de red sin pasar primero por la auditoria.
- Cualquier sistema nuevo para multijugador debe insertar su autoridad bajo esta tabla definitiva.

## Criterio de cierre

La tabla de ownership queda congelada para P6 y se considera la base de trabajo autorizada para empezar el desarrollo de multijugador.

Criterio de cierre: si un sistema presente en esta auditoria no encaja en una de las categorias server-owned / client-local / synchronized, debe documentarse y aprobarse antes de introducir red.

## Referencia inmediata para la implementacion

Los siguientes sistemas son los que deberian revisarse primero para empezar a trabajar en el networking real del proyecto:

1. `GameStateManager`
2. `RunManager`
3. `QuotaManager`
4. `MoneyManager`
5. `LootRegistry`
6. `LootItem`
7. `PlayerInventory`
8. `TrainCargo`
9. `LootDeliveryPoint`
10. `TrainSplineFollower`
11. `PlayerMotor`
12. `Health`
13. `EnemyController`
14. `EnemySpawner`
15. `ThreatManager`

Estos son los bloques que se convierten en la primera ola de tareas de P6.
