# MacLarens Defense - Auditoria de contenido western

## Objetivo

Cerrar el conjunto de contenido visual para el primer pueblo y los objetos de loot del MVP sin importar otro pack durante la fase de prototipo.

## Fuente aprobada

`Assets/ThirdParty/Synty/PolygonWestern`

La fuente cubre edificios, props, vehiculos, vias y decoracion ambiental con una familia visual coherente. El pack queda aprobado como fuente principal del MVP.

## Shortlist de loot MVP

Los ocho props siguientes se convierten en candidatos de `LootDataSO`. Sus valores son de gameplay y quedan pendientes de balance, no son propiedades del modelo.

| Candidato | Prefab | Rol visual | Valor inicial sugerido |
| --- | --- | --- | ---: |
| Lingote | `Prefabs/Props/SM_Prop_GoldBar_01.prefab` | Loot muy valioso, silueta pequena | 100 |
| Caja fuerte | `Prefabs/Props/SM_Prop_Vault_01.prefab` | Loot valioso, objetivo de alto riesgo | 80 |
| Caja registradora | `Prefabs/Props/SM_Prop_Cash_Register_01.prefab` | Loot de tienda, silueta clara | 60 |
| Maleta | `Prefabs/Props/SM_Prop_Suitcase_01.prefab` | Loot transportable intermedio | 45 |
| Saco | `Prefabs/Props/SM_Prop_Sack_01.prefab` | Loot comun de almacen | 35 |
| Botella | `Prefabs/Props/SM_Prop_Bottle_01.prefab` | Loot pequeno y frecuente | 25 |
| Lata | `Prefabs/Props/SM_Prop_Tin_01.prefab` | Loot comun de bajo valor | 15 |
| Fichas de poker | `Prefabs/Props/SM_Prop_Poker_Chip_01.prefab` | Loot tematico de saloon | 10 |

### Reglas de uso

- El prototipo arranca con cinco objetos: lingote, maleta, saco, botella y lata.
- Caja fuerte, caja registradora y fichas completan la variedad antes del freeze si el pickup y el drop ya son estables.
- Cada item debe tener un prefab de mundo y un `LootDataSO`; el modelo no contiene estado de run.
- Los barriles, cajas, cestas y botellas rotas quedan como decorado hasta que el loop necesite mas variedad.
- No se importa otra familia visual para cubrir estos roles.

## Composicion inicial del pueblo

La escena jugable sera una sola escena con pueblo y tren. El tren es la zona segura fija y el punto de deposito/extraccion.

### Puntos de interes

1. **Estacion y tren**: zona segura, spawn del jugador, deposito de cargo y salida.
2. **Saloon**: riesgo alto y loot de mayor valor; usar `SM_Bld_Saloon_01` con mesa de poker, caja registradora, botellas y fichas.
3. **Tienda o bloque residencial**: ruta intermedia; usar fachadas `SM_Bld_Single_*` o `SM_Bld_Double_*` con maletas, sacos y latas.
4. **Jail y oficina**: referencia visual y cobertura para combate; usar `SM_Bld_Jail_01` con caja fuerte como objetivo opcional.
5. **Iglesia y cementerio**: limite legible del pueblo y ruta alternativa; usar `SM_Bld_Church_01`, tumbas y decoracion ambiental.
6. **Periferia de almacen**: espacio abierto para retorno y futuras presiones; usar cobertizos, cajas, barriles, heno y vallas.

### Kit de construccion aprobado

- Edificios: `SM_Bld_Saloon_01`, `SM_Bld_Jail_01`, `SM_Bld_Church_01`, `SM_Bld_Shed_01`, `SM_Bld_Single_*`, `SM_Bld_Double_*`.
- Tren: `SM_Veh_Train_01`, `SM_Veh_Train_Carriage_01`, `SM_Veh_Train_Freight_01`, `SM_Veh_Train_Coal_01`.
- Estacion y vias: `SM_Bld_TrainStation_01`, `SM_Bld_TrainStation_Platform_01`, `SM_Env_Train_Track_Straight_01`, `SM_Env_Train_Track_Curve_01`.
- Lectura del espacio: `SM_Prop_RoadSign_01`, `SM_Prop_MessageBoard`, `SM_Env_Road_Straight_01`, `SM_Env_Road_Patch_01`.
- Cobertura y decoracion: barriles, cajas, heno, vallas, carretas, cactus y rocas del mismo pack.

## Fuera de la shortlist

- No se usara el cactus como enemigo en esta fase; queda reservado para una idea posterior y no cambia su uso como decoracion.
- No se usaran props de `PolygonGeneric` para loot o edificios mientras el pack western cubra la necesidad.
- No se anaden tiendas, NPCs, vehiculos interactuables ni piezas de tren fisicamente simuladas al alcance de esta auditoria.

## Criterio de cierre

La tarjeta `[Content] Auditar POLYGON Western Pack para props MVP` queda cerrada: existe una shortlist de ocho props de loot, una composicion inicial de seis puntos de interes y no es necesario buscar otro pack para comenzar el prototipo.

Siguiente tarjeta del backlog: bloquear el set de animaciones placeholder de Mixamo. Despues se retoma el bloque de interaccion base antes de abrir contenido jugable.
