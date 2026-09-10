# MacLarens Defense - Roadmap MVP

## Objetivo de producto

Construir en 5 semanas un vertical slice singleplayer que ya sea divertido por si mismo y en el que exista una decision real entre seguir saqueando o escapar.

La regla de oro del proyecto es esta:

> Llegar a la cuota no termina la partida automaticamente. Llegar a la cuota desbloquea la opcion de irse.

Eso fuerza el tipo de decisiones que queremos: codicia, discusion, panico, perdida absurda y momentos memorables.

## Pilares jugables

- El botin vale mas que las bajas. Matar enemigos solo sirve para seguir saqueando o escapar.
- La extraccion importa. El loot no cuenta hasta que vuelve al tren y se deposita.
- La amenaza sube por acciones del jugador, no por olas predefinidas.
- El pueblo debe ser pequeno, denso y facil de leer en menos de un minuto.
- El juego debe producir decisiones estupidas pero comprensibles entre amigos.

## Vision de producto y alcance de P5

P5 cierra el MVP jugable del modo historia alrededor de un unico loop. La deuda total, el dinero compartido, el cierre explicito del dia y los estados `Success`/`Fail` forman parte del MVP. Infinity Mode queda fuera y se conserva como trabajo post-MVP.

### Story Mode (modo principal del MVP)

- Los jugadores parten con una deuda total con MacLarens (ej. $10.000) y un numero de dias limite para pagarla.
- Cada dia puede cerrar una cuota parcial; al pagarla se genera la siguiente cuota.
- El loot vendido se convierte en `TeamMoney`. Comprar reduce ese dinero, pero nunca reduce la deuda ya pagada.
- `Finish Day` calcula la cuota efectiva con sus modificadores, comprueba el dinero disponible, descuenta el pago de `TeamMoney` y suma exactamente ese pago a `DebtPaid`.
- `DeliveredValue` representa valor entregado desde el tren; no sustituye a `DebtPaid` ni se usa como progreso de deuda.
- Si la cuota no se alcanza y quedan dias, la cuota pendiente y sus modificadores persisten para el siguiente dia; no se comprueba cuanto loot queda en el pueblo.
- Si se cierra el ultimo dia sin alcanzar la cuota efectiva, la run termina en `Fail`.
- Si la deuda restante alcanza cero, la run termina en `Success`.

### Infinity Mode (modo secundario)

- Sin deuda total ni fecha limite: el juego encadena cuotas cada vez mayores hasta que el equipo falla una cuota o el equipo entero muere.
- Requiere tracking de record (dinero total, cuotas completadas, dias sobrevividos): un sistema de meta-progresion nuevo que no existe hoy.
- Se construye despues de que Story Mode este completo y estable; no se reparte esfuerzo entre ambos modos antes del freeze.

### El fog y la extraccion forzada (P5)

- Al salir el tren del pueblo, este queda sellado por niebla: todo lo que quede dentro (jugadores que no llegaron al tren, cuerpos, enemigos, loot no entregado) se destruye.
- La extraccion resuelve primero que jugadores y cuerpos llegaron al tren; despues el fog/despawn limpia jugadores atrasados, cuerpos abandonados, enemigos, loot restante y entidades temporales.
- La salida debe tener un owner de limpieza unico y ejecutarse antes de comenzar el regreso, para que ningun objeto del pueblo sobreviva accidentalmente al siguiente dia.

Secuencia de fases:

```text
MacLarens -> TravelingToTown -> Town -> LeavingTown
    -> [TownExitMarker + cleanup] -> ReturningToMacLarens
    -> [llegada] -> ResolvingDay -> Finish Day -> MacLarens / Success / Fail
```

`ReturningToMacLarens` comienza al cruzar `TownExitMarker`, no al alcanzar una fraccion fija del spline. `ResolvingDay` comienza al llegar a MacLarens y permanece activo durante entrega, compras, resolucion de cuerpos y `Finish Day`.

`TownExitMarker` es un `Transform` serializado en `TrainSplineFollower` y tiene Gizmo propio de estacion/salida. El cleanup no depende de referencias a cada `EnemySpawner`: `EnemyController` mantiene los enemigos activos y el trigger general de Town mantiene los jugadores y loot que siguen dentro del pueblo. `ThreatManager` solo aumenta en `Town` y se resetea al salir.

