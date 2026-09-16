# Auditoria multiplayer P6

Fecha: 2026-09-16
Alcance: `Assets/_Project/Scripts/Networking` y sus integraciones con Player, Loot, Train, Enemy, Core, World e Interaction.
Metodo: revision estatica de codigo; no se ha ejecutado Unity Play Mode ni una sesion Relay.

## Veredicto ejecutivo

**Estado: NO listo para Play Mode ni para declarar P6 cerrado.**

La arquitectura elegida es razonable: host-authoritative, intents por `ServerRpc` y estado publico por
`NetworkVariable`/`NetworkList`. Sin embargo, la implementacion actual contiene varios puntos donde un
cliente puede provocar una mutacion global sin validacion suficiente, y varios componentes actualizan
estado networkado cada frame aunque no haya cambiado.

La auditoria confirma que el core loop tiene una ruta de autoridad definida, pero no que todos los
sistemas esten correctamente multiplayer en runtime. La prueba determinante sigue bloqueada por falta
de configuracion de Editor, prefabs registrados, Relay/Authentication y Play Mode.

## Hallazgos criticos

### C1. Daño de armas no se valida en el host

Archivos:
- `Assets/_Project/Scripts/Networking/NetworkHealth.cs`
- `Assets/_Project/Scripts/Networking/NetworkWeaponAuthority.cs`
- `Assets/_Project/Scripts/Player/Weapon.cs`

`NetworkHealth.RequestDamageServerRpc` acepta dano para cualquier objetivo server-owned cuando el RPC
proviene de cualquier cliente. La condicion permite `OwnerClientId == NetworkManager.ServerClientId`,
pero no valida que el cliente haya disparado, que el objetivo este dentro de alcance, que la direccion
coincida con una linea de vision o que el dano corresponda al arma equipada.

Ademas, `NetworkWeaponAuthority` solo envia fire/reload; el host ejecuta el raycast usando la instancia
server-side de `Weapon`. No existe un contrato explicito de origen de mira ni una validacion de cadencia
por request. Un cliente puede pedir dano repetidamente a un enemigo server-owned.

Impacto: cheat de dano, muertes imposibles y resultado divergente respecto de la simulacion visual local.

Accion requerida: crear `RequestFireServerRpc` con arma/instancia valida, cadencia, municion, origen y
direccion acotados; el host debe resolver el raycast y aplicar dano. `NetworkHealth` no debe ser una
puerta generica para dano arbitrario de clientes.

### C2. RPC de salida del tren sin validacion de contexto

Archivo: `Assets/_Project/Scripts/Networking/NetworkTrainState.cs`

`RequestDepartureServerRpc` tiene `RequireOwnership = false` y `BeginDeparture()` solo comprueba que
el tren no este ya saliendo. No valida fase, distancia del jugador al punto de interaccion, que el
solicitante este vivo ni que exista una interaccion valida.

Impacto: cualquier cliente puede iniciar la salida desde cualquier lugar o fase enviando el RPC.

Accion requerida: pasar el `clientId` al validador, resolver su `PlayerObject`, validar distancia,
estado vivo, fase y estacion; solo despues iniciar la salida.

### C3. RPC de compra evita la validacion de fase

Archivos:
- `Assets/_Project/Scripts/Networking/NetworkPurchaseAuthority.cs`
- `Assets/_Project/Scripts/World/ShopStand.cs`

El camino local usa `ShopStand.CanInteract`, que valida `RunPhase.MacLarens`/`ResolvingDay`.
El RPC llama directamente a `ShopStand.TryPurchase`, y `TryPurchase` solo valida inventario y dinero.
Por tanto, un cliente puede comprar fuera de la fase permitida si esta dentro de distancia.

Accion requerida: mover la validacion de fase y estado de run a `TryPurchase` o a un validador comun que
usen tanto la ruta local como la RPC.

### C4. Validacion de movimiento no cubre a clientes remotos en el servidor

Archivo: `Assets/_Project/Scripts/Networking/NetworkPlayer.cs`

La validacion de desplazamiento exige `IsServer && IsOwner`. En el servidor, el jugador de un cliente
remoto normalmente no es owner del servidor, por lo que ese camino no valida su posicion. `NetworkTransform`
puede seguir aceptando transform owner-authoritative sin una simulacion server-side de input.

Impacto: teleport, velocidad imposible y desync pueden entrar por el transform de un cliente remoto.

Accion requerida: elegir una politica explicita: input RPC + movimiento simulado por host, o una validacion
server-side del snapshot recibido con baseline, velocidad maxima, tolerancia de train y rollback/strike.

### C5. Despawn directo de NetworkObjects

