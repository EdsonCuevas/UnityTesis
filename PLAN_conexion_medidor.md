# Plan: conexión real del medidor monofásico (Nivel 1)

> Documento de trabajo para continuar entre ventanas de contexto.
> Al iniciar una fase nueva: leer este archivo completo, revisar la sección **Estado** y
> marcar las casillas al terminar cada tarea.

Imagen de referencia: [`conexion_medidor_monofasico.jpeg`](conexion_medidor_monofasico.jpeg) (base redonda de 4 mordazas con conector neutro al centro).

---

## Estado

- [x] **Etapa 1**: terminal, destornillador y pasos 11–13 (acometida) — falta la prueba en el visor (tarea 7)
- [x] **Etapa 2**: puente de neutro (pasos 15–16) — falta la prueba en el visor
- [x] **Etapa 3**: cables de carga (pasos 17–20) — falta la prueba en el visor
- [ ] **Etapa 4**: tirón de prueba (paso 20), errores en resultados, pulir el acomodo del cable, textos de narración

Notas de avance (agregar al terminar cada etapa: qué quedó, qué falta, qué hay que ajustar según la prueba en el visor):

- **Etapa 1 (2026-09-28)**:
  - **Corrección izquierda/derecha**: el jugador ve el medidor desde +Z mirando hacia −Z, así que a su izquierda está la X **mayor**. La tabla de tornillos de abajo ya está corregida: la terminal de línea (fase, arriba a la izquierda) es `GEO_Tuerca7`, no `GEO_Tuerca10`.
  - Scripts nuevos en `Assets/Scripts/nivel_1/Medidor/`: `WireTip` (ancla + eslabones ordenados desde una punta pelable; fijar/soltar eslabones), `TerminalScrew`, `Screwdriver`, `MeterTerminal` y `MeterTerminalStep`.
  - Escena: raíz `Medidor_Terminales` con `Terminal_Linea` (boca en x 7.594, entra hacia +X, tornillo `GEO_Tuerca7`) y `Terminal_Neutro_Izq/Centro/Der` (bocas por abajo en y 1.393, entran hacia +Y, tornillos `GEO_Tuerca6_Izq`, `GEO_Tuerca6`, `GEO_Tuerca6_Der`; los 3 a escala 0.65 y a 1.35 cm entre sí). Cada terminal tiene hijos `Acomodo_N` (ruta del cable al apretar). `Terminal_Neutro_Der` queda para el puente (etapa 2) y solo tiene 1 punto de acomodo.
  - `GEO_Destornillador`: movido junto a la cinta (7.36, −0.088, 0.82), acostado; ahora es dinámico con gravedad, con `IgnoreLayerCollisions` (capas de las pinzas), `ResetIfFallen` y `Screwdriver`. Su punta es el hijo `Punta` (local 0, −0.0115, 0; eje Z hacia afuera de la punta = −Y del modelo).
  - Marcador `Marcadores/Flecha_Terminal` (copia de `Flecha_Punta_Fase`, rombo a escala 0.03): el paso lo pone sobre la boca y, ya metida la punta, sobre el tornillo.
  - Pasos nuevos `11_conectar_fase_acometida`, `12_conectar_neutro_acometida` y `13_conectar_tierra` (sin narración). Panel en `Panel_Murete`.
  - Desactivados: `x_10/11/12_conectar_*` (renombrados), `Bornes_Medidor` y los 3 `Plug(Clone)` de los cables (sus triggers seguían activos). Renombrados a la numeración final: `06_encintar_amarre`, `07_jalar_guia`, `08/09/10_pelar_*`, `21_cerrar_registro`. Corregido "de el neutro" → "del neutro".
  - Reglas implementadas: la punta entra si una mano sostiene uno de sus primeros 12 eslabones, su extremo está a ≤ 3 cm de la boca y a ≤ 60°; entra en la boca libre más cercana. Sin pelar no entra (aviso). Con el tornillo apretado 1 vuelta o más la boca está cerrada (aviso "Afloja el tornillo…"). Sin apretar, se sale si se estira el cable 4 cm más que el mínimo desde que se agarró. Al apretar (2 vueltas) se fijan los eslabones sobre la ruta de acomodo en 0.4 s; al aflojar se sueltan.
  - Práctica: la punta debe quedar en una terminal de su conexión. Evaluación: cuenta donde quede apretada; los lugares equivocados se cuentan en `MeterTerminalStep.WrongConnections` (falta llevarlos a resultados, etapa 4).
  - Aviso nuevo, no previsto en el plan: "Apoya la punta del destornillador en el tornillo, de frente".
  - **Ajustes tras la primera prueba en el visor (2026-09-28)**. Problemas reportados: los cables sobrantes temblaban, atravesaban el murete y bajaban el rendimiento; el destornillador se caía del mapa; al girar el destornillador no pasaba nada y la muñeca quedaba incómoda. El usuario eligió:
    - **Paso de corte** (`08_cortar_sobrante`, `CutWireStep` + un `WireCutter` por cable en `Cortes_Cables`, marcas `Marcadores/MarcaCorte_*`). Se corta con las pinzas y el gatillo (flanco de subida) en un eslabón a 50 cm ± 15 cm de la entrada del ducto. El corte desactiva los eslabones entre la punta y el corte, los quita de `WireController.segments`, mueve el `StartAnchor` al corte, reconecta la junta, actualiza `WireEndAligner.neighbor` y los `cableHandles` del pelado. `ConduitPathGuide.IsInside(link)` se agregó para medir el cable libre. Quedan ~28 eslabones libres por cable en vez de ~76.
    - **Colliders simples**: el murete (`MeshCollider` cóncavo) y la carcasa ahora ignoran la capa Wire (`IgnoreLayerCollisions`). Los cables chocan con 8 cajas en `Murete2_Colisiones` (nicho x 7.315–7.710, y 1.175–1.505, fondo z 0.169, con hueco pasante de 6 cm en el centro para la etapa 3). Los eslabones tienen interpolación y `contactOffset` 0.003. La carcasa `GEO_Medidor` ya tenía un `MeshCollider` casi sin tamaño (bounds de 2 cm), así que en la práctica no chocaba.
    - **Destornillador acoplado**: a ≤ 3 cm del tornillo y ≤ 60° se acopla solo. El destornillador real se oculta y se ve una copia alineada sobre el tornillo que gira con él. El giro se mide con el roll del control sobre su propio eje (sin importar cómo se agarró); se suelta si el control se aleja 8 cm. Ahora es **1 vuelta** (la boca se cierra a partir de 0.5).
    - Causa del destornillador que se caía: su único collider era trigger; ahora es sólido.
    - **Error corregido**: el signo del giro estaba invertido (girar a la derecha aflojaba, y en 0 vueltas no se movía).
  - **Segunda prueba en el visor: seguía sobrando cable.** Causa: el recorrido del ducto empieza 25 cm antes de la boca real del tubo (y 1.135), en el frente de la carcasa (z 0.47 → 0.32), donde se enganchan los cables a la guía. El cable libre salía hacia el jugador y tenía que dar vuelta en U. Arreglo:
    - `ConduitPathGuide.ReleaseBefore(distancia)`: `WireCutter.Prepare` suelta los eslabones antes de `mouthDistance` = 0.25 m, así que el cable libre sale de la boca del tubo dentro de la base. El corte ahora deja 40 cm ± 10 medidos desde ahí.
    - Al apretar, `MeterTerminal` busca el primer eslabón fijo por otro sistema (cinemático y sin mano; por ejemplo, el primero dentro del ducto) a 60 eslabones o menos, y reparte **todo** el tramo libre entre la boca de la terminal, los puntos de acomodo y ese eslabón (con la separación ajustada). No queda bucle aunque se corte largo. Esto también sirve para el puente (etapa 2): su segundo extremo se acomoda hasta el primero.
  - **Tercera prueba: el negro seguía atravesando el medidor.** Causas: (1) la carcasa no tenía colisión útil para los cables, así que el cable libre caía por el fondo; (2) la caja `Abajo` del murete llegaba a y 1.175 y cubría la boca del tubo (y 1.135), por lo que los eslabones que salían del tubo nacían dentro de ella. Arreglo:
    - `Medidor_Colisiones`: 20 cajas en anillo (radio interior 0.175, z 0.17–0.43) con hueco para el tubo en la pared de abajo (z 0.245–0.33), y fondo en z 0.15–0.192 con hueco pasante de 6.4 cm al centro para la etapa 3.
    - `Murete2_Colisiones/Abajo` ahora llega a y 1.05; encima hay 4 cajas alrededor de un canal para el tubo (x 7.458–7.568, z 0.232–0.345). Las de adelante y atrás del canal llegan a y 1.155 para no tocar el fondo interior de la carcasa.
    - Comprobado en edición: el tramo que se suelta (0–25 cm del ducto), las bocas y los puntos de acomodo no tocan ninguna caja.
  - **Pendiente de probar en el visor**: cómo se siente meter la punta (radio 3 cm, 60°), el montaje del destornillador (1.5 cm, 30°) y si la orientación del agarre deja girar la muñeca sobre el vástago; la altura z de las bocas (0.2255 zapatas, 0.2015 conector); que el sobrante cuelgue sin estorbar; el tamaño y lugar de la flecha.

