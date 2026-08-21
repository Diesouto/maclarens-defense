# MacLarens Defense - Code Style Unity 6

## Objetivo

Usar un estilo de codigo y de assets que favorezca velocidad, claridad y bajo coste de mantenimiento durante un MVP corto.

## Idioma del proyecto

- Identificadores de codigo en ingles.
- Documentacion de produccion en espanol, salvo que haga falta compartirla fuera.
- Texto visible al jugador puede quedarse en ingles placeholder hasta definir localizacion.

## Convenciones C#

- Tipos, metodos, propiedades y eventos en `PascalCase`.
- Campos privados serializados en `camelCase` con `[SerializeField] private`.
- Campos privados no serializados en `camelCase`.
- Bools con prefijos `is`, `has`, `can` o `should`.
- Ficheros con el mismo nombre que el tipo principal.
- Eventos publicos con forma `OnXChanged`, `OnDeath`, `OnQuotaReached`.
- Metodos de consulta con forma `HasSpace`, `CanInteract`, `IsQuotaReached`.
- Metodos con fallo esperado usan `TryX` cuando mejore legibilidad: `TryAddItem`, `TryDepositAll`.

## Convenciones Unity

- Cachear referencias en `Awake`.
- Suscribirse en `OnEnable` y desuscribirse en `OnDisable` cuando la relacion dependa de habilitar o deshabilitar el componente.
- Usar `OnDestroy` para desuscribirse solo cuando la fuente de eventos viva mas que el componente y no exista mejor punto.
- Mantener `Update` fino; delegar a metodos privados pequenos.
- Preferir `TryGetComponent` cuando una dependencia sea opcional.
- Evitar `FindObjectOfType`, `FindFirstObjectByType` y `FindGameObjectsWithTag` en codigo de gameplay recurrente.
- Usar `SerializeField` en lugar de campos publicos mutables.
- Exponer solo lectura mediante propiedades cuando haga falta.
- Los `ScriptableObject` son para datos de configuracion, no para estado vivo.

## Decisiones de estilo especificas para este proyecto

- No introducir namespaces de forma retroactiva durante el MVP salvo que se haga un refactor deliberado de estructura completa.
- No crear singletons globales por defecto. Si un sistema necesita acceso global, justificarlo y limitarlo.
- Una clase nueva debe entrar en la carpeta del dominio que controla, no en `Player` por cercania casual.
- No meter logica de loot, tren o threat dentro de la UI o del weapon code.
- No modificar `PlayerController` para soportar cada nuevo interactuable.

## Estructura recomendada por dominio

```text
Assets/_Project/Scripts/
  Core/
  Interaction/
  Player/
  Loot/
  Train/
  Enemy/
  World/
  UI/
  Networking/
```

## Naming de assets

- Prefabs: `PF_Player`, `PF_Loot_Bottle`, `PF_Enemy_Bandit`
- ScriptableObjects: `LD_Bottle`, `LD_GoldWatch`, `WD_Revolver`, `QD_DefaultQuota`, `TH_Default`
- Escenas: `Bootstrap`, `MainMenu`, `Town_Western_01`, `Sandbox_Loot`, `Sandbox_Combat`
- Materiales: `MAT_` prefijo
- Audio: `SFX_` prefijo
- VFX prefabs: `VFX_` prefijo

La idea no es hacer nomenclatura corporativa; la idea es poder filtrar rapido en el Project window.

## Inspector y serializacion

- Agrupar solo cuando haya bloques claros de configuracion usando `[Header]`.
- Usar `[Min]`, `[Range]` y `[Tooltip]` en valores de tuning expuestos.
- Las referencias requeridas deben quedar visibles y faciles de validar en inspector.
- No serializar caches de runtime solo para verlas en inspector, salvo que aporten depuracion real.

## Reglas de arquitectura al escribir codigo

- UI observa y presenta; no decide.
- Los managers poseen un sistema concreto y elevan eventos.
- Los interactuables encapsulan su propia validacion.
- El input expresa intencion; el sistema propietario decide si la accion se aplica.
- Las dependencias de datos se pasan por referencia serializada o se resuelven al inicio, no cada frame.

## Patrones recomendados

Bueno:

```text
PlayerInteractor -> IInteractable -> PlayerInventory -> QuotaManager -> UI
```

Malo:

```text
PlayerController conoce loot, tren, puertas, quota, threat y HUD a la vez
```

Bueno:

```text
ThreatManager.AddLootThreat(value)
```

Malo:

```text
LootItem modifica por su cuenta spawns de enemigos y HUD
```

## Logging y errores

- `Debug.Log` solo para depuracion temporal.
- `Debug.LogWarning` cuando una referencia opcional falta y hay fallback razonable.
- `Debug.LogError` cuando un setup invalido rompe el comportamiento esperado.
- Si un componente no puede funcionar sin referencias criticas, debe deshabilitarse explicitamente y fallar pronto.

## Reglas para features nuevas

Toda feature nueva debe dejar claros estos puntos:

- Que componente es owner del estado.
- Que eventos expone.
- Que dependencias necesita.
- Como se valida en singleplayer.
- Como afectaria a autoridad de servidor en fase multiplayer.

## Definicion de hecho para una tarea de gameplay

Una tarea no esta terminada hasta que cumple todo esto:

- Compila sin errores.
- No deja referencias rotas en prefabs o escena tocados.
- Tiene camino feliz validado en una escena jugable o sandbox.
- Su UI minima existe si el jugador necesita leer ese estado.
- Sus fallos principales estan contemplados.
- Si afecta a loot, cuota, threat, inventario o enemigos, su futura autoridad en servidor esta al menos pensada.

## Reglas de alcance tecnico

- No reescribir sistemas que ya funcionan solo por gusto arquitectonico.
- Primero se hace jugable; despues se limpia lo que realmente molesta.
- Si un hack de prototipo sobrevive mas de un milestone, se abre tarea de saneamiento.
- Si una feature exige dos sistemas nuevos y no fortalece el core loop, se corta.