# MacLarens Defense - MVP Backlog

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
6. Cerrar failure states, day loop y build singleplayer estable.
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

- `[Loot] Crear LootItem interactuable`
  Resultado: representacion en mundo que usa `LootDataSO`.
  Aceptacion: al interactuar intenta entrar en inventario y desaparece del mundo si entra.
  Nota de diseño: los objetos pesados, como la caja fuerte, iniciaran transporte visible en mano y bloquearan sprint y arma; ver [Arquitectura MVP](Arquitectura_MVP.md).

### Inventario y drop

- `[Inventory] Crear PlayerInventory de 4 slots`
  Resultado: add, remove, has space, total value.
  Aceptacion: no permite sobrellenar, informa valor correcto y expone evento de cambio.

- `[Inventory] Implementar drop al mundo`
  Resultado: el jugador puede soltar loot y recuperarlo despues.
  Aceptacion: el item vuelve a existir en escena con su dato correcto y sin duplicarse.

- `[UI] Crear InventoryUI minima`
  Resultado: slots y valor transportado visibles.
  Aceptacion: el HUD se refresca en pickup y drop sin intervencion manual.

- `[Scene] Crear Sandbox_Loot`
  Resultado: espacio de prueba para pickup, drop y edge cases.
  Aceptacion: permite validar todo el milestone 1 en menos de 2 minutos.

## P2 - Train, quota y economia

- `[Train] Crear TrainCargo`
  Resultado: punto de deposito conectado al inventario.
  Aceptacion: transfiere valor desde el jugador a quota sin contar doble.

- `[Core] Crear QuotaManager`
  Resultado: quota actual, cargo actual y evento de cambio.
  Aceptacion: puede responder si la cuota ya fue alcanzada.

- `[Core] Crear RunManager`
  Resultado: dia actual, resultado de run y reset de flujo.
  Aceptacion: Day 1, Day 2 y Day 3 se configuran sin hardcode disperso.

- `[UI] Crear QuotaUI`
  Resultado: mostrar cuota, cargo y estado de salida.
  Aceptacion: el jugador sabe cuanto falta y si ya puede irse.

- `[Train] Crear accion Return to MacLarens`
  Resultado: la salida se puede activar solo cuando procede.
  Aceptacion: cuota alcanzada desbloquea opcion; no fuerza salida automatica.

## P3 - Pueblo y spawns

- `[World] Montar Town_Western_01`
  Resultado: pueblo pequeno, legible y denso.
  Aceptacion: el jugador encuentra 4-6 puntos de interes claros en una vuelta corta.

- `[Loot] Crear LootSpawnPoint`
  Resultado: puntos authoring para colocar botin.
  Aceptacion: el spawner puede rellenar la escena sin referencias manuales una a una.

- `[Loot] Crear LootSpawner`
  Resultado: reparto aleatorio simple por partida.
  Aceptacion: reiniciar run cambia parte del reparto de loot.

- `[Flow] Integrar tren y pueblo en una misma escena jugable`
  Resultado: extraccion clara sin complejidad de scene flow.
  Aceptacion: se puede salir del tren, saquear y volver sin cargar otra escena.

## P4 - Threat y enemigos

- `[Enemy] Redirigir EnemyController hacia jugador`
  Resultado: el enemigo persigue jugador en vez de booze.
  Aceptacion: detecta al jugador y aplica presion funcional.

- `[Enemy] Crear EnemySpawner`
  Resultado: spawns controlados por presupuesto de amenaza.
  Aceptacion: los enemigos aparecen en puntos validos y no saturan el mapa sin control.

- `[Core] Crear ThreatManager`
  Resultado: scalar de amenaza y thresholds simples.
  Aceptacion: recoger loot aumenta threat y el HUD se actualiza.

- `[UI] Crear ThreatUI`
  Resultado: lectura clara del nivel de peligro.
  Aceptacion: el jugador entiende cuando se esta sobreexponiendo.

- `[Train] Implementar Board Train y countdown`
  Resultado: extraccion legible y con tension.
  Aceptacion: volver al tren no finaliza al instante; hay una ventana de riesgo corta.

- `[Train] Implementar aceleracion progresiva de salida`
  Resultado: el tren ofrece una ultima oportunidad corta para subirse.
  Aceptacion: existe una ventana de riesgo legible en la que llegar tarde aun puede salvar la run.

## P5 - MVP freeze

- `[Core] Cerrar estados de juego minimos`
  Resultado: menu, run, success, fail.
  Aceptacion: cada estado tiene entrada, salida y feedback visual claro.

- `[Fail] Implementar game over por muerte, wipe y cuota fallida`
  Resultado: las derrotas quedan claras y reinician correctamente.
  Aceptacion: no hay softlocks ni estados ambiguos tras fallar.

- `[Balance] Ajustar economia y threat del Day 1`
  Resultado: una run normal genera decision real de seguir o escapar.
  Aceptacion: un playtest produce al menos una decision de riesgo interesante.

- `[Build] Generar build interna estable`
  Resultado: vertical slice portable y demostrable.
  Aceptacion: se puede jugar de inicio a fin sin usar el editor para arreglar nada.

## P6 - Multiplayer despues del freeze

- `[Network] Instalar Authentication + Relay`
  Resultado: base de servicios para host y join por codigo.
  Aceptacion: el proyecto puede crear o unirse a una sesion Relay sin pasos manuales fuera del flujo previsto.

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

## P7 - Polish posterior

- `[Combat] Anadir shotgun o rifle`
- `[Enemy] Anadir segundo enemigo solo si el primero ya es estable`
- `[Loot] Ampliar a 10-12 objetos`
- `[Hazard] Añadir plantas rodadoras explosivas si la build ya es estable`
- `[Enemy] Prototipar cactus observador solo despues de cerrar hazards simples`
- `[Audio] Sonidos de armas, loot, enemigos y tren`
- `[VFX] Muzzle flash, hit, blood y warning de threat`
- `[UI] Refinar HUD final`

## Reglas de prioridad

- Primero se cierran sistemas del core loop.
- Luego se cierran UX minima y failure states.
- Multiplayer no adelanta trabajo de singleplayer si el loop aun no esta probado.
- Contenido extra solo entra cuando el sistema base ya es estable.