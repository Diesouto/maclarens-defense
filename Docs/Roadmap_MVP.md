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

## Hitos resumidos

- 2026-08-30: loot prototype jugable
- 2026-09-13: singleplayer loop completo
- 2026-09-25: MVP freeze
- 2026-10-02: co-op funcional 1-4 jugadores
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

## Fase 2 - 2026-08-31 a 2026-09-06

### Meta

Conectar el loot con la economia de la run.

### Entregables

- `TrainCargo`
- `QuotaManager`
- `RunManager` con Day 1, Day 2 y Day 3
- UI de cuota, cargo y estado del dia
- Interaccion de deposito en el tren
- Opcion de finalizar viaje cuando la cuota ya es alcanzable

### Criterios de salida

- Se puede completar una run sin enemigos
- La cuota no se completa por recoger loot; solo por depositarlo
- Al llegar a cuota, el jugador puede decidir si seguir saqueando o volver

### Si hay retraso

- Solo se soporta Day 1 real; Day 2 y Day 3 quedan como datos configurables
- El fin de run usa transicion simple en lugar de secuencia elaborada

## Fase 3 - 2026-09-07 a 2026-09-13

### Meta

Construir el primer pueblo y volver repetible la exploracion.

### Entregables

- Un pueblo pequeno y denso con 4-6 puntos de interes claros
- `LootSpawnPoint`
- `LootSpawner`
- Rotacion aleatoria simple de loot por partida
- Conexion clara tren <-> pueblo dentro del mismo espacio jugable

### Criterios de salida

- La misma escena puede producir partidas ligeramente distintas sin procedural generation real
- El jugador encuentra loot valioso en rutas alternativas, no en una sola linea optima
- El pueblo se entiende visualmente rapido y se puede recorrer en menos de 60 segundos

### Si hay retraso

- Se recorta tamano del pueblo antes de recortar legibilidad
- Se usa una sola escena jugable con el tren como zona segura fija

## Fase 4 - 2026-09-14 a 2026-09-20

### Meta

Introducir presion dinamica para convertir el transporte de loot en riesgo.

### Entregables

- Enemigos orientados a perseguir jugador en vez de loot
- `EnemySpawner`
- `ThreatManager`
- Umbrales de amenaza y tablas de spawn simples
- `Board Train`, cuenta atras de salida y aceleracion progresiva

### Criterios de salida

- La amenaza sube por saquear y no por tiempo puro
- La cantidad de enemigos escala de forma legible
- Volver al tren se siente como extraccion, no como teletransporte gratuito

### Si hay retraso

- Un solo tipo de enemigo
- Umbrales fijos de amenaza
- Sin comportamiento avanzado de patrulla

## Fase 5 - 2026-09-21 a 2026-09-25

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