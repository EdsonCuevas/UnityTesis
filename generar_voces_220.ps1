# Genera las voces nuevas de los niveles de 220 V (Nivel1_220 y Nivel2_220) con Azure Speech.
# Solo incluye los textos que cambian respecto a 127 V; lo demás reusa los audios de Nivel1 y Nivel2.
# La llave y la región se leen de .env (DEMO_AZURE_SPEECH_KEY y DEMO_AZURE_SPEECH_REGION).
# Este archivo debe guardarse en UTF-8 con BOM: sin él, PowerShell 5.1 lee mal los acentos.
# Uso (desde la raíz del proyecto):
#   powershell -ExecutionPolicy Bypass -File .\generar_voces_220.ps1
#   powershell -ExecutionPolicy Bypass -File .\generar_voces_220.ps1 -Solo Nivel1_220/04_enganchar_faseB
# Después, en Unity: menú Tesis > Asignar voces 220 V (asigna los WAV a los pasos y a los avisos).
param(
    [string]$Voz = 'es-MX-Dalia:DragonHDLatestNeural',
    [string[]]$Solo
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$carpetaAudio = Join-Path $raiz 'Assets\Audio'

# Pasos: el nombre del archivo es el del objeto del paso en la escena.
$pasos = [ordered]@{
    'Nivel1_220/00_intro'                    = 'Instala una acometida bifásica de 220 V desde el medidor hasta el registro: abre el registro, mete la guía jalacables, engancha las dos fases, el neutro y la tierra y jálalos por el tubo, pela sus puntas, conéctalas al medidor, coloca el puente de neutro de la quinta terminal, conecta los cables de carga y cierra el registro. Práctica: con guías y avisos. Evaluación: sin ayudas, se mide tu tiempo.'
    'Nivel1_220/04_enganchar_faseB'          = 'Ahora acerca la punta del cable negro a la punta de la guía, en la base del medidor, hasta que se enganche.'
    'Nivel1_220/08_jalar_guia_atasco'        = 'La guía se atoró. Ve al murete, agarra un cable justo debajo de donde entra al medidor y empújalo hacia arriba, hacia el medidor, con movimientos cortos. Los cuatro cables entran juntos.'
    'Nivel1_220/11_pelar_faseB'              = 'Ahora haz lo mismo con el cable negro: coloca las pinzas sobre la marca de su punta, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    'Nivel1_220/15_conectar_faseB_acometida' = 'Lleva la punta pelada del cable negro a la otra terminal de línea, arriba a la derecha, y métela por el lado de adentro. Luego apriétala con el destornillador: mantén el gatillo y gira la muñeca a la derecha.'
    'Nivel1_220/19a_puente_conector'         = 'Afloja otra vez el tornillo del conector de neutro y mete una punta del puente por abajo, junto al neutro y la tierra. Luego vuelve a apretarlo con el destornillador: mantén el gatillo y gira la muñeca a la derecha.'
    'Nivel1_220/19b_puente_quinta'           = 'Mete la otra punta del puente en la quinta terminal, la mordaza pequeña a media altura del lado izquierdo, por su lado de adentro. Esa terminal lleva el neutro al medidor bifásico. Aprieta su tornillo con el destornillador.'
    'Nivel1_220/23_meter_faseB_carga'        = 'Toma el cable negro largo que está en la mesa, por la punta que marca la flecha. Métela en el hueco del fondo de la base y empuja el cable por tramos, hasta que pase al otro lado del murete.'
    'Nivel1_220/24_pelar_faseB_carga'        = 'Ya pasó el cable negro. Coloca las pinzas sobre la marca de la punta que quedó de tu lado, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    'Nivel1_220/25_conectar_faseB_carga'     = 'Lleva la punta pelada del cable negro de carga a la terminal de abajo a la derecha y métela por el lado de adentro. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    'Nivel1_220/28_conectar_neutro_carga'    = 'Lleva la punta pelada del cable blanco de carga al conector de neutro, el bloque de cobre del centro. Afloja su tornillo, mete la punta por abajo junto a los otros cables y vuelve a apretarlo: mantén el gatillo y gira a la derecha.'
    'Nivel2_220/00_intro'                    = 'El medidor ya está cableado a 220 V. Ahora conecta las dos fases y el neutro de carga en la caja de la pastilla de dos polos, detrás del murete. Elige cómo quieres hacerlo.'
    'Nivel2_220/03_montar_pastilla'          = 'Toma la pastilla termomagnética de dos polos de la mesa y suéltala en el centro de la caja: se engancha abajo. Luego empuja con la mano su parte de arriba hacia el bloque hasta que haga clic.'
    'Nivel2_220/05_conectar_fase_caja'       = 'Mete la punta pelada del cable rojo por arriba de la zapata derecha del bloque que alimenta la pastilla. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    'Nivel2_220/06_pelar_faseB_caja'         = 'El cable negro es la otra fase que viene del medidor. Sácalo de la caja, coloca las pinzas sobre la marca de su punta, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    'Nivel2_220/07_conectar_faseB_caja'      = 'Mete la punta pelada del cable negro por arriba de la zapata izquierda del bloque que alimenta la pastilla. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
}

# Avisos: el texto debe ser idéntico al del juego, porque la voz se busca por ese texto.
# Se escribe un avisos.txt (archivo|texto) junto a los WAV para que Unity los asigne.
$avisos = [ordered]@{
    'Nivel1_220/Avisos/aviso_encintar_cuatro'    = 'Engancha los cuatro cables a la guía antes de encintar'
    'Nivel1_220/Avisos/aviso_atorada_cuatro'     = 'La guía está atorada: ve al murete y empuja los cuatro cables hacia el medidor'
    'Nivel1_220/Avisos/aviso_jalar_cuatro'       = 'Engancha los cuatro cables a la guía antes de jalar'
    'Nivel1_220/Avisos/aviso_sostener_negro'     = 'Sostén el cable negro con la otra mano para poder jalar'
    'Nivel1_220/Avisos/aviso_lugar_faseA'        = 'Ese no es su lugar: la fase A va en la terminal de línea, arriba a la izquierda'
    'Nivel1_220/Avisos/aviso_lugar_faseB'        = 'Ese no es su lugar: la fase B va en la terminal de línea, arriba a la derecha'
    'Nivel1_220/Avisos/aviso_lugar_faseB_carga'  = 'Ese no es su lugar: la fase B de carga va en la terminal de abajo a la derecha'
    'Nivel1_220/Avisos/aviso_lugar_neutro_carga' = 'Ese no es su lugar: el neutro de carga va en el conector de neutro'
    'Nivel1_220/Avisos/aviso_lugar_puente_quinta' = 'Ese no es su lugar: el puente va del conector de neutro a la quinta terminal'
    'Nivel1_220/Avisos/aviso_lugar_puente_conector' = 'Ese no es su lugar: primero mete una punta del puente en el conector de neutro'
    'Nivel1_220/Avisos/aviso_lugar_puente_otra' = 'Ese no es su lugar: la otra punta del puente va en la quinta terminal'
    'Nivel2_220/Avisos/aviso_sostener_negro'     = 'Sostén el cable negro con la otra mano para poder jalar'
    'Nivel2_220/Avisos/aviso_lugar_faseA_caja'   = 'Ese no es su lugar: la fase A va en la zapata derecha del bloque'
    'Nivel2_220/Avisos/aviso_lugar_faseB_caja'   = 'Ese no es su lugar: la fase B va en la zapata izquierda del bloque'
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
    New-Item -ItemType Directory -Force (Split-Path $ruta) | Out-Null
    $seguro = [System.Security.SecurityElement]::Escape($texto)
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='es-MX'><voice name='$Voz'>$seguro</voice></speak>"
    Invoke-WebRequest -UseBasicParsing -Method Post -Uri "https://$region.tts.speech.microsoft.com/cognitiveservices/v1" -Headers @{
        'Ocp-Apim-Subscription-Key' = $llave
        'X-Microsoft-OutputFormat'  = 'riff-24khz-16bit-mono-pcm'
        'User-Agent'                = 'UnityTesis'
    } -ContentType 'application/ssml+xml; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($ssml)) -OutFile $ruta
    Write-Host "Listo: $ruta"
}

function Generar($textos) {
    foreach ($nombre in $textos.Keys) {
        if ($Solo -and ($Solo -split ',') -notcontains $nombre) { continue }
        Sintetizar $textos[$nombre] (Join-Path $carpetaAudio ($nombre + '.wav'))
    }
}

Generar $pasos
Generar $avisos

# Lista de avisos por nivel (archivo|texto), para el asignador de Unity.
foreach ($nivel in 'Nivel1_220', 'Nivel2_220') {
    $lineas = foreach ($nombre in $avisos.Keys) {
        if ($nombre.StartsWith("$nivel/")) { (Split-Path $nombre -Leaf) + '|' + $avisos[$nombre] }
    }
    $ruta = Join-Path $carpetaAudio "$nivel\Avisos\avisos.txt"
    New-Item -ItemType Directory -Force (Split-Path $ruta) | Out-Null
    [IO.File]::WriteAllLines($ruta, [string[]]$lineas, (New-Object Text.UTF8Encoding $false))
}
