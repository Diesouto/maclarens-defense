# MacLarens Defense Docs

## North Star

MacLarens Defense tiene que producir este bucle una y otra vez:

```text
loot -> risk -> escape -> cash in
```

El objetivo del proximo mes es llegar a un MVP divertido y funcional en singleplayer antes del 2026-09-25. El cooperativo con NGO + Relay se trata como una extension inmediatamente posterior, con objetivo 2026-10-02.

## Documentos

- [Roadmap MVP](Roadmap_MVP.md): roadmap refinado, gates semanales, criterios de salida y reglas de recorte.
- [Arquitectura MVP](Arquitectura_MVP.md): organizacion de sistemas, ownership de estado y preparacion para multiplayer.
- [Code Style Unity 6](Code_Style_Unity6.md): convenciones de codigo, assets, escenas y validacion.
- [MVP Backlog](MVP_Backlog.md): lista priorizada de tareas y criterios de aceptacion para ejecucion diaria.
- [MVP Scope Matrix](MVP_Scope_Matrix.md): corte exacto de alcance, plan de contenido y clasificacion de features por coste/impacto.

## Working Agreements

- Si una feature no mejora directamente `loot -> risk -> escape -> cash in`, no entra en el MVP.
- Singleplayer demuestra el bucle. Multiplayer lo escala; no lo valida.
- Cada semana debe terminar con una build jugable y un playtest corto.
- Antes de abrir una nueva feature, se corrigen errores de consola y referencias rotas de la feature anterior.
- Cuando haya retraso, se recorta alcance antes de aumentar complejidad tecnica.

## Baseline actual del proyecto

- Movimiento de jugador
- Shooting
- Reload
- Health
- WeaponDataSO
- Enemigo base
- NavMesh
- Ragdoll
- HUD

Usa el roadmap para decidir prioridades, la arquitectura para decidir donde vive cada comportamiento y la guia de estilo para mantener consistencia al crecer el proyecto.