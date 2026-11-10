# Regenera las voces del tutorial y del nivel 1 con Azure Speech, igual que el nivel 2.
# Reemplaza cada WAV en su lugar: conserva su .meta, así que las escenas no cambian.
# La llave y la región se leen de .env (DEMO_AZURE_SPEECH_KEY y DEMO_AZURE_SPEECH_REGION).
# Este archivo debe guardarse en UTF-8 con BOM: sin él, PowerShell 5.1 lee mal los acentos.
# El texto de los avisos debe ser idéntico al del juego, porque la voz se busca por ese texto.
# Uso (desde la raíz del proyecto):
#   powershell -ExecutionPolicy Bypass -File .\generar_voces_tutorial_nivel1.ps1
#   powershell -ExecutionPolicy Bypass -File .\generar_voces_tutorial_nivel1.ps1 -Solo Tutorial/12_final,Nivel1/00_intro
param(
    [string]$Voz = 'es-MX-Dalia:DragonHDLatestNeural',
    [string[]]$Solo
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$carpetaAudio = Join-Path $raiz 'Assets\Audio'

$textos = [ordered]@{
    'Nivel1/00_intro' = 'Instala la acometida desde el medidor hasta el registro: abre el registro, mete la guía jalacables, engancha la fase, el neutro y la tierra y jálalos por el tubo, pela sus puntas, conéctalas al medidor y cierra el registro. Práctica: con guías y avisos. Evaluación: sin ayudas, se mide tu tiempo.'
    'Nivel1/01_abrir_registro' = 'La tapa del registro es pesada: sujétala de las agarraderas con las dos manos, usando el grip, levántala y déjala a un lado para dejar libre la salida del tubo.'
    'Nivel1/02_meter_guia' = 'Toma la punta de la guía de su carrete con el grip, suéltala junto a la salida del tubo dentro del registro y empújala con movimientos cortos hasta que llegue al medidor.'
    'Nivel1/03_enganchar_fase' = 'La punta de la guía asoma en la base del medidor. Acerca la punta del cable rojo a ella hasta que se enganche.'
    'Nivel1/04_enganchar_neutro' = 'La punta de la guía asoma en la base del medidor. Acerca la punta del cable blanco a ella hasta que se enganche.'
    'Nivel1/05_enganchar_tierra' = 'La punta de la guía asoma en la base del medidor. Acerca la punta del cable verde a ella hasta que se enganche.'
    'Nivel1/05b_encintar_amarre' = 'Toma el rollo de cinta aislante de tu cinto, en la cadera izquierda, acércalo a la cabeza de la guía donde están enganchados los cables y da vueltas con él alrededor del amarre, siempre en el mismo sentido, hasta cubrirlo.'
    'Nivel1/06_jalar_guia' = 'Agarra la guía donde sale del tubo en el registro y jálala hacia ti. Suelta y vuelve a agarrarla cerca del tubo; en las curvas jala más despacio. Si se atora, ve al murete y empuja los cables hacia el medidor.'
    'Nivel1/06_jalar_guia_atasco' = 'La guía se atoró. Ve al murete, agarra un cable justo debajo de donde entra al medidor y empújalo hacia arriba, hacia el medidor, con movimientos cortos. Los tres cables entran juntos.'
    'Nivel1/06_jalar_guia_destrabe' = '¡Se destrabó! Regresa al registro y sigue jalando'
    'Nivel1/07_pelar_fase' = 'Toma las pinzas, colócalas sobre la marca del otro extremo del cable rojo, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    'Nivel1/08_cortar_sobrante' = 'Deja unos 40 centímetros de cada cable desde donde sale del tubo, abajo del medidor. Toma las pinzas de tu cinto, en la cadera derecha, pon sus quijadas sobre la marca de cada cable y aprieta el gatillo para cortar. El tramo que sobra se retira.'
    'Nivel1/08_pelar_neutro' = 'Toma las pinzas, colócalas sobre la marca del otro extremo del cable blanco, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    'Nivel1/09_pelar_tierra' = 'Toma las pinzas, colócalas sobre la marca del otro extremo del cable verde, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    'Nivel1/10_conectar_fase' = 'Lleva la punta pelada del cable rojo al borne marcado con F.'
    'Nivel1/11_conectar_neutro' = 'Lleva la punta pelada del cable blanco al borne marcado con N.'
    'Nivel1/12_conectar_fase_acometida' = 'Lleva la punta pelada del cable rojo a la terminal de línea, arriba a la izquierda, y métela por el lado de adentro. Luego toma el destornillador de tu cinto, en la cadera derecha, apóyalo en el tornillo de esa terminal, mantén el gatillo y gira la muñeca a la derecha hasta que apriete.'
    'Nivel1/12_conectar_tierra' = 'Lleva la punta pelada del cable verde al borne marcado con T.'
    'Nivel1/13_cerrar_registro' = 'Con las dos manos, coloca la tapa de nuevo sobre el registro y suéltala para que se acomode.'
    'Nivel1/13_conectar_neutro_acometida' = 'Mete la punta pelada del cable blanco por abajo del conector de neutro, el bloque de cobre del centro. Después aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    'Nivel1/14_conectar_tierra' = 'La tierra también va al conector de neutro, porque ahí se unen el neutro y la tierra. Primero afloja su tornillo con el destornillador: mantén el gatillo y gira la muñeca a la izquierda. Mete la punta pelada del cable verde por abajo, junto al neutro, y vuelve a apretar el tornillo.'
    'Nivel1/14_nivel_completado' = '¡Nivel completado!'
    'Nivel1/15_pelar_puente' = 'Toma el cable blanco corto que está en la mesa, junto al murete. Pela sus dos puntas: coloca las pinzas sobre la marca, aprieta el gatillo y jala hacia la punta mientras sostienes el cable con la otra mano.'
    'Nivel1/16_colocar_puente' = 'Afloja otra vez el tornillo del conector de neutro y mete una punta del puente por abajo, junto al neutro y la tierra. Mete la otra en la terminal de arriba a la derecha, por el lado de adentro. Aprieta los dos tornillos con el destornillador.'
    'Nivel1/17_meter_fase_carga' = 'Ahora sigue el cable que va hacia la casa. Toma el cable rojo largo que está en la mesa, por la punta que marca la flecha. Métela en el hueco del fondo de la base y empuja el cable por tramos, hasta que pase al otro lado del murete.'
    'Nivel1/17_pelar_fase_carga' = 'Ya pasó el cable rojo. Coloca las pinzas sobre la marca de la punta que quedó de tu lado, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    'Nivel1/18_pelar_neutro_carga' = 'Ya pasó el cable blanco. Coloca las pinzas sobre la marca de la punta que quedó de tu lado, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    'Nivel1/19_conectar_fase_carga' = 'Lleva la punta pelada del cable rojo de carga a la terminal de abajo a la izquierda y métela por el lado de adentro. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    'Nivel1/20_conectar_neutro_carga' = 'Lleva la punta pelada del cable blanco de carga a la terminal de abajo a la derecha y métela por el lado de adentro. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    'Nivel1/20_meter_neutro_carga' = 'Toma el cable blanco largo que está en la mesa, por la punta que marca la flecha. Métela en el hueco del fondo de la base y empuja el cable por tramos, hasta que pase al otro lado del murete.'
    'Nivel1/21_tiron_de_prueba' = 'Comprueba que cada conexión quedó firme: agarra con el grip cada cable cerca de su terminal y jálalo un poco hacia ti. Una conexión bien apretada no se mueve.'
    'Nivel1/Avisos/aviso_acercar_rollo' = 'Acerca el rollo a la cabeza de la guía, donde están enganchados los cables'
    'Nivel1/Avisos/aviso_aflojar_tornillo' = 'Afloja el tornillo de la terminal para poder meter el cable'
    'Nivel1/Avisos/aviso_aflojar_girar' = 'Mantén el gatillo y gira la muñeca a la izquierda para aflojar'
    'Nivel1/Avisos/aviso_agarrar_mas_arriba' = 'Agarra el cable más arriba, justo debajo de donde entra al medidor'
    'Nivel1/Avisos/aviso_apoyar_destornillador' = 'Apoya la punta del destornillador en el tornillo, de frente'
    'Nivel1/Avisos/aviso_cable_no_alcanza' = 'Un cable ya no alcanza: acerca su rollo al medidor'
    'Nivel1/Avisos/aviso_cable_resbalo' = 'El cable se resbaló: empújalo más despacio'
    'Nivel1/Avisos/aviso_cable_se_salio' = 'El cable se salió: vuelve a meterlo y aprieta el tornillo'
    'Nivel1/Avisos/aviso_carga_apuntar' = 'Apunta la punta del cable hacia el hueco del fondo de la base'
    'Nivel1/Avisos/aviso_carga_mas_cerca' = 'Sujeta el cable más cerca de la entrada para empujarlo'
    'Nivel1/Avisos/aviso_carga_no_alcanza' = 'Ya no alcanza el cable: acerca el resto al tubo'
    'Nivel1/Avisos/aviso_carga_soltar_resto' = 'Deja libre el resto del cable: se arrastra solo al empujar'
    'Nivel1/Avisos/aviso_carga_otra_punta' = 'Esa punta se queda de este lado: mete la otra por el hueco'
    'Nivel1/Avisos/aviso_carga_resbalo' = 'El cable se resbaló: empuja más despacio en las curvas'
    'Nivel1/Avisos/aviso_cortar_sobre_marca' = 'Ahí no: corta sobre la marca, a unos 40 centímetros de donde sale el cable del tubo'
    'Nivel1/Avisos/aviso_dar_vueltas' = 'Da vueltas con el rollo alrededor del amarre, siempre en el mismo sentido'
    'Nivel1/Avisos/aviso_encintar_antes_de_jalar' = 'Encinta el amarre de los cables antes de jalar la guía'
    'Nivel1/Avisos/aviso_enganchar_antes_de_encintar' = 'Engancha los tres cables a la guía antes de encintar'
    'Nivel1/Avisos/aviso_enganchar_antes_de_jalar' = 'Engancha los tres cables a la guía antes de jalar'
    'Nivel1/Avisos/aviso_extremo_equivocado' = 'Ese extremo va al medidor: engancha la otra punta del cable'
    'Nivel1/Avisos/aviso_girar_muneca' = 'Mantén el gatillo y gira la muñeca a la derecha para apretar'
    'Nivel1/Avisos/aviso_guia_atorada' = 'La guía está atorada: ve al murete y empuja los tres cables hacia el medidor'
    'Nivel1/Avisos/aviso_guia_resbalo' = 'La guía se resbaló: jala más despacio en las curvas'
    'Nivel1/Avisos/aviso_lugar_equivocado' = 'Ese no es su lugar: saca el cable y revisa a dónde va'
    'Nivel1/Avisos/aviso_lugar_fase' = 'Ese no es su lugar: la fase va en la terminal de línea, arriba a la izquierda'
    'Nivel1/Avisos/aviso_lugar_fase_carga' = 'Ese no es su lugar: la fase de carga va en la terminal de abajo a la izquierda'
    'Nivel1/Avisos/aviso_lugar_neutro' = 'Ese no es su lugar: el neutro va en el conector de neutro, al centro'
    'Nivel1/Avisos/aviso_lugar_neutro_carga' = 'Ese no es su lugar: el neutro de carga va en la terminal de abajo a la derecha'
    'Nivel1/Avisos/aviso_lugar_puente' = 'Ese no es su lugar: el puente va del conector de neutro a la terminal de arriba a la derecha'
    'Nivel1/Avisos/aviso_tapa_dos_manos' = 'La tapa pesa mucho: usa las dos manos para levantarla'
    'Nivel1/Avisos/aviso_lugar_tierra' ='Ese no es su lugar: la tierra va en el conector de neutro, al centro'
    'Nivel1/Avisos/aviso_pelar_antes' = 'Pela la punta antes de conectarla'
    'Nivel1/Avisos/aviso_quijadas_marca' = 'Pon las quijadas de las pinzas sobre la marca del cable y aprieta el gatillo'
    'Nivel1/Avisos/aviso_soltar_gatillo' = 'Suelta el gatillo para regresar la muñeca'
    'Nivel1/Avisos/aviso_soltar_guia' = 'Suelta la punta de la guía junto a la salida del tubo, dentro del registro'
    'Nivel1/Avisos/aviso_sostener_cable_negro' = 'Sostén el cable blanco con la otra mano para poder jalar'
    'Nivel1/Avisos/aviso_sostener_cable_rojo' = 'Sostén el cable rojo con la otra mano para poder jalar'
    'Nivel1/Avisos/aviso_sostener_cable_verde' = 'Sostén el cable verde con la otra mano para poder jalar'
    'Nivel1/Avisos/aviso_sostener_puente' = 'Sostén el puente con la otra mano para poder jalar'
    'Nivel1/Avisos/aviso_suficiente_cable' = 'Ya empujaste suficiente cable: regresa al registro a jalar la guía'
    'Nivel1/Avisos/aviso_tiron_acercar' = 'Acerca la mano al cable que brilla, junto a su terminal'
    'Nivel1/Avisos/aviso_tiron_agarrar' = 'Agarra con el grip cada cable cerca de su terminal y jálalo hacia ti'
    'Nivel1/Avisos/aviso_tiron_jalar' = 'Jala el cable unos centímetros hacia ti'
    'Nivel1/Avisos/aviso_tomar_destornillador' = 'Toma el destornillador y aprieta el tornillo de la terminal'
    'Tutorial/01_bienvenida' = 'Te damos la bienvenida al simulador de instalaciones eléctricas. Antes de empezar, aprenderás a usar los controles. Apunta con el control al botón Comenzar y presiona el gatillo.'
    'Tutorial/02_comodidad' = 'Elige cómo quieres girar y si quieres una viñeta al moverte (ayuda a evitar el mareo). Apunta a cada opción y presiona el gatillo. Al terminar, elige Listo.'
    'Tutorial/03_caminar' = 'Mueve el joystick izquierdo para caminar. Sal por la puerta y sigue la línea del piso hasta el círculo brillante del patio.'
    'Tutorial/04_girar' = 'Mueve el joystick derecho hacia los lados para girar. Gira un poco hacia cada lado y termina mirando la puerta del cuarto.'
    'Tutorial/06_agacharse' = 'Hunde el joystick derecho para agacharte, como cuando revisas un contacto cerca del piso. Espera un momento y vuelve a hundirlo para levantarte.'
    'Tutorial/07_saltar' = 'Presiona el botón A para saltar.'
    'Tutorial/08_agarrar' = 'Camina hasta la mesa de trabajo, acerca tu mano al desarmador y mantén presionado el botón lateral grip para agarrarlo.'
    'Tutorial/08b_cinto' = 'Ahora guarda el desarmador en tu cinto: acércalo a tu cadera derecha hasta que la funda se ilumine y suelta el grip. Después sácalo con el grip y guárdalo otra vez. Así tendrás las manos libres durante el nivel.'
    'Tutorial/10_conectar' = 'Toma con el grip la punta del cable rojo y llévala al contacto del tablero hasta que encaje.'
    'Tutorial/10b_pelar' = 'Sostén la punta del cable blanco con una mano. Con la otra toma las pinzas con el grip, ponlas sobre la marca, mantén el gatillo y jala hacia la punta hasta que salga el forro y se vea el cobre.'
    'Tutorial/11_tubo' = 'Lleva la punta pelada del cable blanco a la entrada del tubo, apuntando hacia adentro. Cuando entre, sujeta el cable un poco más atrás y empújalo con movimientos cortos hasta el final. Si jalas, el cable sale.'
    'Tutorial/12_final' = 'Ya sabes caminar, girar, agacharte, saltar, agarrar herramientas, pelar cables y guiarlos. En el nivel usarás la guía jalacables para pasar los cables por el tubo.'
}

$entorno = @{}
foreach ($linea in Get-Content (Join-Path $raiz '.env')) {
    if ($linea -match '^\s*(?:export\s+)?([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$') { $entorno[$Matches[1]] = $Matches[2].Trim('"', "'") }
}
$llave = $entorno['DEMO_AZURE_SPEECH_KEY']
$region = $entorno['DEMO_AZURE_SPEECH_REGION']
if (-not $llave -or -not $region) { throw 'Faltan DEMO_AZURE_SPEECH_KEY o DEMO_AZURE_SPEECH_REGION en .env' }

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Sintetizar($texto, $ruta) {
    $seguro = [System.Security.SecurityElement]::Escape($texto)
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='es-MX'><voice name='$Voz'>$seguro</voice></speak>"
    # Se descarga aparte para no dejar un WAV a medias si Azure falla.
    $temporal = [IO.Path]::GetTempFileName()
    for ($intento = 1; ; $intento++) {
        try {
            Invoke-WebRequest -UseBasicParsing -Method Post -Uri "https://$region.tts.speech.microsoft.com/cognitiveservices/v1" -Headers @{
                'Ocp-Apim-Subscription-Key' = $llave
                'X-Microsoft-OutputFormat'  = 'riff-24khz-16bit-mono-pcm'
                'User-Agent'                = 'UnityTesis'
            } -ContentType 'application/ssml+xml; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($ssml)) -OutFile $temporal
            break
        } catch {
            if ($intento -ge 5) { throw }
            Start-Sleep -Seconds (10 * $intento)
        }
    }
    Move-Item -Force $temporal $ruta
    Write-Host "Listo: $ruta"
}

$nombres = if ($Solo) { $Solo -split ',' } else { @($textos.Keys) }
foreach ($nombre in $nombres) {
    Sintetizar $textos[$nombre] (Join-Path $carpetaAudio ($nombre + '.wav'))
}
