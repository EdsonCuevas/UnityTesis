# Genera la narración de los pasos nuevos del medidor (nivel 1) y sus avisos con la voz de Sabina (Cortana).
# Mismo formato que los audios que ya hay: WAV 22050 Hz, 16 bits, mono, velocidad por defecto.
# Uso (desde la raíz del proyecto): powershell -ExecutionPolicy Bypass -File .\generar_voces_medidor.ps1
# Con -SoloPasos genera solo la narración de los pasos, sin los avisos.
param([switch]$SoloPasos)

Add-Type -AssemblyName System.Speech

$raiz = $PSScriptRoot
$carpetaPasos = Join-Path $raiz 'Assets\Audio\Nivel1'
$carpetaAvisos = Join-Path $carpetaPasos 'Avisos'

$pasos = [ordered]@{
    '08_cortar_sobrante'           = 'Deja unos 40 centímetros de cada cable desde donde sale del tubo, abajo del medidor. Toma las pinzas, pon sus quijadas sobre la marca de cada cable y aprieta el gatillo para cortar. El tramo que sobra se retira.'
    '12_conectar_fase_acometida'   = 'Lleva la punta pelada del cable rojo a la terminal de línea, arriba a la izquierda, y métela por el lado de adentro. Luego toma el destornillador, apóyalo en el tornillo de esa terminal, mantén el gatillo y gira la muñeca a la derecha hasta que apriete.'
    '13_conectar_neutro_acometida' = 'Mete la punta pelada del cable negro por abajo del conector de neutro, el bloque de cobre del centro. Después aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    '14_conectar_tierra'           = 'La tierra también va al conector de neutro, porque ahí se unen el neutro y la tierra. Mete la punta pelada del cable verde por abajo, en una entrada libre, y aprieta su tornillo.'
    '15_pelar_puente'              = 'Toma el cable negro corto que está en el piso, junto al destornillador. Pela sus dos puntas: coloca las pinzas sobre la marca, aprieta el gatillo y jala hacia la punta mientras sostienes el cable con la otra mano.'
    '16_colocar_puente'            = 'Mete una punta del puente por abajo del conector de neutro, en la entrada libre, y la otra en la terminal de arriba a la derecha, por el lado de adentro. Aprieta los dos tornillos con el destornillador.'
    '17_pelar_fase_carga'          = 'El cable rojo que sale por el hueco de atrás de la base va hacia la casa. Sácalo hacia ti, coloca las pinzas sobre la marca de su punta, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    '18_pelar_neutro_carga'        = 'El cable negro que sale por el hueco de atrás de la base va hacia la casa. Sácalo hacia ti, coloca las pinzas sobre la marca de su punta, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    '19_conectar_fase_carga'       = 'Lleva la punta pelada del cable rojo de carga a la terminal de abajo a la izquierda y métela por el lado de adentro. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    '20_conectar_neutro_carga'     = 'Lleva la punta pelada del cable negro de carga a la terminal de abajo a la derecha y métela por el lado de adentro. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    '21_tiron_de_prueba'           = 'Comprueba que cada conexión quedó firme: agarra con el grip cada cable cerca de su terminal y jálalo un poco hacia ti. Una conexión bien apretada no se mueve.'
}

# El texto debe ser idéntico al del aviso en el juego, porque la voz se busca por ese texto.
$avisos = [ordered]@{
    'aviso_cortar_sobre_marca'    = 'Ahí no: corta sobre la marca, a unos 40 centímetros de donde sale el cable del tubo'
    'aviso_quijadas_marca'        = 'Pon las quijadas de las pinzas sobre la marca del cable y aprieta el gatillo'
    'aviso_sostener_puente'       = 'Sostén el puente con la otra mano para poder jalar'
    'aviso_pelar_antes'           = 'Pela la punta antes de conectarla'
    'aviso_aflojar_tornillo'      = 'Afloja el tornillo de la terminal para poder meter el cable'
    'aviso_cable_se_salio'        = 'El cable se salió: vuelve a meterlo y aprieta el tornillo'
    'aviso_tomar_destornillador'  = 'Toma el destornillador y aprieta el tornillo de la terminal'
    'aviso_apoyar_destornillador' = 'Apoya la punta del destornillador en el tornillo, de frente'
    'aviso_girar_muneca'          = 'Mantén el gatillo y gira la muñeca a la derecha para apretar'
    'aviso_soltar_gatillo'        = 'Suelta el gatillo para regresar la muñeca'
    'aviso_lugar_equivocado'      = 'Ese no es su lugar: saca el cable y revisa a dónde va'
    'aviso_lugar_fase'            = 'Ese no es su lugar: la fase va en la terminal de línea, arriba a la izquierda'
    'aviso_lugar_neutro'          = 'Ese no es su lugar: el neutro va en el conector de neutro, al centro'
    'aviso_lugar_tierra'          = 'Ese no es su lugar: la tierra va en el conector de neutro, al centro'
    'aviso_lugar_puente'          = 'Ese no es su lugar: el puente va del conector de neutro a la terminal de arriba a la derecha'
    'aviso_lugar_fase_carga'      = 'Ese no es su lugar: la fase de carga va en la terminal de abajo a la izquierda'
    'aviso_lugar_neutro_carga'    = 'Ese no es su lugar: el neutro de carga va en la terminal de abajo a la derecha'
    'aviso_tiron_agarrar'         = 'Agarra con el grip cada cable cerca de su terminal y jálalo hacia ti'
    'aviso_tiron_acercar'         = 'Acerca la mano al cable que brilla, junto a su terminal'
    'aviso_tiron_jalar'           = 'Jala el cable unos centímetros hacia ti'
}

$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $synth.SelectVoice('Microsoft Sabina Desktop')
} catch {
    Write-Error "No se encontró la voz 'Microsoft Sabina Desktop'. Voces instaladas:"
    $synth.GetInstalledVoices() | ForEach-Object { Write-Host ' -' $_.VoiceInfo.Name }
    exit 1
}

$formato = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(
    22050,
    [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,
    [System.Speech.AudioFormat.AudioChannel]::Mono)

function Generar($carpeta, $textos) {
    New-Item -ItemType Directory -Force $carpeta | Out-Null
    foreach ($nombre in $textos.Keys) {
        $ruta = Join-Path $carpeta ($nombre + '.wav')
        $synth.SetOutputToWaveFile($ruta, $formato)
        $synth.Speak($textos[$nombre])
        $synth.SetOutputToNull()
        Write-Host "Listo: $ruta"
    }
}

Generar $carpetaPasos $pasos
if (-not $SoloPasos) { Generar $carpetaAvisos $avisos }
$synth.Dispose()