- **Relevo para la etapa 2 (puente de neutro, pasos 15–16 de la tabla nueva)**:
  - El usuario considera la etapa 1 lista para seguir; lo pendiente de visor (radios, ángulos, acomodo) se ajusta cuando lo reporte.
  - La terminal del puente en el conector ya existe: `Terminal_Neutro_Der` (x 7.4995, tornillo `GEO_Tuerca6_Der`). Solo tiene 1 punto de acomodo; hay que darle ruta hacia la zapata de arriba a la derecha. Falta crear la terminal de esa zapata: tornillo `GEO_Tuerca10` (x 7.414), boca en el lado de adentro (x ≈ 7.432), entrando hacia −X, z ≈ 0.2255.
  - Agregar las 2 puntas del puente a `accepts` de las 3 terminales del conector y de la zapata nueva. `MeterTerminal` encuentra todas las `WireStripper` de la escena en `Start`, así que el puente se detecta solo.
  - `WireTip` ya ordena los eslabones desde cualquiera de los dos extremos (`StartAnchor` o `EndAnchor`). El prefab `EndAnchor` no trae `PuntaPelable`: hay que duplicarla desde un `StartAnchor` y asignar `WireEndAligner.neighbor` y `WireStripper.cableHandles` de ese extremo.
  - Al apretar el segundo extremo del puente, `MeterTerminal.BuildDress` encuentra el primer extremo (sus eslabones fijos son cinemáticos) y reparte todo el puente entre ambos, así que no queda bucle.
  - Nuevos objetos que choquen con cables: revisar que no traslapen `Medidor_Colisiones` ni `Murete2_Colisiones` (cualquier eslabón libre dentro de una caja tiembla).
  - Nunca recompilar ni editar la escena mientras Unity esté en Play (el SDK de Meta truena con la recarga de scripts y los cambios se pierden). Revisar `EditorApplication.isPlaying` antes de tocar la escena.