### Recuperacion de cuerpos y penalizacion por abandono (P5, preparado para coop)

- Si un jugador muere en el pueblo, su cuerpo queda fisicamente en el mundo; el equipo puede cargarlo hasta el tren para revivirlo al llegar a MacLarens, o abandonarlo (revive igualmente en MacLarens, pero con penalizacion de cuota).
- Aunque su lectura principal es cooperativa, forma parte del contrato del run: un cuerpo recuperado llega a MacLarens y revive; un cuerpo abandonado provoca respawn en MacLarens y penalizacion de cuota.
- Punto de partida: hoy `Health.Die()` no tiene ningun estado de "cuerpo recuperable" (solo dispara ragdoll); `PlayerController.OnDeath()` simplemente desactiva el control del jugador.

### Arquitectura de flujo de Run

El flujo de partida debe tener un owner autoritativo desde P5. `RunManager` coordina las fases funcionales y `GameStateManager` mantiene los cuatro estados globales; ningun sistema secundario decide por su cuenta cuando termina la run:

```text
Run
 ├── CurrentMode (Story en el MVP; Infinity despues)
 ├── CurrentDay / DaysRemaining
 ├── CurrentQuota / DeliveredValue
 ├── RunState: AtMacLarens -> TravelingToTown -> InTown -> LeavingTown
 │             -> ReturningToMacLarens -> ResolvingDay -> QuotaCompleted / RunFailed
 └── Player state: Alive / Dead / BodyOnTrain / Respawned
```

`QuotaManager`, `TrainDeparture`, `TrainSplineFollower`, `LootDeliveryPoint`, `EnemySpawner` y el sistema de muerte/revive deben leer y alimentar este estado en vez de tomar decisiones de flujo por su cuenta. Las fases funcionales no son estados globales: solo `GameStateManager` puede entrar en `Success` o `Fail`.

## Objetivo online realista

El objetivo tecnico online no debe definirse como P2P full mesh.

Para este proyecto, lo realista con Unity 6 es esto:

- `Host-authoritative listen server`
- NGO para sincronizacion
- Relay para conectividad
- Authentication para bootstrap de servicios

En terminos de producto puede describirse como cooperativo online con host, pero el codigo debe asumir una unica fuente de verdad para loot, threat, enemigos, daño, cuota y salida del tren.

## Definicion del loop MVP

El MVP queda logrado cuando una persona puede hacer una run completa con este flujo:

```text
Train safe area
    -> Enter town
    -> Find loot
    -> Carry loot with a 4-slot inventory
    -> Threat rises from looting
    -> Enemies pressure the player
    -> Return to train
    -> Deposit loot
    -> Decide to leave or keep risking
    -> Sell loot into shared TeamMoney
    -> Buy and prepare at MacLarens
    -> Finish day and check quota
    -> Start next day, Success or Fail
```

## Lo que si entra exactamente en el MVP freeze

- 1 escena jugable principal con tren + pueblo
- 1 sandbox de loot y 1 sandbox de combate
- 1 arma estable
- 1 enemigo base estable
- 5 objetos de loot minimos, con objetivo de 8 antes del freeze
- 4 slots de inventario
- 1 sistema de quota por dias
- Deuda total de Story Mode con cuotas encadenadas
- Bote comun `TeamMoney`, venta de loot y compras basicas
- 1 sistema de threat
- 1 flujo de extraccion con countdown y aceleracion del tren
- 1 loop completo de victoria y derrota en singleplayer

## Ideas divertidas y corte de alcance

### Si entra antes del freeze

- Aceleracion progresiva del tren al salir.

Motivo: mejora directamente la tension del escape, es legible, barata y no exige un sistema nuevo enorme.

### Puede entrar despues del freeze si el loop base ya es estable

- Plantas rodadoras explosivas que lancen al jugador y activen ragdoll temporal.

Motivo: aporta caos y humor al loop, pero introduce fisicas, knockback, recuperacion y luego sincronizacion online.

### No debe entrar en el MVP base

- Enemigos tipo cactus que solo se mueven cuando no los miras.

Motivo: es una idea buena, pero no es un primer enemigo. Requiere reglas de line-of-sight, feedback claro, casos borde en multijugador y mas coste de red que un bandido basico.

## Fuera de alcance del MVP

