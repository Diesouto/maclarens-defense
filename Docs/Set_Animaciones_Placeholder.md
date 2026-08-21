# MacLarens Defense - Set de animaciones placeholder

## Objetivo

Fijar los estados de animacion que usara el MVP para evitar cambios de rig o de controlador durante el prototipo.

## Estado actual

Los controladores propios ya existentes son:

- `Assets/_Project/Animations/Controllers/PlayerAnimationController.controller`
- `Assets/_Project/Animations/Controllers/EnemyAnimationController.controller`

Ambos usan el parametro `Speed` y un blend tree de locomocion. Los clips disponibles en el proyecto son:

- `Assets/_Project/Animations/Clips/Idle.fbx`
- `Assets/_Project/Animations/Clips/Walking.fbx`
- `Assets/_Project/Animations/Clips/Running.fbx`

Estos tres clips quedan bloqueados como locomocion base placeholder para jugador y enemigo. No se cambia el parametro `Speed` ni la estructura del blend tree base.

Los clips adicionales ya importados son:

- Reacciones y recovery: `Standing React Large Gut.fbx`, `Standing React Death Backward.fbx` y `Getting Up.fbx`.
- Pistola: contenido de `Pistol_Handgun Locomotion Pack/`, incluyendo idle, run, walk, backward y strafe.
- Rifle/escopeta: contenido de `Basic Shooter Pack/`, incluyendo idle apuntando, locomocion direccional, disparo, recarga y hit reaction.
- Objetos: `Carry/CarryingIdle.fbx`, `Carry/CarryingWalking.fbx` y `Carry/CarryingRunning.fbx`.

## Contrato de estados MVP

| Estado | Jugador | Enemigo | Requisito |
| --- | --- | --- | --- |
| Idle | Si | Si | Ya disponible |
| Walk | Si | Si | Ya disponible |
| Run | Si | Opcional | Base disponible; armado usa su propia locomocion |
| Hit | Si | Si | Importado; validar transicion y rig |
| Death | Opcional | Opcional | Importado como fallback; ragdoll sigue siendo principal |
| Recovery | Si | Si | `Getting Up.fbx`, pendiente validar root motion |
| Carry idle | Si | No | Importado; usar con objeto pesado |
| Carry walk | Si | No | Importado; usar con velocidad reducida |
| Carry run | No | No | Importado, pero excluido: los objetos pesados bloquean sprint |

### Decisiones bloqueadas

- Un unico rig humanoide compatible con los controladores actuales.
- Locomocion base controlada por `Speed`.
- La locomocion armada usa un controlador o capa separada con direccion de movimiento independiente de la orientacion de la mirada.
- El estado `Carry` tiene prioridad sobre locomocion armada: al transportar un objeto pesado se bloquea el arma y se usa la pose de objeto.
- Hit, Death y Recovery seran estados discretos, no parte del blend tree de locomocion.
- `Death` debe poder dejar paso al ragdoll existente sin que la animacion sea la fuente de verdad de la muerte.
- `Recovery` devuelve el control al motor del personaje; no modifica vida ni reglas de gameplay.
- No se anaden capas, parametros o transiciones definitivas hasta verificar los clips importados en Unity.

## Feedback y aprobacion del set propuesto

El set propuesto es correcto para el MVP, con estas condiciones:

- **Set de objetos**: aprobado como familia independiente para transporte. `CarryingIdle` y `CarryingWalking` entran en el MVP; `CarryingRunning` queda reservado para objetos ligeros futuros porque la caja fuerte bloquea sprint.
- **Pistol Handgun Locomotion**: aprobado para locomocion armada direccional. Las variantes backward y strafe son las que necesitamos para mirar al frente mientras se cambia de direccion.
- **Basic Shooter Pack**: aprobado para escopeta/rifle, con aiming idle, firing, reloading y locomocion direccional en una capa armada separada.
- **Hit**: aprobado para jugador y enemigo; debe ser un estado breve y no-loop.
- **Get Up**: aprobado como `Recovery`; debe devolver el control al motor sin mover la raiz de forma inesperada.
- **Death**: recomendable conservarlo como fallback visual, pero no debe ser requisito para activar la muerte. El daño puede activar ragdoll directamente y la animacion queda disponible para una muerte sin ragdoll o una futura variante.

La separacion entre objetos y armas es buena: evita mezclar la pose de transporte con la locomocion armada. La regla de autoridad debe ser que el estado de gameplay decide si el jugador puede correr, disparar o interactuar; la animacion solo representa ese estado.

## Integracion diferida

Los clips ya estan importados y la tarjeta P0 queda cerrada. La verificacion detallada y la conexion a controladores se hara cuando exista el gameplay que consume cada estado, con el mismo rig humanoide y licencia compatible:

- Player/enemy: hit, get up/recovery y death opcional.
- Objeto en mano: idle y walking ya importados; pickup y drop deben resolverse como transiciones puntuales. `CarryingRunning` queda fuera de la caja fuerte.
- Pistol Handgun Locomotion: idle, walk, run, backward y strafe.
- Basic Shooter Pack: aiming idle, walk/run direccional, firing y reloading.

Antes de incorporarlos hay que verificar en Unity que:

1. El avatar se marca como Humanoid y queda valido.
2. La orientacion y escala coinciden con los FBX de locomocion.
3. El clip no contiene movimiento de raiz no deseado para el controlador actual.
4. Hit y Recovery son no-loop; Idle, Walk y Run conservan su configuracion de locomocion.
5. Death, si se usa, puede reproducirse una vez; el ragdoll sigue siendo la ruta principal de muerte por impacto.

## Siguiente orden de integracion

1. Al implementar arma direccional, verificar avatar y root motion de los clips armados.
2. Mantener `PlayerAnimationController` como locomocion base estable.
3. Crear una capa `Armed` con blend direccional para pistola y rifle; no mezclar sus clips con el blend tree base.
4. Al implementar carry, crear una capa `Carry` con prioridad superior a `Armed`, usando `CarryingIdle` y `CarryingWalking`.
5. Conectar hit, recovery y death al estado de salud/ragdoll, no a la UI ni al interactuable.

## Criterio de cierre de la tarjeta

La decision de set queda bloqueada y los clips ya estan importados. La tarjeta `[Animation] Bloquear set Mixamo de placeholder` queda cerrada para P0; la integracion tecnica se abre dentro de P1/P4 cuando exista una escena de prueba que ejercite carry, arma o estados de salud. El set de objetos sigue siendo una dependencia del sistema de transporte, no de la locomocion armada.

Siguiente paso permitido: continuar con `LootDataSO`, `LootItem` e inventario. La integracion de `Armed` y `Carry` queda aparcada hasta que esos sistemas existan.