- **Etapa 2 (2026-09-28)**:
  - **Cable `Puente_Neutro`** (raíz): negro, 14 eslabones + 2 anclas, unos 26 cm, en el piso sobre la banqueta (x 7.35–7.61, z 1.25), entre el tubo y el rollo rojo. La raíz, `WireRender` y `Helper` (apagado; en build no se oculta solo) salen del prefab `WireBuilder`, pero los eslabones y las **dos** anclas son **clones de los del cable `Neutro`**, porque el prefab no trae lo que se les agregó (agarres de Meta, interpolación, escala). Las dos anclas son copias del `StartAnchor` con su `PuntaPelable`; el `EndAnchor(Clone)` además lleva una `ConfigurableJoint` (copia de la de un eslabón) unida al último eslabón. Las 2 `PuntaPelable` tienen su `WireEndAligner.neighbor` y en `cableHandles` todo el puente (cualquier parte cuenta como sostenerlo). Se revisó que ninguna referencia apunte al `Neutro`.
  - **Terminal `Terminal_Puente`** (en `Medidor_Terminales`): boca en (7.4328, 1.3775, 0.2255), entra hacia −X, tornillo `GEO_Tuerca10`, 1 vuelta, etiqueta "la terminal de arriba a la derecha", acepta solo las 2 puntas del puente. Acomodo: `Acomodo_0` (7.452, 1.3775, 0.2255) y `Acomodo_1` (7.470, 1.3755, 0.222).
  - `Terminal_Neutro_Der` ahora tiene `Acomodo_1` (7.488, 1.372, 0.215) para que el puente salga hacia la derecha por encima del hueco. Las 3 entradas del conector aceptan también las 2 puntas del puente. Se comprobó en edición con una vista previa del puente acomodado: baja del conector, pasa sobre el hueco y entra por el lado de adentro de la zapata (~9 cm de ruta). Ninguna boca ni punto de acomodo nuevo toca las cajas de colisión.
  - **Código**:
    - `StripWireStep.otherStrippers`: más puntas en el mismo paso (el progreso se promedia; al pelar una punta se oculta su `MarcaPelado` si está en `visibleDuringStep`).
    - `MeterTerminalStep.Connection.otherWires`: una conexión acepta cualquiera de varias puntas; cada punta llena solo una conexión. El puente usa dos conexiones con las mismas 2 puntas: conector (Izq/Centro/Der) y zapata (`Terminal_Puente`). Meter la segunda punta en el conector cuenta como conexión equivocada (`AlreadyFilled`) aunque la terminal la acepte. En Evaluación, una punta mal puesta solo llena una conexión (`ClaimedByOther`). `Pending()` prefiere la conexión con una punta metida sin apretar, para que la flecha y los avisos sigan a esa.
  - **Pasos nuevos** (en `LevelManager.steps` después de `14_conectar_tierra`):
    - `15_pelar_puente` (StripWireStep, puntas A y B): "Pela el puente de neutro". Aviso "Sostén el puente con la otra mano para poder jalar". Línea guía y marcador nuevo `Marcadores/Flecha_Puente` (copia de `Flecha_Cinta`, sigue el eslabón del centro).
    - `16_colocar_puente` (MeterTerminalStep, 2 conexiones): "Coloca el puente de neutro". Aviso de lugar equivocado: "Ese no es su lugar: el puente va del conector de neutro a la terminal de arriba a la derecha".
    - Sin narración todavía (textos en los pasos; audios en la etapa 4).
  - **Pendiente de probar en el visor**: agarrar y pelar un cable corto suelto en el piso (¿se mueve demasiado al jalar con las pinzas?); meter la punta en la zapata por el lado de adentro (el destornillador y la mano cerca del conector); cómo queda el puente al apretar el segundo extremo (todo el sobrante se reparte en ~9 cm).

- **Relevo para la etapa 3 (cables de carga, pasos 17–20)**:
  - Crear los cables igual que el puente: clonar eslabones y ancla del cable real (`Fase` para el rojo, `Neutro` para el negro) y dejar un extremo fijo (cinemático) oculto en el hueco trasero (centro 7.51, 1.34; el murete tiene hueco pasante y `Murete2_Colisiones`/`Medidor_Colisiones` tienen hueco de 6–6.4 cm). La punta pelable va en el otro extremo.
  - Terminales de carga: `GEO_Tuerca8` (abajo a la izquierda, x 7.613, y 1.309) y `GEO_Tuerca9` (abajo a la derecha, x 7.414). Por simetría con las de arriba, las bocas irían en x 7.594 (hacia +X) y 7.4328 (hacia −X), y ≈ 1.3075, z 0.2255 (confirmar con captura).
  - Un extremo fijo es cinemático, así que `MeterTerminal.BuildDress` lo encontrará como eslabón fijo si queda a ≤ 60 eslabones: cables de ~40 cm (unos 23 eslabones) sirven.