- Procedural generation
- Fisica compleja para cada objeto
- Mas de 1 mapa jugable real
- Mas de 1 tipo de enemigo antes del freeze
- Mas de 3 armas antes de polish
- Skill trees, perks o crafting
- Tienda compleja en el tren
- Bosses
- Matchmaking
- Voice chat propio
- Progresion permanente
- Misiones secundarias complejas
- Tren fisicamente simulado
- Infinity Mode y estadisticas de record
- Nuevos hazards, enemigos y contenido fuera del loop base

## Hitos resumidos

- 2026-08-30: loot prototype jugable
- 2026-09-13: tren jugable con viaje fisico MacLarens ↔ pueblo y economia de run completa
- 2026-09-20: singleplayer loop completo con pueblo y loot randomizado
- 2026-09-27: MVP freeze
- 2026-10-04: co-op funcional 1-4 jugadores
- 2026-10-09: build final y entrega

## Fase 0 - 2026-08-21 a 2026-08-24

### Meta

Bloquear direccion, reglas de alcance y la infraestructura minima de interaccion.

### Entregables

- Roadmap, arquitectura y guia de estilo cerrados
- `IInteractable` definido
- `PlayerInteractor` separado del movimiento
- Estructura basica de carpetas de scripts decidida
- Lista cerrada de sistemas MVP y no MVP

### Criterios de salida

- Se puede anadir un nuevo interactuable sin tocar `PlayerController`
- La UI ya puede mostrar un prompt de interaccion generico
- No hay dudas abiertas sobre si el tren sera una escena aparte o una zona segura

### Estado actual

- Fase cerrada. `IInteractable`, `PlayerInteractor` e `InteractUI` estan implementados y documentados.
- La estructura de carpetas por dominio ya existe y la decision de tren como zona segura dentro de la misma escena sigue vigente.

### Si hay retraso

- No se abre ninguna feature de contenido
- Se evita cualquier refactor grande del FPS actual

## Fase 1 - 2026-08-25 a 2026-08-30

### Meta

Demostrar que recoger, transportar, soltar y recuperar loot ya es divertido por si solo.

### Entregables

- `LootDataSO`
- 5 objetos de loot con valor distinto
- `LootItem` interactuable
- `PlayerInventory` de 4 slots
- Drop de loot al mundo
- HUD de inventario y valor transportado
- Escena sandbox o area de prueba para iterar rapido

### Criterios de salida

- El jugador llena inventario, suelta objetos y puede volver a recogerlos
- El valor total transportado es visible y correcto
- No hay casos frecuentes de perder un objeto por colisiones o referencias rotas

### Estado actual

- Los sistemas base de P1 estan implementados: `LootDataSO`, `LootItem`, `PlayerInventory`, `InventoryUI`, `ItemHolder`, `PlayerPoseController`, drop y throw cargado con fisicas.
- El item soltado o lanzado vuelve al mismo `LootItem` del mundo mediante `ItemInstance`, de forma que mantiene referencias y datos runtime como la municion.
- La fase esta implementada a nivel de sistemas y escena de prueba: existen 6 assets de loot authoring con valores distintos. No se marca cerrada al 100% hasta mostrar el valor total transportado en HUD y completar el playtest final de pickup, drop y recuperacion.

### Si hay retraso

- Se deposita por valor, no por representacion fisica en el tren
- Se deja el inventario sin drag and drop; solo pickup y drop

## Fase 2 - 2026-08-31 a 2026-09-13

### Meta

Conectar el loot con la economia de la run y hacer que el tren sea una pieza espacial jugable: el jugador viaja fisicamente entre MacLarens y el pueblo en una unica escena con raíl circular.

### Decision de arquitectura de mundo

Una unica escena principal con dos zonas y un raíl circular que las une:

- **MacLarens** = zona segura, deposito, arranque del dia.
- **Tren** = transicion, conversacion, primera presion de salida.
- **Pueblo** = zona de riesgo, loot, enemigos.

El tren sigue el spline automaticamente; el jugador activa la salida, no conduce. El sistema debe poder extenderse a mas destinos (Town 02, Town 03) sin cambiar el core del spline.

### Entregables (ya implementados)

