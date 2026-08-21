# Prueba de interaccion base

## Preparacion rapida

1. Abre una escena jugable que ya tenga un jugador con `PlayerInputHandler`.
2. Crea un `Cube` frente a la camara y confirma que tiene `BoxCollider`.
3. Anade `InteractionTestInteractable` al cubo.
4. Anade `PlayerInteractor` al objeto del jugador.
5. En `PlayerInteractor`, asigna la camara que usa el jugador y el `InteractUI` del HUD.
6. Deja `Interaction Distance` en `3` y `Interaction Mask` incluyendo la capa del cubo.
7. En el componente de prueba usa `Prompt = Abrir caja de prueba` y deja `Disable After Interaction` desactivado.

## Validacion en Play Mode

- Mirando al cubo: aparece `Abrir caja de prueba`.
- Mirando fuera del cubo: el prompt desaparece.
- Pulsando la accion `Interact`: aparece un log con el nombre del cubo y `Total: 1`.
- Pulsando de nuevo: el contador aumenta a `Total: 2`, sin duplicar objetos ni tocar `PlayerController`.
- Con `Disable After Interaction` activado: la primera pulsacion desactiva el cubo y el prompt desaparece.

## Resultado esperado

La prueba confirma el contrato completo:

```text
PlayerInteractor -> IInteractable -> InteractionTestInteractable
                                -> InteractUI
```

El componente es solo de validacion y debe eliminarse de la escena antes de crear `LootItem` real.