- **Etapa 3 (2026-09-28)**: sin código nuevo; todo es escena.
  - **Cables `Carga_Fase` (rojo) y `Carga_Neutro` (negro)** (raíces): clones de `Puente_Neutro` con 20 eslabones + 2 anclas (~38 cm, separación 0.0174). La punta pelable es el `StartAnchor`; el `EndAnchor` es el extremo fijo, a z 0.10 dentro del hueco del murete, y su `PuntaPelable` está **apagada**. Los 7 eslabones con z ≤ 0.207 (ancla + 6) son cinemáticos y tienen apagado su `HandGrabInstallationRoutine`; el resto (15, ~26 cm) es libre. Salen del hueco por sus orillas para no chocar con la bajada del neutro y la tierra de acometida (x 7.513–7.5265): fase en x 7.538 y neutro en x 7.492, y 1.34. En reposo cuelgan hacia su lado y quedan en el fondo de la base (fase x 7.566, neutro x 7.454, y 1.205, puntas en z ≈ 0.375), fuera del canal del tubo. `cableHandles` = los 15 eslabones libres; `WireEndAligner.neighbor` = primer eslabón. Rojo: `WireRender` y `Forro` con el material `red` de `Fase`. Comprobado: ningún eslabón libre toca las cajas de colisión y ninguna referencia apunta al puente.
  - **Terminales** (en `Medidor_Terminales`): `Terminal_Carga_Fase` (clon de `Terminal_Linea`; boca (7.594, 1.3068, 0.2255), entra hacia +X, tornillo `GEO_Tuerca8`, "la terminal de carga, abajo a la izquierda") y `Terminal_Carga_Neutro` (clon de `Terminal_Puente`; boca (7.4328, 1.3068, 0.2255), hacia −X, `GEO_Tuerca9`, "…abajo a la derecha"). Cada una acepta solo su punta. Acomodo de 3 puntos hacia el hueco: fase (7.575, 1.3068, 0.228) → (7.556, 1.314, 0.238) → (7.543, 1.330, 0.230), pasa **por delante** de la bajada de la fase de acometida (x 7.556, z 0.2255); neutro (7.452, 1.3068, 0.2255) → (7.472, 1.318, 0.226) → (7.487, 1.332, 0.222). El `Acomodo_3` que traía el clon de la línea quedó apagado. Vista previa en edición: `BuildDress` encuentra el eslabón fijo 15 y reparte 14 eslabones en ~9 cm (separación ~0.6 cm); se ven como en la foto.
  - **Pasos nuevos** (en `LevelManager.steps` antes de `22_cerrar_registro`; sin narración):
    - `17_pelar_fase_carga` / `18_pelar_neutro_carga` (clones de `09_pelar_fase`): "Pela la fase de carga" / "Pela el neutro de carga". Visibles: `Flecha_Pinzas` y la `MarcaPelado` de su punta. El aviso de sostener el cable reusa el texto del rojo / negro de acometida, así que ya tiene audio.
    - `19_conectar_fase_carga` / `20_conectar_neutro_carga` (clones de `12_conectar_fase_acometida`, con `Flecha_Terminal`): avisos de lugar equivocado "Ese no es su lugar: la fase de carga va en la terminal de abajo a la izquierda" / "…el neutro de carga va en la terminal de abajo a la derecha".
  - `22_cerrar_registro` conserva su nombre; con el tirón de prueba (etapa 4) queda como paso 22. Hoy el nivel tiene 21 pasos.
  - **Pendiente de probar en el visor**: que los cables de carga caigan y se queden en el fondo de la base sin temblar ni meterse al tubo; que se puedan sacar hacia el frente para pelarlos (con 26 cm libres la punta llega a z ≈ 0.45); meter la punta en las zapatas de abajo (la mano y el destornillador cerca del cable de fase de acometida); cómo se ve el acomodo al apretar.

- **Ajuste "demasiados cables" (2026-09-28, tras probar en el visor)**: el usuario agarraba el cable equivocado y se le desconectaban, y le resultaba estresante. Causas: todos los eslabones libres de todos los cables se podían agarrar (5 cables en ~30 cm en los pasos 12–14, esferas de agarre de 3 cm); una punta metida sin apretar se salía al agarrar su cable a ≤ 21 cm y estirarlo 4 cm; y el destornillador se acoplaba a cualquier tornillo a ≤ 3 cm, incluso uno ya apretado, y girar a la izquierda lo aflojaba. El usuario eligió (para Práctica y Evaluación):
  - **Solo el cable del paso se agarra**: `WireGrab` (nuevo, en la raíz de cada cable con punta pelable: `Fase`, `Neutro`, `Tierra`, `Puente_Neutro`, `Carga_Fase`, `Carga_Neutro`). `WireGrab.Only(puntas, ocupantes)` apaga el objeto `[BuildingBlock] HandGrabInstallationRoutine` de los eslabones de los demás cables (el `enabled` de los componentes lo siguen usando `WireTip.Pin` y `ConduitPathGuide`, así que no chocan). Los hijos de agarre que empiezan apagados (eslabones fijos de los cables de carga) nunca se prenden. Lo llaman `StripWireStep` y `MeterTerminalStep` en `Begin`; en `End` va `WireGrab.Free()`. Los pasos 1–8 y 22 no limitan nada. `MeterTerminalStep` también deja agarrar el cable que ocupe una de sus terminales (en Evaluación puede haber quedado ahí uno equivocado).
  - **Resaltar**: el cable del paso pulsa hacia ámbar (`_BaseColor` con `MaterialPropertyBlock` en su `WireRender`) y se ilumina fijo cuando una mano está en hover o lo sostiene (`IInteractableView.State`).
  - **Cables de carga ocultos hasta el paso 17**: `TutorialStep.showFromStep` (nuevo) activa objetos al empezar el paso y los deja activos. `Carga_Fase` y `Carga_Neutro` empiezan apagados en la escena y los activa `17_pelar_fase_carga`.
  - **Bloquear lo ya conectado**: `TerminalScrew.Locked` se activa al apretar una terminal con su cable correcto (`MeterTerminal.Tighten`). Un tornillo bloqueado no gira y el destornillador no se acopla a él; si estaba acoplado, se suelta. Tras soltar uno bloqueado, no se acopla a otro hasta alejar la punta 2 cm más allá del radio de acople (`awayDistance`), porque los tornillos del conector están a 1.35 cm entre sí. Los tornillos con un cable equivocado se pueden seguir aflojando para corregir.
  - Para el tirón de prueba (etapa 4): las conexiones correctas ya no se pueden aflojar, así que el tirón siempre aguanta.

- **Relevo para la etapa 4 (tirón de prueba, errores, narración)**:
  - Los cables para `TugTestStep` son 7 puntas: fase, neutro y tierra de acometida (`StartAnchor` de `Fase`/`Neutro`/`Tierra`), las 2 del puente (una por cada terminal donde quede) y las 2 de carga (`StartAnchor` de `Carga_Fase`/`Carga_Neutro`). Ya apretadas, sus eslabones cercanos están fijos (cinemáticos y sin agarre), así que el tirón debe medirse agarrando el primer eslabón libre después del acomodo, o hay que dejar agarrable un eslabón del acomodo.
  - Crear el paso 21 antes de `22_cerrar_registro`; los errores de cada `MeterTerminalStep` están en `WrongConnections`.