Archivos:
- `Assets/_Project/Scripts/Enemy/EnemyController.cs`
- `Assets/_Project/Scripts/Enemy/EnemySpawner.cs`
- `Assets/_Project/Scripts/World/TownExtractionResolver.cs`
- `Assets/_Project/Scripts/Networking/NetworkPlayerSpawner.cs`

Hay rutas que llaman `Destroy(gameObject)` sobre objetos que pueden tener `NetworkObject`: muerte de
enemigos, limpieza de Town, destruccion de jugadores desconectados y fallback de spawns. Otras rutas usan
`NetworkObject.Despawn`, por lo que el ciclo de vida no es consistente.

Impacto: warnings de NGO, objetos fantasma, callbacks incompletos y estados distintos entre peers.

Accion requerida: centralizar `DespawnNetworkObjectOrDestroy` con una regla unica: si esta spawned y el
host es autoridad, `Despawn`; solo usar `Destroy` para objetos no networkados o durante shutdown controlado.

## Hallazgos altos

### H1. Churn de red por sincronizacion cada frame

Archivos:
- `NetworkTrainState.cs`
- `NetworkCargoState.cs`
- `NetworkEconomyState.cs`
- `NetworkThreatState.cs`
- `NetworkGameState.cs`
- `NetworkRunState.cs`

Varios componentes escriben `NetworkVariable` o reconstruyen `NetworkList` en cada `Update`/`LateUpdate`.
En particular `NetworkCargoState.SyncFromCargo()` hace `Clear()` y vuelve a agregar todos los ids cada
frame, aunque el cargo no haya cambiado.

Impacto: trafico innecesario, serializacion repetida, callbacks de UI excesivos y riesgo de alcanzar
limites de ancho de banda antes de probar 4 jugadores.

Accion requerida: sincronizar solo al cambiar el valor, con dirty flags/eventos. Para cargo, usar eventos
`ItemAdded`/`ItemRemoved`; para snapshots economicos, escuchar eventos de managers; para tren, publicar
solo cuando cambien umbrales o a una frecuencia limitada.

### H2. Estado de inventario incompleto para presentacion y autoridad

Archivo: `Assets/_Project/Scripts/Networking/NetworkInventoryState.cs`

Solo replica slots de mochila y `TotalValue`. No replica item en mano, slot seleccionado, ammo por
`ItemInstance`, `ItemId` estable ni referencia al objeto world-item. Los clientes pueden recibir una
imagen distinta del objeto que el jugador sostiene y el host no tiene un protocolo de confirmacion para
seleccion/drop.

Accion requerida: definir un `NetworkInventoryEntry` estable con `ItemId`, instancia, ammo y estado de
mano; separar estado autoritativo de la presentacion `ItemHolder`.

### H3. Estado de cargo no sincroniza pose ni salida de items

Archivo: `Assets/_Project/Scripts/Networking/NetworkCargoState.cs`

El cliente solo recibe ids y ejecuta `SetParent(transform, true)`. No recibe offset local, rotacion,
estado de rigidbody/collider ni una orden clara de desparentado cuando un item sale del cargo.

Impacto: loot flotando, cayendo o permaneciendo visualmente en el tren aunque el host ya lo haya retirado.

Accion requerida: representar cada entrada como id + offset/rotacion + estado de cargo, o usar una
entidad cargo networkada que controle pose y fisica solo en host.

### H4. Host disconnect y handoff no estan definidos

Archivo: `Assets/_Project/Scripts/Networking/NetworkSessionManager.cs`

`RemovePlayer` puede escoger otro `HostClientId`, pero NGO no transfiere automaticamente la autoridad
server a ese cliente. Durante `InProgress`, el resto de clientes no puede continuar solo cambiando una
variable. `OnSessionClosed` tampoco implementa retorno al menu.

Accion requerida: para MVP, cerrar la sesion y llevar a todos al menu al perder el host. No simular
host migration con un cambio de `HostClientId` que no cambia la autoridad real.

### H5. Desconectado vivo sin estado de abandono jugable

Archivos:
- `NetworkPlayerSpawner.cs`
- `NetworkPlayer.cs`
- `BodyRecoveryManager.cs`

Se marca `IsAbandoned`, y los cuerpos muertos pueden marcarse perdidos, pero un jugador vivo que se
desconecta no tiene una transicion completa a cuerpo, estado inerte, penalizacion o exclusión del team wipe.

Accion requerida: fijar una politica de MVP: eliminarlo del roster activo y no contar su cuerpo como
muerto, o convertirlo explicitamente en abandono penalizado. Implementar esa politica en host y cubrirla
con pruebas.

### H6. Spawns dinamicos dependen silenciosamente del prefab

Archivos:
- `LootSpawner.cs`
- `LootItem.cs`
- `EnemySpawner.cs`

