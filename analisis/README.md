# Estadísticas del simulador

## 1. Antes de jugar
Cada alumno escribe su **número de cuenta (8 dígitos)** en el teclado del menú principal y lo confirma en la
pantalla siguiente. En el visor solo se muestran los últimos 4 dígitos. Para el siguiente alumno se usa **Cambiar**.
El número se borra al cerrar la app, así que hay que escribirlo otra vez al abrirla.

Los CSV guardan el número de cuenta completo, que es un dato personal: pide el consentimiento informado de los
alumnos, no compartas los CSV crudos y usa `--anonimizar` para todo lo que vaya a la tesis.

## 2. Copiar los datos del Quest
Conecta el visor por USB, acepta "Permitir acceso a los datos" dentro del visor y copia esta carpeta:

```
Quest > Almacenamiento interno compartido > Android > data > com.KamehouseO.VRElectricalSimulation > files > estadisticas
```

Si Windows no muestra la carpeta `Android/data`, usa Meta Quest Developer Hub (File Manager) o adb:

```
adb pull /sdcard/Android/data/com.KamehouseO.VRElectricalSimulation/files/estadisticas datos/2026-11-20
```

Puedes guardar varias copias (una por día) dentro de `datos/`: el script junta todo y quita repetidos.
No borres los CSV del visor; cada copia trae todo el historial.

## 3. Analizar
```
pip install -r requirements.txt
python analisis_estadisticas.py datos --salida resultados --anonimizar privado/claves_privadas.csv
```

Con `--anonimizar` cada número de cuenta se cambia por un código (A001, A002…) en todas las tablas y gráficas.
`claves_privadas.csv` guarda la equivalencia para que puedas cruzar los datos con calificaciones o
cuestionarios; guárdalo aparte y no lo compartas. Si ya existe, se reutiliza: cada alumno conserva su código
aunque vuelvas a correr el análisis con datos nuevos. Sin `--anonimizar` las tablas llevan el número de cuenta.

El script avisa si encuentra identificadores que no tienen 8 dígitos (por ejemplo, pruebas hechas en el editor).

Si abres un CSV con números de cuenta en Excel, impórtalo con Datos > Desde texto/CSV y marca la columna
`alumno` como texto; con doble clic Excel borra los ceros a la izquierda.

## Archivos que escribe el simulador
- `intentos.csv`: una fila por intento: alumno, sesión, nivel, voltaje (127/220), modo (Tutorial, Práctica, Evaluación),
  inicio, fin, segundos, pasos completados, avisos, errores y resultado (`completado`, `abandonado`, `saltado`).
- `pasos.csv`: una fila por paso de cada intento, con `terminado = 0` en el paso donde se quedó un intento abandonado.

Un intento que tiene pasos pero no tiene fila en `intentos.csv` (por ejemplo, porque se apagó el visor)
aparece en el análisis como `sin_cierre`.