---

## Reglas de trabajo (importantes)

- **Nunca entrar a Play mode desde el MCP de Unity**: truena Unity. Se prueba en modo edición (compilar, revisar la jerarquía, ejecutar código de editor). El usuario prueba en el visor (Quest).
- **No borrar objetos de la escena**: solo desactivarlos (el clasificador de permisos bloqueó borrados antes).
- Escena del nivel: `Assets/Scenes/SampleScene.unity`. Flujo: UIMenu → TutorialScene → SampleScene.
- Código en el estilo del proyecto: comentarios de tooltip y resúmenes en español, poca densidad de comentarios, `TutorialStep` como base de los pasos.
- Textos de UI y narración en español de México.

---

## Decisiones del usuario (2026-09-28)

1. **Topología como en la imagen**:
   - Fase de acometida → terminal de **línea arriba a la izquierda**.
   - Neutro de acometida → **conector neutro central**.
   - Tierra → también al **conector neutro central** (unión neutro-tierra).
   - **Puente de neutro** del conector central → mordaza **arriba a la derecha**.
2. **El jugador también conecta el lado de carga** (fase y neutro hacia la casa) y coloca el puente.
3. **El jugador pela todo**: las 3 puntas de la acometida (ya existe), las 2 puntas del puente y las 2 de carga.
4. **Fijación con destornillador**: meter la punta pelada y luego apretar el tornillo. Sin apretar, el cable se sale al jalarlo.
5. **Cualquier terminal acepta cualquier cable**. En Práctica se avisa y se pide corregir; en Evaluación se registra como error.
6. **No hay paso para poner el medidor de vidrio**; eso lo hace la CFE. El nivel termina con la base cableada y el registro cerrado.
7. **Los cables de carga salen por atrás** a la pared: la base y el murete tienen un hueco pasante.
8. **Narración**: Claude escribe los textos de los pasos y avisos nuevos; el usuario genera los audios con la voz de Sabina. Mientras tanto, esos pasos quedan sin audio.
9. **Orden de pasos aprobado** (ver abajo).

---

## Orden de los pasos (22)

Del 1 al 7 no cambian. El 8 (cortar el sobrante) se agregó después de la primera prueba en el visor.

| # | Objeto del paso | Tipo | Título | Qué hace el jugador |
|---|---|---|---|---|
| 1 | `01_abrir_registro` | LidStep | Abre el registro | (sin cambio) |
| 2 | `02_meter_guia` | GuideInsertStep | Mete la guía jalacables | (sin cambio) |
| 3–5 | `03/04/05_enganchar_*` | GuideHookStep | Engancha fase / neutro / tierra | (sin cambio) |
| 6 | `06_encintar_amarre` | GuideTapeStep | Encinta el amarre | (sin cambio) |
| 7 | `07_jalar_guia` | GuidePullStep | Jala la guía desde el registro | (sin cambio) |
| 8 | `08_cortar_sobrante` | CutWireStep | Corta el sobrante de los cables | Pinzas sobre la marca de cada cable (a ~50 cm de la salida del medidor) y gatillo |
| 9–11 | `09/10/11_pelar_*` | StripWireStep | Pela la punta de la fase / del neutro / de la tierra | (sin cambio) |
| 12 | `12_conectar_fase_acometida` | MeterTerminalStep | Conecta la fase de acometida | Meter la punta en la terminal de línea (arriba a la izquierda) y apretar |
| 13 | `13_conectar_neutro_acometida` | MeterTerminalStep | Conecta el neutro de acometida | Conector neutro central, meter y apretar |
| 14 | `14_conectar_tierra` | MeterTerminalStep | Conecta la tierra | Conector neutro central (unión neutro-tierra), meter y apretar |
| 15 | nuevo | StripWireStep ×2 puntas (o paso compuesto) | Pela el puente de neutro | Pelar las 2 puntas del tramo corto negro |
| 16 | nuevo | MeterTerminalStep (2 terminales) | Coloca el puente de neutro | Del conector central a la mordaza arriba a la derecha, apretar ambos |
| 17 | nuevo | StripWireStep | Pela la fase de carga | Pinzas sobre la marca |
| 18 | nuevo | StripWireStep | Pela el neutro de carga | Pinzas sobre la marca |
| 19 | nuevo | MeterTerminalStep | Conecta la fase de carga | Terminal abajo a la izquierda, meter y apretar |
| 20 | nuevo | MeterTerminalStep | Conecta el neutro de carga | Terminal abajo a la derecha, meter y apretar |
| 21 | nuevo | TugTestStep | Tirón de prueba | Jalar cada cable cerca de su terminal para comprobar que está firme |
| 22 | `22_cerrar_registro` | LidStep | Cierra el registro | (sin cambio) |

**Ojo:** en las secciones de etapas de abajo, los números de paso de las etapas 2–4 siguen con la numeración vieja (14–20); en la tabla de arriba son 15–21.

Los pasos viejos `10_conectar_fase`, `11_conectar_neutro` y `12_conectar_tierra` (PlugWireStep) se **desactivan** y se quitan de `LevelManager.steps`.

---

## Lo que hay en la escena (medido el 2026-09-28)

### Medidor