- `TrainCargo` — trigger de deposito fisico en el vagon; reparenta el loot al vagon mientras esta dentro (bug de reparentado corregido tras el refactor de "single source of truth")
- `QuotaManager` — cuota actual, cargo en transito (`CurrentCargoValue`) y valor entregado (`DeliveredValue`, fuente de verdad de `QuotaMet`), evento de cambio
- `RunManager` — Day 1/2/3, avance y reset
- `TrainDeparture` — interaccion contextual ("Return to MacLarens" / "Depart to Town" segun estacion actual) con countdown de 5s y hook `OnTrainDeparted`/`OnArrived`
- Composicion visual del tren en escena (locomotora + vagones + `TrainCargo`), ya colocada en `MainScene`
- Rail spline circular `MacLarens → Town → MacLarens` usando Unity Splines (`TrainSpline`)
- Estacion MacLarens y estacion Town como marcadores de distancia sobre el spline (`townPosition`/`macLarensPosition` en `TrainSplineFollower`)
- Movimiento automatico del tren siguiendo el spline (`TrainSplineFollower` + `TrainCarFollower`, maquina de estados `IsMoving`/`CurrentStation` equivalente a un `TrainController`)
- Jugadores reparentados al vagon mientras viajan (`TrainPassenger` + `TrainPassengerArea`, reemplaza el enfoque anterior de inyeccion de velocidad)
- Animacion de ruedas (`TrainWheelSpin`)
- UI de cuota, cargo y estado del dia (`QuotaUI` + `CargoValueUI`)
- `LootRegistry` — controla que loot existe activo en la run
- `LootDeliveryPoint` — punto donde depositar loot para que cuente como entregado

### Entregables (pendientes)

- Spawn/posicion explicito del jugador al subir al tren por primera vez
- Playtest end-to-end grabado del criterio de salida completo (todas las piezas existen pero no hay validacion jugable documentada)

### Criterios de salida

- El jugador aparece en MacLarens, entra al tren, viaja fisicamente hasta el pueblo, puede bajarse, recoger loot, volver al tren y regresar a MacLarens a depositar el loot — **implementado a nivel de sistemas, pendiente de playtest de validacion**
- La cuota solo avanza al depositar fisicamente en el vagon, no al recoger loot y solo cuenta como entregado al depositarlo en el LootDeliveryPoint — implementado
- Al alcanzar cuota, el jugador puede decidir seguir saqueando o activar la salida — implementado (`TrainDeparture` no bloquea salida por cuota)
- Todo ocurre en una sola escena sin cambio de escena — implementado (`MainScene`)

### Si hay retraso

- Solo se soporta Day 1 real; Day 2 y Day 3 quedan como datos configurables sin UI especifica
- El tren teleporta al jugador entre zonas si el spline no esta listo; el sistema de economia no depende del movimiento fisico
- La animacion de ruedas queda como entregable post-milestone, no bloquea criterios de salida

## Fase 3 - 2026-09-14 a 2026-09-20


### Meta

Hacer que el pueblo sea divertido de saquear y que el loop completo singleplayer sea jugable de inicio a fin.

En esta fase la conexion tren-pueblo ya existe (resuelta en Fase 2). El foco es exclusivamente la densidad, legibilidad y repetibilidad del pueblo.

### Entregables

- `Town_Western_01` montado con props del POLYGON Western Pack
- 4-6 puntos de interes claros y navegables
- `LootSpawnPoint` — authoring para colocar loot en el mundo (implementado)
- `LootSpawner` — reparto aleatorio ponderado por distancia al iniciar la run (implementado)
- Reposicion parcial de loot entre dias (loot sobrante + nuevos spawns) — implementado: `LootSpawner.Restock()` ahora se conecta a `RunManager.OnDayChanged`
- Primer loop completo singleplayer jugable de inicio a fin sin enemigos

### Criterios de salida

- La misma escena produce partidas ligeramente distintas sin procedural generation real — implementado (distribucion ponderada de `LootSpawner`)
- El jugador encuentra loot valioso en rutas alternativas, no en una sola linea optima — depende de la composicion de `Town_Western_01`, no verificable por codigo
- El pueblo se entiende visualmente y se puede recorrer en menos de 60 segundos — pendiente de playtest
- Una run completa MacLarens → tren → pueblo → loot → deposito → decision → salida funciona sin errores bloqueantes — pendiente de playtest end-to-end

### Si hay retraso

