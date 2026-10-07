- Connect sounds (bell hiss when it begins moving and rail sounds) on the train movement
- Ajustar número de enemigos en threat
- Ajustar valor y probabilidad de los items
- Meter memes



NOTAS MACLARENS


- El cursor se desbloquea a veces, no se mantiene bloqueado en el juego. El cursor solo debe desbloquearse en el menú principal.
- Explicar mejor las mecánicas de movimiento o añadir un texto tutorial en el menú
- A veces el fantasma spawnea pero no se mueve por alguna razón
- Cuando hay múltiples jugadores algunos han respawneado en el pueblo
- A veces al droppear rápido los objetos despawnean? A un jugador al soltar un item perdió otro que tenía en el inventario
- Arreglar movimiento en los tejados, un jugador no podía moverse ni saltar al llegar a un tejado. Puede que se deba porque el suelo está en pendiente
- El número de LootItems está escalando con los jugadores??
- Los jugadores están reapareciendo con su inventario cuando su cuerpo se pierde. Al morir deberían de soltar todos sus objetos en su posición y vaciar su inventario
- Cambiar el texto de Threat a Peligro
- Poner los sprites correspondientes a los LootItems
- Join code no se refresca, se intenta unir con el Join Code anterior. Los jugadores tienen que cerrar el juego y volver a abrirlo para volver a conectarse a un lobby
- Botón de salir no funciona
- Configurador del tiempo de la partida 
- Configurador del dinero inicial de la Quota ?
- Spawnean demasiados enemigos de todos los tipos
- Detectó a uno de los jugadores como en el tren por alguna razón y lo arrastró a pesar de estar en el pueblo. (El jugador estaba en el pueblo y otro activó el tren desde el MacLarens)
- A veces el cadaver de los jugadores se cae del mapa. Seguramente por el ragdoll
- Al usar el lazo dentro del tren el cuerpo sale volando. Ragdoll?
- El cuerpo muerto de los jugadores no se mantiene en el tren. Ragdoll?


el lazo debería poder tirar de un jugador muerto

REVISAR
El cadaver de los jugadores no es visible para los clientes, 
El cadáver de los jugadores no se puede meter en el tren, el cuerpo se queda fuera y no consigo poder lanzar el cuerpo dentro de un vagón
La cámara de los jugadores no se levanta de forma progresiva, está en el suelo haciendo ragdoll y de repente se teletransporta a la posición natural. También hay como un flicker de la posición original -> cae al suelo a la nueva posición -> aparece la posición original -> el jugador instantáneamente aparece en la posición correspondiente
Jasper Dog position and behaviour not replicating across players
Cuando se interactúe con la palanca del tren para ir al siguiente destino, debe girar 45 grados y volver a su posición (una animación simple de uso)
Object breaking has stopped working, no matter how many times I try potions and bottles won't break. Exploding objects do work properly.