- `Casa/MedidorV2`, pos (7.51, 1.34, 0.29), sin rotación ni escala. El frente mira hacia **+Z**.
  - `GEO_Medidor`: carcasa (MeshCollider no convexo), bounds 0.42 × 0.44 × 0.27.
  - `GEEO_BasesPlasticas`: placa base, z ≈ 0.175–0.217.
  - `GEO_Mordazas`: las 4 mordazas en **una sola malla**, z ≈ 0.23.
  - `GEO_MetalCobre`: cobre, centro (7.513, 1.370, 0.207).
  - Tornillos de las zapatas (mallas separadas, se pueden animar):

    | Tornillo | Posición (centro de bounds) | Terminal |
    |---|---|---|
    | `GEO_Tuerca7` | (7.613, 1.379, 0.219) | Línea, arriba a la izquierda (vista del jugador) → **fase de acometida** |
    | `GEO_Tuerca10` | (7.414, 1.379, 0.219) | Arriba a la derecha → **puente de neutro** |
    | `GEO_Tuerca8` | (7.613, 1.309, 0.219) | Carga, abajo a la izquierda → **fase de carga** |
    | `GEO_Tuerca9` | (7.414, 1.309, 0.219) | Carga, abajo a la derecha → **neutro de carga** |
    | `GEO_Tuerca6` (+ `_Izq`, `_Der`) | (7.513, 1.416, 0.195) | **Conector neutro central** (naranja); `_Izq` está en x 7.5265 y `_Der` en x 7.4995 |
    | `GEO_Tuerca2` / `GEO_Tuerca3` | (7.513, 1.234 / 1.455, 0.179) | Tornillos de montaje (no se usan) |
    | `GEO_Tuerca`, `1`, `4`, `5` | esquinas, pequeños | Tornillos de las mordazas (no se usan) |

  - El tamaño de los tornillos de zapata es de unos 0.018 × 0.018 × 0.028. Las zapatas están a unos 20 cm entre sí en X.
  - Izquierda/derecha siempre desde la vista del jugador (mirando hacia −Z): la izquierda es la X mayor.
  - El pivote de cada `GEO_Tuerca` está en el centro de la cabeza, cerca de su cara de arriba, con Z hacia afuera; se puede girar sobre su Z local.
- `Medidor` (raíz, desactivado): modelo viejo, no tocar.
- **Hueco de abajo (acometida)**: el tubo `Casa/Tubo2` sube vertical en x = 7.51, z ≈ 0.29–0.31 y entra a la base por abajo (y ≈ 1.13). Los waypoints `Entrada_Fase_0..7`, `Entrada_Neutro_*` y `Entrada_Tierra_*` van del frente del medidor (z = 0.47, y ≈ 1.2) hacia adentro y abajo.
- **Hueco trasero (carga)**: pasante, de unos 6 cm de diámetro, centrado en (7.51, 1.34). Atraviesa la base y el murete `Casa/Murete2` (bounds z −0.16 … 0.38; el fondo del nicho está en z = 0.169).
- **Hueco de arriba**: existe en el modelo (y ≈ 1.55), no se usa.

### Objetos a desactivar

- `Bornes_Medidor` (raíz): `Borne_Fase`, `Borne_Neutro` y `Borne_Tierra` a y = 1.70, cubos con letras F/N/T.
- `Flecha_Bornes`: marcador con `Pulse` (ya está apagado, pero lo manejan los pasos viejos).
- Pasos `10_conectar_fase`, `11_conectar_neutro` y `12_conectar_tierra`.

### Cables existentes (WireBuilder)

- Raíces `Fase` (rojo), `Neutro` (**negro**) y `Tierra` (verde). Cada uno tiene `WireController`, `Rigidbody` y `Grabbable`; capa `Wire`, 389 segmentos, separación ≈ 0.0175 m.
  - Hijos: `WireRender` (`TubeRenderer`), `Plug(Clone)` (`PlugController`, apuntando a la borne vieja), `StartAnchor(Clone)` y `segment(Clone)` ×389 (cada uno con `HandGrabInteractable`), más un `EndAnchor(Clone)`.
  - **El extremo que se conecta al medidor es el `StartAnchor`**. Tiene `PuntaPelable` (`WireStripper` + `WireEndAligner`) con los hijos `Cobre`, `Forro` y `MarcaPelado`.
  - La punta que se mete por el tubo (el eslabón 0 de `ConduitPathGuide`) termina en el registro.
  - Después de jalar, el `StartAnchor` queda afuera del frente del medidor y **sobran unos 1.5 m** de cable colgando hacia el rollo (en el piso, cerca de x 8.0–8.9, z 1.12).
- `Guia` (raíz, desactivada en edición): guía jalacables, controlada por `PullGuide`.
- Para crear cables en edición se usa la API de `WireController` (bajo `#if UNITY_EDITOR`), en este orden: `SetPosition(pos)` → `AddStar()` → `SetPosition` → `AddSegment()` … → `AddEnd()`. El prefab `EndAnchor` **no** trae `PuntaPelable`; hay que duplicarlo desde un `StartAnchor`.

### Herramientas

- `GEO_Destornillador` (raíz): `Rigidbody`, `Grabbable`, `BoxCollider` y un `HandGrabInstallationRoutine` (`HandGrabInteractable`). **No tiene lógica todavía.** Hay que confirmar la orientación de su eje y dónde está la punta.
- `CFE_Pinzas`: pinzas para pelar, usadas por `WireStripper` (el gatillo aprieta).
- `GEO_CintaAislante`: la usa `GuideTape`.

### Código relevante

