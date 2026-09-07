# MacLarens Defense - MVP Scope Matrix

## Objetivo de este documento

Cerrar el alcance de la forma mas dura posible para evitar que buenas ideas destruyan el calendario.

## Modelo online objetivo

Aunque a nivel de producto pueda describirse como "P2P con los sistemas online de Unity", la implementacion recomendada es esta:

- 1 host hace de servidor de la partida
- 1-3 clientes se conectan a ese host
- Relay resuelve conectividad
- Authentication inicializa servicios
- NGO sincroniza objetos y eventos

Conclusión practica:

> El juego debe pensarse como cooperativo host-authoritative, no como full mesh P2P.

## Corte exacto del MVP

### Obligatorio para el freeze singleplayer

- 1 pueblo pequeno y legible
- 1 tren como zona segura y punto de extraccion
- 1 enemigo base funcional
- 1 arma funcional
- 5-8 objetos de loot reutilizando el pack western
- Inventario de 4 slots
- Pickup, drop y deposit
- Quota por dias
- Threat por acciones de loot
- Countdown de salida
- Aceleracion progresiva del tren
- Victoria, derrota y siguiente dia

### Obligatorio para multiplayer base

- Host / Join por codigo Relay
- Inventario autoritativo en host
- Loot autoritativo en host
- Enemy AI y threat autoritativos en host
- Quota y salida del tren sincronizadas

### No obligatorio para el MVP

- Segundo pueblo
- Segundo enemigo
- Segunda y tercera arma
- Hazards fisicas complejas
- Sistema de shop
- Progression permanente
- Voice chat
- Deuda total de partida y modos Story / Infinity separados (ver `Roadmap_MVP.md` > Vision extendida del loop)
- Fog que despawnea el pueblo al salir el tren y logica de jugador abandonado
- Recuperacion y revivir cuerpos de jugador muerto

## Clasificacion de ideas por impacto

| Idea | Impacto en core loop | Coste tecnico | Coste de red | Decision |
| --- | --- | --- | --- | --- |
| Tren que tarda en acelerar | Alto | Bajo | Bajo | Entra antes del freeze |
| Plantas rodadoras explosivas | Medio-Alto | Medio | Medio | Solo despues del freeze si la build es estable |
| Enemigo cactus que se mueve si no lo miras | Alto | Alto | Alto | No entra en MVP base |
| Fisicas complejas para mucho loot | Bajo-Medio | Alto | Alto | Fuera del MVP |

## Presupuesto de contenido del MVP

### Entorno

- Fuente principal: `POLYGON - Western Pack`
- Regla: construir el primer pueblo casi entero con ese pack antes de importar nada mas.

### Props de loot

- Prioridad 1: reutilizar props del western pack.
- Prioridad 2: si faltan siluetas utiles, usar Kenney o pack gratuito low poly solo para tapar huecos claros.
- Regla: no mezclar mas de 2 familias visuales grandes antes del freeze.

### Personajes y animacion

- Fuente base: Mixamo para locomotion, hit, death y recovery.
- Regla: bloquear un set pequeno y consistente temprano.
- Regla: no cambiar de rig o de avatar base a mitad del MVP salvo bloqueo real.

## Orden de implementacion recomendado

### Paso 1

Interaccion base y prompt.

### Paso 2

Inventario, loot, pickup y drop.

### Paso 3

Deposito en tren, quota y decision de escapar.

### Paso 4

Montaje del pueblo, spawn de loot y lectura del espacio.

### Paso 5

Threat, enemigo base, damage pressure y extraccion con countdown.

### Paso 6

Loop completo de dia, fail states y build singleplayer estable.

### Paso 7

Migracion controlada a host-authoritative multiplayer.

### Paso 8

Solo entonces se estudian hazards fisicas, segundo enemigo y contenido extra.

## Reglas de recorte duras

- Si el loot loop no es divertido, no se pasa a content spam.
- Si la quota no genera decision, no se pasa a multiplayer.
- Si el enemigo base no funciona, no se abre el cactus observador.
- Si la build en host + 1 no es estable, no se amplian hazards fisicas.

## Lista cerrada de "si pero mas tarde"

- Cactus observador
- Hazards fisicas encadenadas
- Shop en el tren
- Segundo pueblo
- Objetos especiales raros
- Eventos aleatorios complejos