- Se recorta tamano del pueblo antes de recortar legibilidad
- La reposicion de loot entre dias se simplifica a reparto completamente nuevo cada run

## Fase 4 - 2026-09-21 a 2026-09-27

### Meta

Introducir presion dinamica para convertir el transporte de loot en riesgo.

### Entregables

- Enemigos orientados a perseguir jugador en vez de loot — implementado (`EnemyController` persigue y ataca al jugador mas cercano)
- `EnemySpawner` — implementado (cantidad y ritmo de spawn dinamicos por `ThreatLevel`, spawn points validados contra NavMesh, distancia a jugadores, cooldown de reuso y score)
- `Hitbox` — implementado (multiplicador de daño por headshot en `Weapon`)
- `ThreatManager` — implementado (`Assets/_Project/Scripts/Core/ThreatManager.cs`): threat por tiempo constante + pickup unico por item de loot, sin decay por matar enemigos, niveles `ThreatLevel` con 4 umbrales configurables, reset por dia
- Umbrales de amenaza y tablas de spawn simples — implementado: `EnemySpawner.ThreatSpawnSettings[]` mapea cada `ThreatLevel` a `maxAliveEnemies`/`spawnInterval`
- `Board Train`, cuenta atras de salida y aceleracion progresiva — el countdown de salida ya existe en `TrainDeparture` (Fase 2); la aceleracion/deceleracion progresiva del tren (ease-in/ease-out via `accelerationDistance`/`decelerationDistance`) ya esta implementada en `TrainSplineFollower`, ver P4/Docs TODOs en memoria de repo

### Criterios de salida

- La amenaza sube por saquear y no por tiempo puro — el diseno final acordado con el usuario combina ambas fuentes a proposito (tiempo constante + pickup unico de loot); no hay decay por matar enemigos, que era el riesgo real a evitar. Implementado.
- La cantidad de enemigos escala de forma legible — implementado: `EnemySpawner` lee `maxAliveEnemies`/`spawnInterval` en vivo segun `ThreatManager.CurrentLevel`
- Volver al tren se siente como extraccion, no como teletransporte gratuito — sistemas ya en su sitio (threat, spawn dinamico, spawn points con distancia/cooldown/score); confirmacion final pendiente de playtest

### Si hay retraso

- Un solo tipo de enemigo
- Umbrales fijos de amenaza
- Sin comportamiento avanzado de patrulla

## Fase 5 - 2026-09-28 a 2026-10-01

### Meta

Cerrar el vertical slice singleplayer del modo historia y congelar alcance.

### Entregables

- Milestone interno P5.1: `Menu -> Run -> MacLarens -> Town -> MacLarens -> Finish Day -> Next Day/Success/Fail`, inicialmente validable con botones o datos temporales
- Loop completo de partida
- Estados de juego minimos: menu, run, success, fail
- `GameStateManager` separado de `RunManager`, con solo `Menu`, `Run`, `Success` y `Fail`
- `RunManager` con fases funcionales de run, cierre de dia y transiciones sin softlocks
- `MoneyManager` con `TeamMoney`, gasto y venta de loot
- `QuotaManager` con deuda total, deuda restante, cuota efectiva, modificadores, deuda pagada y dias restantes
- MacLarens: venta, compras, preparacion y boton `Finish Day`
- Fog de salida y limpieza de entidades del pueblo
- Cuerpo recuperable, transporte al tren, revive y penalizacion por abandono
- Game over por muerte, wipe o cuota fallida
- Story Mode completo hasta `Success`
- Reinicio fiable recargando la escena de gameplay y creando una run nueva
- HUD de deuda, dinero, dia, cuota y `Cargo Value` separado
- Owner de MacLarens como NPC interactuable con feedback narrativo minimo
- Infinity Mode documentado como post-MVP, sin implementarlo
- Build interna estable

### Criterios de salida

- Una persona puede empezar, preparar, saquear, extraer, vender, comprar y cerrar todos los dias de una run
- La cuota se comprueba solo al pulsar `Finish Day`, despues de vender y antes de avanzar
- La entrega procesa `LootItem` fisicos individualmente; no convierte `CargoValue` como total agregado
- El dinero comun, el `CargoValue`, el `DeliveredValue` y la deuda pagada son valores distintos y visibles donde corresponde
- Una penalizacion de abandono se refleja como modificador de cuota visible y auditable
- Team wipe provoca `Fail` inmediatamente; la cuota fallida solo provoca `Fail` al cerrar el ultimo dia
- `Success` se alcanza unicamente al pagar la deuda total
- Reiniciar elimina el estado de la run anterior sin dejar loot, enemigos, tren, threat o dinero residual
- No hay errores bloqueantes ni reglas esenciales sin implementar
- Toda feature abierta pero no estable se corta en lugar de arrastrarse