- `Assets/Scripts/tutorial/TutorialStep.cs`: base de los pasos (`title`, `body`, `narration`, `panelAnchor`, `highlightParts`, `worldTarget`, `pathPoints`, `visibleDuringStep`, `Progress`, `IsComplete`, `Begin`/`Tick`/`End`).
- `Assets/Scripts/tutorial/GuidedFlowManager.cs`: recorre `steps`, maneja el panel, la línea guía, la narración y los avisos hablados (`voiceCues`, que se buscan por el **texto exacto** del aviso).
- `Assets/Scripts/nivel_1/LevelManager.cs`: modos Práctica y Evaluación, y resultados (`LevelResultsStore`, `LevelStepResult`: `paso`, `segundos`, `avisos`).
- `Assets/Scripts/tutorial/TutorialContext.cs`: `ShowHint(text)` cuenta los avisos; en Evaluación los cuenta pero no los muestra. `GuidesVisible` indica si se ven las guías.
- `Assets/Scripts/tutorial/PlugWireStep.cs` y `Assets/WireBuilder/Scripts/PlugController.cs`: el sistema viejo de bornes (referencia de cómo fijar un ancla en modo cinemático).
- `Assets/Scripts/tutorial/StripWireStep.cs` y `WireStripper.cs`: el pelado (`IsStripped`, `Progress01`, `cableHandles`). El aviso por defecto `holdCableHint` dice "cable blanco"; hay que ajustarlo en cada paso nuevo.
- `Assets/WireBuilder/Scripts/GuiaController/GuideTape.cs`: patrón de giro acumulado (`SignedAngle` sobre un eje), vibración por cuarto de vuelta y `NearestHand()`. **Es la referencia para el destornillador.**
- `Assets/WireBuilder/Scripts/GuiaController/ConduitPathGuide.cs`: mueve el cable a lo largo del tubo (`FeedTo`, `Progress01`, `PathLength`). Puede servir para recoger el sobrante.
- Manos: `LeftControllerAnchor` y `RightControllerAnchor` (a los scripts se les pasan como `leftHand`/`rightHand`). Botones con `OVRInput` (gatillo: `PrimaryIndexTrigger`).

---

## Componentes nuevos (diseño)

Carpeta sugerida: `Assets/Scripts/nivel_1/Medidor/`.

### 1. `MeterTerminal` (zapata o cada entrada del conector)

- Campos:
  - `entry`: Transform; su posición es la boca y su `forward` es la dirección de inserción.
  - `screw`: `TerminalScrew`.
  - `expected`: qué punta es la correcta; una referencia al ancla, o al `WireStripper` de ese extremo.
  - `label`: nombre para los avisos, por ejemplo "la terminal de línea".
  - `acceptRadius` ≈ 0.03 y `maxAngle` ≈ 60°.
  - `pullOutDistance`: cuánto hay que jalar para sacar un cable sin apretar.
- Estados: `Empty` → `Inserted` → `Tightened`.
- Al entrar una punta:
  - Se detecta por trigger, o por distancia a las anclas registradas.
  - Si **no está pelada**, se rechaza y se avisa ("Pela la punta antes de conectarla").
  - Si está pelada: el ancla pasa a cinemática, el cobre queda dentro de la boca y el forro afuera, y se fuerza a la mano a soltarla.
  - Si es **otro cable**, se dispara `OnWrongWire`. En Práctica sale el aviso "Ese no es su lugar: …" y hay que sacarlo; en Evaluación se cuenta como error.
- `Inserted` sin apretar: si se agarra el cable cerca y se jala más de `pullOutDistance`, la punta se suelta (reemplaza a `disconnectDistance`).
- `Tightened`: el cable ya no se sale. Si se afloja el tornillo, regresa a `Inserted`.
- Eventos: `OnInserted`, `OnTightened`, `OnLoosened`, `OnRemoved`, `OnWrongWire`.

### 2. `TerminalScrew` + `Screwdriver`

- `TerminalScrew`:
  - Tiene el `head` (la malla `GEO_TuercaX`), su eje (el `forward` apunta hacia afuera, +Z del medidor) y `requiredTurns` ≈ 2.
  - Las vueltas se guardan en un rango de 0 a `requiredTurns`. Visualmente, el tornillo gira y baja unos milímetros.
  - `IsTight` es verdadero cuando las vueltas llegan a `requiredTurns`. Solo aprieta si la terminal tiene un cable metido; si no, gira en vacío hasta el tope.
- `Screwdriver` (en `GEO_Destornillador`):
  - Tiene una `tip` (Transform en la punta) y un eje del vástago.
  - **Montar**: la punta a menos de ~1.5 cm de la cabeza del tornillo y el vástago a menos de ~30° de su eje.
  - **Girar**: con el **gatillo presionado**, se acumula el giro de la mano alrededor del eje del vástago (`SignedAngle` de un vector radial del control, como en `GuideTape`). Girar a la derecha aprieta y a la izquierda afloja. Sin gatillo, la muñeca regresa sin girar el tornillo (efecto matraca).
  - Vibra cada cuarto de vuelta y con más fuerza al llegar al tope.
  - Expone `Engaged`, `CurrentScrew` y `LastTurnTime` para los avisos.

### 3. Acomodo del cable (`WireDresser`)

- Al apretar, el tramo de cable entre la entrada a la base (el hueco de abajo o el trasero) y la zapata se acomoda en una curva limpia: los eslabones pasan a cinemáticos y siguen una Bézier o puntos de ruta por terminal.
- **Cable sobrante de la acometida** (~1.5 m). Hay dos opciones y se prueba cuál se ve bien:
  - A) Recorrer el sobrante hacia el registro a lo largo de la ruta del tubo (con `ConduitPathGuide`).
  - B) Esconder los eslabones de sobra dentro del tubo.
  - Cuidado: puntos repetidos en `TubeRenderer` pueden dar normales inválidas.
- Riesgo: la cuerda física tiembla en un espacio de 25 cm; por eso el tramo acomodado va fijo.

### 4. `MeterTerminalStep : TutorialStep`

