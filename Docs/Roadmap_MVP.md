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

## Vision extendida del loop (post-MVP)

Esta seccion registra la direccion final del juego (deuda, modos de juego, fog y recuperacion de cuerpos) tal y como se ha discutido. **No forma parte del freeze de MVP singleplayer** (Fase 0-5); se retoma cuando el loop base ya este validado y estable, mayormente en o despues de la Fase 6 (coop). El proposito de documentarla ahora es no perder la idea y no bloquear el trabajo diario con decisiones de diseno a largo plazo.

### Story Mode (modo principal)

- Los jugadores parten con una deuda total con MacLarens (ej. $10.000) y un numero de dias limite para pagarla.
- Cada expedicion resuelve una cuota parcial; completarla genera una cuota nueva y mayor.
- Si se agotan los dias sin pagar la deuda completa, la run termina y se reinicia desde el Day 1.
- Si la cuota de una expedicion no se completa pero quedan dias, la cuota **persiste** en vez de resetearse. Esto difiere de como se comporta hoy `RunManager.AdvanceDay()` (sustituye la cuota entera por la siguiente de una tabla fija `dayQuotas`, sin arrastrar `DeliveredValue`). Decision pendiente para cuando se aborde esta fase: si `DeliveredValue` debe acumularse entre dias no completados y como se refleja en `RunManager`/`QuotaManager`.

### Infinity Mode (modo secundario)

- Sin deuda total ni fecha limite: el juego encadena cuotas cada vez mayores hasta que el equipo falla una cuota o el equipo entero muere.
- Requiere tracking de record (dinero total, cuotas completadas, dias sobrevividos): un sistema de meta-progresion nuevo que no existe hoy.
- Se construye despues de que Story Mode este completo y estable; no se reparte esfuerzo entre ambos modos antes del freeze.

### El fog y la extraccion forzada

- Al salir el tren del pueblo, este queda sellado por niebla: todo lo que quede dentro (jugadores que no llegaron al tren, cuerpos, enemigos, loot no entregado) se destruye.
- Hoy no existe ninguna consecuencia si un jugador se queda atras al partir el tren; es una laguna real frente al pilar "la extraccion importa", pero el fog completo (VFX + despawn + logica de jugador abandonado) es contenido de Fase 6 (coop): en singleplayer con 1 jugador "quedarse atras" no tiene la misma lectura (no hay equipo que decida abandonarlo).
- Version minima que si podria entrar antes del freeze si sobra tiempo: limpiar/despawnear el loot no entregado del pueblo al salir el tren. Refuerza "solo cuenta lo entregado" sin necesitar logica de jugadores ni cuerpos.

### Recuperacion de cuerpos y penalizacion por abandono

- Si un jugador muere en el pueblo, su cuerpo queda fisicamente en el mundo; el equipo puede cargarlo hasta el tren para revivirlo al llegar a MacLarens, o abandonarlo (revive igualmente en MacLarens, pero con penalizacion de cuota).
- Es un sistema intrinsecamente cooperativo (recuperar a un companero); no aplica de forma significativa en singleplayer. Se implementa en Fase 6 junto al resto de sistemas de coop, no antes.
- Punto de partida cuando se aborde: hoy `Health.Die()` no tiene ningun estado de "cuerpo recuperable" (solo dispara ragdoll); `PlayerController.OnDeath()` simplemente desactiva el control del jugador.

### Arquitectura objetivo (para cuando se retome)

El flujo de partida deberia converger en un unico estado autoritativo en vez de que cada sistema decida por su cuenta cuando empieza o termina un dia:

```text
Run
 ├── CurrentMode (Story / Infinity)
 ├── CurrentDay / DaysRemaining
 ├── CurrentQuota / DeliveredValue
 ├── RunState: AtMacLarens -> TravelingToTown -> InTown -> LeavingTown
 │             -> ReturningToMacLarens -> ResolvingDay -> QuotaCompleted / RunFailed
 └── Player state: Alive / Dead / BodyOnTrain / Respawned
```

`QuotaManager`, `TrainDeparture`, `TrainSplineFollower`, `LootDeliveryPoint`, `EnemySpawner` y el futuro sistema de muerte/revive deberian leer y alimentar este estado en vez de tomar decisiones de flujo por su cuenta. Esto no implica reescribir esos sistemas ahora: es la direccion a seguir la proxima vez que se toque `RunManager` a fondo, para no rehacer el flujo dos veces.

## Objetivo online realista

El objetivo tecnico online no debe definirse como P2P full mesh.

Para este proyecto, lo realista con Unity 6 es esto:

- `Host-authoritative listen server`
- NGO para sincronizacion
- Relay para conectividad
- Authentication para bootstrap de servicios

En terminos de producto puede describirse como cooperativo online con host, pero el codigo debe asumir una unica fuente de verdad para loot, threat, enemigos, daño, cuota y salida del tren.

## Definicion de MVP

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
    -> Reach quota or fail the day
    -> Start next day or game over
```

## Lo que si entra exactamente en el MVP freeze

- 1 escena jugable principal con tren + pueblo
- 1 sandbox de loot y 1 sandbox de combate
- 1 arma estable
- 1 enemigo base estable
- 5 objetos de loot minimos, con objetivo de 8 antes del freeze
- 4 slots de inventario
- 1 sistema de quota por dias
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
- Deuda total de partida y separacion Story Mode / Infinity Mode (ver "Vision extendida del loop")
- Fog que despawnea el pueblo al salir el tren y consecuencias de jugador abandonado (Fase 6 coop)
- Recuperacion y revivir cuerpos de jugador muerto (Fase 6 coop)

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

Cerrar el vertical slice singleplayer y congelar alcance.

### Entregables

- Loop completo de partida
- Estados de juego minimos: menu, run, success, fail
- Game over por muerte, wipe o cuota fallida
- Progresion de dias simple
- Build interna estable

### Criterios de salida

- Una persona puede empezar, completar y perder una run completa sin ayuda externa
- No hay errores bloqueantes ni reglas esenciales sin implementar
- Toda feature abierta pero no estable se corta en lugar de arrastrarse

### Regla de freeze

Desde este punto no entran features nuevas de sistema. Solo correcciones, UX, balance y estabilidad.

## Fase 6 - 2026-09-26 a 2026-10-02

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

## Fase 7 - 2026-10-03 a 2026-10-05

### Meta

Mejorar game feel sin abrir sistemas nuevos grandes.

### Entregables

- Shotgun o rifle
- Un segundo enemigo solo si el primero ya esta estable
- 10-12 objetos de loot totales
- Ajustes de recoil, hit feedback y ritmo de combate

### Regla

Si el loop base todavia tiene bugs serios, esta fase se convierte en fase de estabilizacion.

## Fase 8 - 2026-10-06 a 2026-10-08

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