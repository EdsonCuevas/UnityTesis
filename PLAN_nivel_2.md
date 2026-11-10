# Plan: nivel 2, caja de la pastilla

Escena `Assets/Scenes/Nivel2.unity`, copia de `SampleScene` tal como estaba en disco el 2026-11-02. Flujo: al terminar el nivel 1, el botón "Siguiente nivel" (`LevelManager.nextSceneName`) carga `Nivel2`. En `Nivel2` ese campo está vacío.

## Estado

- [x] Escena, medidor cableado fijo, pastilla, tapa, colisiones, terminales y 8 pasos (validado en edición).
- [ ] Prueba en el visor (nada de esto se ha jugado).
- [x] Audios generados con Azure Speech, voz `es-MX-Dalia:DragonHDLatestNeural`, con `generar_voces_nivel2.ps1` (lee la llave de `.env`; guardar el script en UTF-8 con BOM) y asignados en `narration`, `introNarration` y `voiceCues`. WAV 24 kHz, 16 bits, mono. `resultsNarration` sigue vacío. El nivel 1 y el tutorial siguen con Sabina.

## Pasos

| # | Objeto | Tipo | Qué hace el jugador |
|---|---|---|---|
| 1 | `01_rodear_murete` | ReachZoneStep | Rodea el murete siguiendo la línea (`Punto_Atras`, radio 0.6) |
| 2 | `02_quitar_tapa` | SeatedPartStep (Remove) | Aleja la tapa 25 cm y la suelta |
| 3 | `03_pelar_fase_caja` | StripWireStep | Pela `Carga_Fase` |
| 4 | `04_conectar_fase_caja` | MeterTerminalStep | Punta en `Terminal_Bloque_Fase`, tornillo `GEO_Tuerca (1)` |
| 5 | `05_pelar_neutro_caja` | StripWireStep | Pela `Carga_Neutro` |
| 6 | `06_conectar_neutro_caja` | MeterTerminalStep | Punta en `Terminal_Barra_Neutro`, tornillo `GEO_Tuerca (2)` |
| 7 | `07_montar_pastilla` | SeatedPartStep (Place, con bisagra) | Suelta la pastilla a ≤ 8 cm de su pose enganchada y empuja su parte de arriba hasta el clic |
| 8 | `08_cerrar_caja` | SeatedPartStep (Place) | Suelta la tapa a ≤ 12 cm de su asiento |

En `Nivel2_220` el orden es igual, con la fase B entre la fase A y el neutro (10 pasos): la pastilla de dos polos es el paso 9.

### Montaje de la pastilla (2026-11-09)

La pastilla se monta al final, después de conectar los cables, en dos tiempos:

1. **Enganchar.** Se suelta cerca del asiento y se acomoda sola enganchada por su borde de abajo y atrás (`Asiento_Pastilla/Bisagra_Pastilla`), inclinada 25° hacia el jugador. Suena un clic suave y se apaga su collider para que no empuje los cables.
2. **Presionar.** La mano que toca `Pastilla/Punto_Empuje` (radio 7 cm) y avanza hacia el fondo la va cerrando tanto como avanzó, con vibración ligera. A 3° entra sola: clic fuerte y pulso en el control más cercano.

Si se vuelve a agarrar enganchada, se desengancha. Tras 5 s enganchada sin empujarla sale el aviso "Empuja la parte de arriba de la pastilla hasta que haga clic" (`Audio/Nivel2/Avisos/aviso_empujar_pastilla.wav`, también en 220). Los WAV de los pasos conservan su nombre anterior (`03_montar_pastilla.wav`); la narración se regeneró con el texto nuevo. Código: `SeatedPart.hinge`, `pressPoint`, `hookAngle`, `clickAngle`; sin bisagra (la tapa) funciona como antes.

## Lo que cambió respecto al nivel 1

- **Medidor**: `Medidor_Cableado` son 6 mallas fijas generadas de las rutas de acomodo (`Assets/Scenes/Nivel2_Datos`). Apagados: `Fase`, `Neutro`, `Tierra`, `Puente_Neutro`, `Medidor_Terminales`, `Cortes_Cables`, `Cables_Guardados`, `Rutas_Carga`, `GEO_CintaAislante`, `GuiaJalacables` y los pasos del nivel 1.
- **Cables de carga**: la fase tiene 8 eslabones libres (~14 cm) y el neutro 10 (~17 cm), más 7 fijos y 2 anclas cada uno; el tramo fijo va dentro del tubo desde y 1.275. Antes eran 15 libres (~26 cm) y sobraba cable. Esperan quietos dentro de la caja (`WireGrab.startDormant`) hasta su paso de pelar; `WireGrab.stayFixed` los mantiene fijos al despertar.
- **Pastilla**: raíz nueva `Pastilla` con el pivote en el centro de la malla; `MLD_TermoMagnetico` se desempaquetó de su prefab. Su asiento está 7.6 cm más adentro que su posición original, que quedaba fuera de la caja. Empieza en el piso, en (8.05, −0.16, −0.8).
- **Tapa**: `Tapa_Caja` son 4 cubos con el material de la caja y una ranura para la pastilla. No hay modelo; si se hace uno, se cambia aquí.
- **Colisiones**: `Caja_Colisiones`. Los cables solo chocan con `Fondo` y `Abajo`; las cajas de `Sin_Cables` (paredes laterales, techo y cara del murete) los ignoran con `IgnoreLayerCollisions`, pero siguen deteniendo la tapa y la pastilla.
- **Código**: `SeatedPart`, `SeatedPartStep` (`Assets/Scripts/nivel_2/`).

## Decisiones supuestas, por confirmar

- La fase va al tornillo derecho del bloque y el neutro al tornillo superior de la barra. No hay foto de la caja real; no se conecta tierra en la caja.
- El radio de 3 cm de las bocas se dejó igual: solo hay dos terminales activas y están a unos 9 cm.

## Pendiente de probar en el visor

- Enganchar la pastilla y empujarla hasta el clic: que la mano se detecte (radio 7 cm en `Punto_Empuje`), que 25° se lea como enganchada y que el clic se sienta. Que no choque con el cable de fase ya conectado.
- Que los cables no tiemblen al despertar dentro de la caja, y cómo se ve el acomodo con los cables más cortos.
- Que la punta salga lo suficiente para pelarla (la fase unos 6.6 cm y el neutro unos 9.6 cm fuera de la caja); si queda justo, subir 1 o 2 eslabones. Que no se note el cable al atravesar las paredes.
- Alcanzar `GEO_Tuerca (1)` con el destornillador, justo arriba de la pastilla.
- Dirección de las bocas (entran hacia abajo) y lugar del panel `Panel_Caja`.
- Que quepan los 3 botones en el panel de resultados del nivel 1.
- Paso 1: que nada bloquee el rodeo al caminar; solo se revisó con rayos.