- Campos: `connections[]`, cada uno con una `MeterTerminal` y el extremo esperado (el puente usa 2), y `screwdriver`.
- `Progress`: acercar la punta (0–0.4 según la distancia) → metida (0.5) → apretada (0.5–1.0). Si hay varias conexiones, se promedia.
- `IsComplete`: todas las conexiones esperadas están `Tightened` con el cable correcto.
- Avisos (con `Context.ShowHint`):
  - "Pela la punta antes de conectarla"
  - "Ese no es su lugar: la fase va en la terminal de línea, arriba a la izquierda" (uno por cable)
  - "Toma el destornillador y aprieta el tornillo de la terminal"
  - "Mantén el gatillo y gira la muñeca a la derecha para apretar"
  - "Suelta el gatillo para regresar la muñeca"
  - "El cable se salió: vuelve a meterlo y aprieta el tornillo"
- Errores: lleva la cuenta de las conexiones incorrectas (`WrongConnections`) para los resultados.
- Guías en modo Práctica: un marcador que pulsa (`Pulse`) en la zapata correcta y después en su tornillo; la línea guía hacia el medidor.

### 5. `TugTestStep : TutorialStep`

- La lista de cables a probar: fase, neutro, tierra, puente, fase de carga y neutro de carga (6).
- Una prueba cuenta cuando una mano agarra el cable a menos de ~15 cm de su terminal y se aleja más de ~3 cm mientras lo sostiene.
- Si está apretado, el cable aguanta, vibra y cuenta.
- `Progress` = cables probados / total.

### 6. Resultados

- Agregar `errores` a `LevelStepResult` (y el total en `LevelAttempt`) en `LevelResultsStore.cs`.
- En el panel final de `LevelManager`, agregar la línea "Conexiones incorrectas: N".
- Los pasos obtienen sus errores con una interfaz opcional, por ejemplo `IStepErrors { int Errors { get; } }`, que el `LevelManager` lee en `OnStepCompleted`.

---

## Cambios en la escena

- **Conector neutro**: agregar 2 entradas más, cada una con su tornillo (copias escaladas de `GEO_Tuerca6`), para que quepan 3 conductores: neutro de acometida, tierra y puente.
- **Terminales**: crear un `MeterTerminal` por boca (7 en total).
  - Zapatas: línea izquierda, arriba a la derecha, carga izquierda y carga derecha.
  - Conector neutro: 3 entradas.
  - Direcciones de entrada: en las zapatas laterales, horizontal (desde el centro hacia afuera, como en la foto); en el conector central, vertical u horizontal según la geometría. Confirmarlo con capturas.
- **Puente de neutro**: cable nuevo con `WireController`, **negro**, de unos 30 cm, suelto junto a las herramientas, con `PuntaPelable` en **ambos** extremos.
- **Cables de carga**: fase **roja** y neutro **negro**, de unos 40 cm.
  - Un extremo fijo (cinemático) y oculto dentro del hueco trasero del murete (z ≈ 0.0–0.1).
  - El otro cuelga dentro de la base, con `PuntaPelable` sin pelar y su `MarcaPelado`.
- **Desactivar** `Bornes_Medidor`, `Flecha_Bornes` y los pasos viejos `10/11/12_conectar_*`.
- **Actualizar `LevelManager.steps`** con los 21 pasos en orden y renombrar los objetos de los pasos con numeración consecutiva.
- `panelAnchor`: reusar `PanelAnchors/Panel_Murete` o crear `Panel_Medidor` frente al medidor.

---

## Etapas de implementación

### Etapa 1: terminal, destornillador y pasos 11–13

1. Capturas de cerca de las zapatas y del conector para definir las bocas (`entry`) y el eje de los tornillos. Revisar la orientación y la punta de `GEO_Destornillador`.
2. Escribir `MeterTerminal`, `TerminalScrew`, `Screwdriver` y `MeterTerminalStep`.
3. Crear las terminales de: línea izquierda y las 3 entradas del conector (incluidos los 2 tornillos nuevos).
4. Crear los pasos 11–13, desactivar los viejos y actualizar `LevelManager.steps`.
5. Primera versión del acomodo del cable (aunque sea solo dejar el ancla fija y el tramo corto).
6. Compilar, revisar la consola y validar en modo edición.
7. **El usuario prueba en el visor** cómo se siente meter y apretar; ajustar radios, vueltas y ángulos.

### Etapa 2: puente de neutro (pasos 14–15)

1. Crear el cable del puente (negro, ~30 cm, puntas pelables en ambos extremos).
2. Crear la terminal de arriba a la derecha.
3. Paso 14: pelar las 2 puntas (un `StripWireStep` compuesto o dos strippers en un paso).
4. Paso 15: `MeterTerminalStep` con 2 conexiones.

### Etapa 3: cables de carga (pasos 16–19)

1. Crear los cables de carga desde el hueco trasero (extremo fijo oculto).
2. Crear las terminales de carga izquierda y derecha.
3. Pasos 16–17 (pelar) y 18–19 (conectar).

### Etapa 4: cierre

1. `TugTestStep` (paso 21).
2. Agregar `errores` a los resultados (JSON y panel).
3. Pulir el acomodo del cable y resolver el sobrante de la acometida.
4. Corregir "de el neutro" → "del neutro" en el paso 9.
5. Entregar los **textos de narración** de todos los pasos y avisos nuevos (tabla: objeto del paso o aviso → texto) para que el usuario genere los audios. Después, asignar los clips en `narration` y en `voiceCues`.

---

## Riesgos y cómo mitigarlos

- **Escala pequeña** (bocas de ~1 cm, zapatas a 20 cm): radios de enganche generosos (~3 cm), alineación tolerante y resaltado en Práctica.
- **Cuerda física en un espacio chico**: fijar (cinemático) el tramo acomodado.
- **Sobrante de 1.5 m de la acometida**: ver las opciones A y B del acomodo.
- **Soltar de la mano al asentar la punta**: forzar que el `Grabbable` o `HandGrabInteractable` del ancla suelte (desactivar y volver a activar el interactable).
- **Destornillador y muñeca**: la matraca con gatillo evita tener que girar la muñeca más de 180°.