Los spawners hacen `Instantiate` y solo llaman `Spawn` si el objeto ya tiene `NetworkObject`. Si falta el
componente, el host crea un objeto que los clientes nunca reciben, sin fallo duro que detenga la partida.

Accion requerida: validar prefabs al arrancar, rechazar spawn networkado sin `NetworkObject` y registrar
un error accionable. No añadir componentes de red a runtime como sustituto del registro en
`NetworkManager.NetworkPrefabs`.

### H7. Validacion de interacción inconsistente entre cliente y host

Archivos:
- `NetworkLootItem.cs`
- `NetworkLootDelivery.cs`
- `NetworkBodyCarrier.cs`
- `NetworkPurchaseAuthority.cs`

Las RPCs validan parte de distancia/ownership, pero no existe un validador común de interacción. Cada
sistema decide de forma distinta si el jugador esta vivo, en fase correcta, mirando al objeto o si el
objeto sigue disponible.

Accion requerida: crear una política común de `InteractionValidation` para sender, player object,
distancia, fase, vida y estado del objetivo. El host debe repetir siempre la validacion; nunca confiar en
`CanInteract` ejecutado en el cliente.

## Hallazgos medios

### M1. Managers singleton mezclan estado local y networkado

`MoneyManager`, `QuotaManager`, `RunManager`, `GameStateManager` y `LootRegistry` siguen siendo
`MonoBehaviour` con singleton. Sus snapshots networkados no sustituyen completamente la fuente de verdad.
Esto facilita que UI, suscripciones y scripts de escena lean valores locales durante la inicializacion,
antes de `OnNetworkSpawn`, o en el cliente después de una transición.

Accion: documentar una única ruta de lectura para UI y una única ruta de mutacion para host; evitar que
los clientes llamen métodos de mutacion aunque el guard actual los bloquee silenciosamente.

### M2. Suscripciones e inicializacion dependen de orden de escena

Hay referencias resueltas con `FindFirstObjectByType` en `Awake` y suscripciones en `OnEnable`. En una
escena networkada, el orden de `Awake`, `OnNetworkSpawn` y el registro de managers puede dejar referencias
null o snapshots iniciales incorrectos.

Accion: inyectar referencias serializadas donde sean estables, comprobar dependencias en `OnNetworkSpawn`
y fallar con mensajes claros si faltan.

### M3. IA y transform de enemigos requieren configuración coherente

`NetworkEnemyState` desactiva AI cliente, pero los cambios de transform, animacion, ragdoll y muerte
visual dependen de `NetworkTransform` y del prefab. `EnemyController` usa `Destroy(gameObject, 30f)`
tras muerte, que debe sustituirse por despawn host-authoritative para enemigos networkados.

### M4. RPCs globales no tienen respuesta de rechazo

Selección, drop, compra, salida, finish day y daño se rechazan silenciosamente. Esto es funcional para
el primer prototipo, pero produce UI optimista o bloqueada sin explicación y dificulta depurar latencia.

Accion: añadir resultado de request o `ClientRpc` de rechazo/confirmacion solo donde la UX lo necesite;
no replicar efectos visuales como autoridad.

### M5. Reglas de host/listener y singleplayer no estan totalmente separadas

Algunos métodos hacen guards basados en `NetworkManager.IsListening`, otros en `NetworkBehaviour.IsSpawned`
y otros en la presencia del componente networkado. Las tres rutas pueden producir comportamientos distintos
durante arranque y shutdown.

Accion: centralizar `NetworkRuntime.IsServerAuthoritative` y usarlo para todos los mutadores de gameplay.

## Sistemas evaluados

| Sistema | Resultado |
| --- | --- |
| Session/lobby | Base correcta; host disconnect y Relay pendientes |
| Player spawn/ownership | Parcial; input/camera local, movimiento remoto no validado correctamente |
| Health/damage | Riesgo critico por dano arbitrario a objetivos server-owned |
| Inventory | Parcial; snapshot incompleto y confirmaciones ausentes |
| Loot | Pickup/entrega con base host; prefabs y validadores incompletos |
| Train | Estado replicado; RPC de salida necesita contexto y cargo necesita pose |
| Economy/quota | Mutaciones protegidas, pero snapshots con churn y managers locales |
| Enemies | AI host-only; despawn/transform/animacion requieren endurecimiento |
| Threat | Estado replicado, pero sincronizacion por frame y referencias de escena |
| GameState/run | Replicado; host loss y orden de inicializacion pendientes |
| Body recovery | Base host-only; jugador vivo desconectado sin contrato |
| Presentation | Puede permanecer local; held items/animator/ragdoll deben leer estado publico |

## Fortalezas observadas