### Regla de freeze

Desde este punto no entran features nuevas de sistema. Solo correcciones, UX, balance y estabilidad.

## Fase 6 - 2026-10-02 a 2026-10-08

### Meta

Escalar el loop ya validado a cooperativo 1-4 con NGO + Relay.

### Entregables

- Instalar y configurar Authentication + Relay
- Host y Join por codigo Relay
- Jugador de red sincronizado
- Inventario autoritativo en servidor
- Loot, enemigos, threat, cuota y tren autoritativos en servidor
- Final de run sincronizado para todos

### Criterios de salida

- 2 jugadores pueden completar la run sin desincronizaciones graves
- 4 jugadores pueden entrar, lootear, depositar y salir
- El servidor decide existencia de loot, AI, daño y cuota

### Si hay retraso

- Se optimiza para Host + 1 primero
- Se recortan animaciones o detalles visuales antes de tocar autoridad de servidor

## Fase 7 - Post-MVP

### Meta

Mejorar game feel sin abrir sistemas nuevos grandes.

### Entregables

- Shotgun o rifle
- Un segundo enemigo solo si el primero ya esta estable
- 10-12 objetos de loot totales
- Ajustes de recoil, hit feedback y ritmo de combate

### Regla

Si el loop base todavia tiene bugs serios, esta fase se convierte en fase de estabilizacion.

## Fase 8 - Post-MVP / entrega

### Meta

Pulido final, QA, balance, audio y VFX minimos.

### Entregables

- Audio funcional de armas, loot, enemigos y tren
- VFX minimos legibles
- UI final ligera
- Performance pass
- Build candidata a entrega
- Plantas rodadoras explosivas solo si no compiten con estabilidad

### Regla

No se acepta ninguna feature nueva, por buena que parezca.

## 2026-10-09 - Ship Day

### Objetivo

El dia 9 no es dia de desarrollo. Es dia de build final, smoke test y entrega.

## Cadencia semanal recomendada

- Lunes: cerrar objetivo semanal y maximo 3 tareas principales
- Miercoles: checkpoint jugable aunque sea feo
- Viernes o sabado: build interna + playtest de 15-20 minutos
- Despues del playtest: cortar alcance antes de abrir nuevas tareas

## Matriz de recorte

- Si Fase 1 falla: no se abre economy avanzada; se estabiliza loot hasta que sea divertido
- Si Fase 2 falla: multiplayer se mueve completamente despues del deadline
- Si Fase 3 falla: se reduce mapa, no se amplia
- Si Fase 4 falla: se mantiene un solo enemigo y una sola curva de amenaza
- Si Fase 5 falla: todo el proyecto entra en modo bugfix hasta cerrar una run completa

## Riesgos principales y mitigacion

### Riesgo: scope creep

Mitigacion: cualquier idea nueva debe demostrar mejora directa del core loop antes de aceptarse.

### Riesgo: querer network-first demasiado pronto

Mitigacion: validar el loop offline primero y separar request/validate/apply desde el principio.

### Riesgo: pueblo demasiado grande o vacio

Mitigacion: priorizar densidad, landmarks y rutas cortas sobre metros cuadrados.

### Riesgo: AI inestable

Mitigacion: usar un solo comportamiento fuerte y legible antes de intentar variedad.

### Riesgo: UI pobre para decisiones de riesgo

Mitigacion: asegurar desde temprano HUD de quota, loot e indice de amenaza.

## Definicion de MVP completo

El MVP esta completo cuando cumple estas condiciones al mismo tiempo:

- El bucle principal se juega de principio a fin
- La cuota exige depositar y despues decidir si arriesgar mas o irse
- El jugador entiende cuanto loot lleva, cuanto falta y cuanto peligro hay
- Morir o fallar la cuota corta la run con claridad
- El proyecto se puede enseñar sin explicar sistemas rotos o placeholders criticos