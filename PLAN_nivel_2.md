# Plan: nivel 2, caja de la pastilla

Escena `Assets/Scenes/Nivel2.unity`, copia de `SampleScene` tal como estaba en disco el 2026-11-02. Flujo: al terminar el nivel 1, el botón "Siguiente nivel" (`LevelManager.nextSceneName`) carga `Nivel2`. En `Nivel2` ese campo está vacío.

## Estado

- [x] Escena, medidor cableado fijo, pastilla, tapa, colisiones, terminales y 8 pasos (validado en edición).
- [ ] Prueba en el visor (nada de esto se ha jugado).
- [x] Audios generados con `generar_voces_nivel2.ps1` y asignados en `narration`, `introNarration` y `voiceCues` (2026-11-02). `resultsNarration` sigue vacío.

## Pasos

| # | Objeto | Tipo | Qué hace el jugador |
|---|---|---|---|
| 1 | `01_rodear_murete` | ReachZoneStep | Rodea el murete siguiendo la línea (`Punto_Atras`, radio 0.6) |
| 2 | `02_quitar_tapa` | SeatedPartStep (Remove) | Aleja la tapa 25 cm y la suelta |
| 3 | `03_montar_pastilla` | SeatedPartStep (Place) | Toma la pastilla del piso y la suelta a ≤ 8 cm de su asiento |
| 4 | `04_pelar_fase_caja` | StripWireStep | Pela `Carga_Fase` |
| 5 | `05_conectar_fase_caja` | MeterTerminalStep | Punta en `Terminal_Bloque_Fase`, tornillo `GEO_Tuerca (1)` |
| 6 | `06_pelar_neutro_caja` | StripWireStep | Pela `Carga_Neutro` |
| 7 | `07_conectar_neutro_caja` | MeterTerminalStep | Punta en `Terminal_Barra_Neutro`, tornillo `GEO_Tuerca (2)` |
| 8 | `08_cerrar_caja` | SeatedPartStep (Place) | Suelta la tapa a ≤ 12 cm de su asiento |

## Lo que cambió respecto al nivel 1

- **Medidor**: `Medidor_Cableado` son 6 mallas fijas generadas de las rutas de acomodo (`Assets/Scenes/Nivel2_Datos`). Apagados: `Fase`, `Neutro`, `Tierra`, `Puente_Neutro`, `Medidor_Terminales`, `Cortes_Cables`, `Cables_Guardados`, `Rutas_Carga`, `GEO_CintaAislante`, `GuiaJalacables` y los pasos del nivel 1.
- **Cables de carga**: 22 eslabones + 2 anclas. Los 15 de la punta quedan libres (~26 cm); el resto va fijo dentro del tubo desde y 1.275. Esperan quietos dentro de la caja (`WireGrab.startDormant`) hasta su paso de pelar; `WireGrab.stayFixed` los mantiene fijos al despertar.
- **Pastilla**: raíz nueva `Pastilla` con el pivote en el centro de la malla; `MLD_TermoMagnetico` se desempaquetó de su prefab. Su asiento está 7.6 cm más adentro que su posición original, que quedaba fuera de la caja. Empieza en el piso, en (8.05, −0.16, −0.8).
- **Tapa**: `Tapa_Caja` son 4 cubos con el material de la caja y una ranura para la pastilla. No hay modelo; si se hace uno, se cambia aquí.
- **Colisiones**: `Caja_Colisiones` (caja con hueco arriba y cara del murete).
- **Código**: `SeatedPart`, `SeatedPartStep` (`Assets/Scripts/nivel_2/`).

## Decisiones supuestas, por confirmar

- La fase va al tornillo derecho del bloque y el neutro al tornillo superior de la barra. No hay foto de la caja real; no se conecta tierra en la caja.
- El radio de 3 cm de las bocas se dejó igual: solo hay dos terminales activas y están a unos 9 cm.

## Pendiente de probar en el visor

- Tomar la pastilla del piso (¿hay que agacharse demasiado?) y soltarla en su asiento.
- Que los cables no tiemblen al despertar dentro de la caja, y que el acomodo de 15 eslabones en ~12 cm se vea bien.
- Alcanzar `GEO_Tuerca (1)` con el destornillador, justo arriba de la pastilla.
- Dirección de las bocas (entran hacia abajo) y lugar del panel `Panel_Caja`.
- Que quepan los 3 botones en el panel de resultados del nivel 1.
- Paso 1: que nada bloquee el rodeo al caminar; solo se revisó con rayos.