- Separacion razonable entre `NetworkBehaviour` y gameplay existente.
- Uso consistente de `NetworkVariable` con escritura de servidor en la mayoria de snapshots.
- Uso de `ServerRpcParams.Receive.SenderClientId` en los RPCs sensibles.
- Guards tempranos y componentes pequeños, coherentes con el estilo del proyecto.
- El core loop mantiene una intención clara: `request -> validate -> apply`.
- El proyecto no necesita sincronizar camara, smoothing, UI ni VFX como estado de juego.

## Plan de correccion recomendado

### P0 - Antes de cualquier Play Mode

1. Cerrar dano arbitrario: fire request validado por host y eliminar la puerta generica de `NetworkHealth`.
2. Validar fase/distancia/vida en salida de tren y compra.
3. Sustituir todos los `Destroy` de NetworkObjects por despawn host-authoritative.
4. Corregir validacion de movimiento para cubrir jugadores owner de clientes remotos.
5. Rechazar prefabs dinamicos sin `NetworkObject` con error claro.

### P1 - Antes de QA de 2-4 jugadores

1. Eliminar escrituras de snapshots cada frame y reconstrucciones permanentes de `NetworkList`.
2. Definir host disconnect como cierre de sesión, sin falso host migration.
3. Definir contrato de jugador vivo desconectado.
4. Completar estado de inventario, ammo y held item.
5. Completar pose y desparentado de cargo.

### P2 - Robustez y UX

1. Validador común de interacción.
2. Respuestas de rechazo/confirmacion para acciones visibles.
3. Inicializacion por referencias serializadas/OnNetworkSpawn.
4. Sustituir globals locales por una API clara de lectura/mutacion.
5. Animacion, ragdoll y VFX como presentacion derivada del estado replicado.

## Gate de pruebas requerido

No marcar P6 como terminado hasta probar en 1/2/3/4 jugadores:

- host-only y host + cliente conectado/desconectado;
- spawn y despawn rapido;
- pickup simultaneo, inventario lleno, drop, throw y re-pickup;
- compra simultanea y pago de cuota;
- cargo, salida, entrega y llegada;
- daño, muerte, revive, cuerpo cargado y cuerpo abandonado;
- enemigo spawn/AI/ataque/muerte/despawn;
- desconexion en MacLarens, Town, tren y durante ResolvingDay;
- host loss con cierre de sesion visible;
- valores invariantes: dinero, deuda, cuota, cargo, inventario y loot no negativos ni duplicados.

## Correcciones aplicadas en esta revision

- `NetworkHealth` ya no acepta dano arbitrario de clientes contra cualquier objetivo server-owned.
- `NetworkWeaponAuthority` recalcula en host el objetivo, distancia, linea de vision y headshot;
	el cliente no decide dano ni hitbox.
- `NetworkTrainState` valida jugador conectado, vida, distancia y fase antes de iniciar salida.
- `ShopStand.TryPurchase` repite fase, estado de run, dinero y capacidad en la ruta RPC.
- `NetworkPlayerSpawner` comprueba `IsSpawned` antes de despawn y rechaza prefabs no registrados.
- `EnemyController`, `LootSpawner`, `EnemySpawner`, `BreakableOnImpact` y
	`TownExtractionResolver` usan lifecycle de despawn compatible con NGO cuando corresponde.
- Los snapshots de tren, economia, threat, game state y run state solo escriben cuando cambia el
	valor; el cargo ya no reconstruye la `NetworkList` cada frame.
- `NetworkPlayer` valida desplazamiento en el servidor para jugadores locales y remotos.
- `NetworkInventoryState` replica tambien slot seleccionado y presencia de item en mano.
- `NetworkCargoState` replica id, posicion local y rotacion local, y desparenta items retirados.

## Riesgos restantes antes de Play Mode

- La validacion de movimiento sigue siendo una comprobacion de desplazamiento, no simulacion de input
	server-authoritative. Debe probarse y calibrarse con `NetworkTransform` real.
- La perdida del host cierra la sesion; no existe host migration. La UI debe volver al menu.
- Un jugador vivo desconectado necesita una politica de producto definitiva durante `Run`.
- `ItemHolder`, animadores, ragdoll, VFX y UI necesitan wiring y validacion de presentacion.
- Relay/Authentication, registro de prefabs y referencias serializadas no pueden validarse aqui.

## Conclusion

La base es una buena dirección arquitectonica, pero todavía es una **integracion multiplayer en fase de
endurecimiento**, no una conversión verificada. El siguiente trabajo debe ser corregir los P0 de esta
auditoria antes de usar el Editor para validar prefabs y escenas. Los puntos visuales y de UI pueden
esperar; las reglas de autoridad, ciclo de vida y anti-cheat no.
