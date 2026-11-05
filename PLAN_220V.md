# Modo 220 V (bifásico)

Escenas aparte, copiadas de las de 127 V el 2026-11-05: `Assets/Scenes/Nivel1_220.unity` (de `SampleScene`) y `Assets/Scenes/Nivel2_220.unity` (de `Nivel2`). Las de 127 V no se tocaron.

## Estado

- [x] Escenas, cables, terminales, pasos y textos (revisado en edición: sin referencias rotas).
- [x] Selector 127 V / 220 V en `UIMenu`.
- [x] Voces (Azure, Dalia HD) con `generar_voces_220.ps1`, asignadas con el menú **Tesis > Asignar voces 220 V**.
- [ ] Prueba en el visor (nada de esto se ha jugado).

## Menú

- `VoltageMode` (estática, PlayerPrefs `modo_220`): `SceneFor("SampleScene")` → `Nivel1_220`, `SceneFor("Nivel2")` → `Nivel2_220`; `LevelIdFor` agrega `_220` al id de resultados.
- `MenuVoltageSelector` en `UI/Canvas/UIElement/Selector_Voltaje`: dos botones 127 V / 220 V; agrega el voltaje al título de los botones de nivel y refresca sus tiempos (`MenuLevelEntry.Refresh`).
- Los botones de nivel llaman `MenuController.LoadLevel` (antes `LoadScene`). "Ir al nivel" del tutorial también respeta el modo.

## Nivel1_220 (27 pasos)

| Cable | Color | Acometida | Carga |
|---|---|---|---|
| Fase A | rojo | `Terminal_Linea`, arriba a la izquierda | `Terminal_Carga_Fase`, abajo a la izquierda |
| Fase B | negro | `Terminal_LineaB` (antes `Terminal_Puente`, `GEO_Tuerca10`), arriba a la derecha | `Terminal_Carga_FaseB` (antes `Terminal_Carga_Neutro`, `GEO_Tuerca9`), abajo a la derecha |
| Neutro | blanco | conector, `Izq`/`Centro` | conector, `Terminal_Neutro_Der` |
| Tierra | verde | conector, `Izq`/`Centro` | — |

- `Fase_B`: clon de `Fase` (negro), rollo en la mesa en x 6.18. `Neutro` y `Tierra` se recorrieron 40 cm hacia −x y la mesa creció 40 cm hacia −x (5.35–7.15). Su recorrido son 35 puntos `Entrada_FaseB_*` (copias de los de la fase, desplazados ~1 cm) en un cuarto `ConduitPathGuide` de `Tubo2`, agregado a `PullGuide.cables`. Ruta guardada `Cables_Guardados/Ruta_FaseB` (espejo de la fase, z 0.27), corte `Corte_FaseB` y marca `MarcaCorte_FaseB`, flecha `Flecha_Punta_FaseB`.
- `Carga_FaseB`: clon de `Carga_Fase` (negro), en la mesa a z 1.32. Ruta `Rutas_Carga/Ruta_FaseB`: sale por la orilla baja derecha del hueco (7.486, 1.318) y pasa el muro en x 7.510, entre la fase y el neutro.
- Las rutas de acomodo de la fase B son espejo de las de la fase A (eje x 7.5134), con el mismo cruce: la de carga pasa por delante de la de acometida.
- Sin puente de neutro: `Puente_Neutro` y los pasos `x_15_pelar_puente`/`x_16_colocar_puente` están apagados.
- `GuidePullStep.jammedPullHint`/`notHookedHint` y `GuideTapeStep.notHookedHint` (campos nuevos; en 127 V conservan el texto de "tres cables") dicen "cuatro".
- `levelId` `nivel1_220`, siguiente nivel `Nivel2_220`.

## Nivel2_220 (10 pasos)

- Pastilla de dos polos: segunda `MLD_TermoMagnetico_B` al lado y barra `Union_Palancas`; collider de 4.4 cm. La ranura de `Tapa_Caja` se ensanchó a x 7.492–7.540.
- Fase A en la zapata derecha del bloque (`Terminal_Bloque_Fase`, `GEO_Tuerca (1)`); fase B en la izquierda (`Terminal_Bloque_FaseB`, `GEO_Tuerca`). `Carga_FaseB` es un clon de `Carga_Fase` desplazado (−1.3 cm x, −1.6 cm z).
- `Medidor_Cableado`: `Cableado_FaseB` (espejo de la fase), `Cableado_Carga_FaseB` (la malla del neutro de carga, en negro) y `Cableado_Carga_Neutro` nuevo (al conector); `Cableado_Puente` apagado. Mallas en `Assets/Scenes/Nivel2_220_Datos`.

## Pendiente de probar en el visor

- Jalar la guía con 4 cables (amarre, atasco, empuje en el murete) y que el rollo de la fase B no estorbe en la mesa más larga.
- Las bocas y rutas nuevas: fase B arriba a la derecha (donde iba el puente) y el neutro de carga en la entrada derecha del conector, muy cerca de la columna de la tierra (1.1 cm).
- Que entren 3 cables por el hueco de atrás sin temblar.
- Nivel 2: que la pastilla doble entre en la ranura y que la fase B no estorbe al montarla.
- Arreglos a mecánicas compartidas se deben hacer en las dos versiones de cada escena.
