# Genera la narración del nivel 2 (caja de la pastilla) y sus avisos con Azure Speech.
# La llave y la región se leen de .env (DEMO_AZURE_SPEECH_KEY y DEMO_AZURE_SPEECH_REGION).
# Este archivo debe guardarse en UTF-8 con BOM: sin él, PowerShell 5.1 lee mal los acentos.
# Uso (desde la raíz del proyecto):
#   powershell -ExecutionPolicy Bypass -File .\generar_voces_nivel2.ps1
#   powershell -ExecutionPolicy Bypass -File .\generar_voces_nivel2.ps1 -Muestras es-MX-DaliaNeural,es-MX-JorgeNeural
#   powershell -ExecutionPolicy Bypass -File .\generar_voces_nivel2.ps1 -Solo 06_pelar_neutro_caja,07_conectar_neutro_caja
# Con -Muestras solo crea un WAV por voz en Muestras_Voces, sin tocar los audios del juego.
# Con -Solo solo regenera los pasos o avisos nombrados.
param(
    [string]$Voz = 'es-MX-Dalia:DragonHDLatestNeural',
    [string[]]$Muestras,
    [string[]]$Solo
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$carpetaPasos = Join-Path $raiz 'Assets\Audio\Nivel2'
$carpetaAvisos = Join-Path $carpetaPasos 'Avisos'
$carpetaMuestras = Join-Path $raiz 'Muestras_Voces'

$pasos = [ordered]@{
    '00_intro'                = 'El medidor ya está cableado. Ahora conecta los cables de carga en la caja de la pastilla, detrás del murete. Elige cómo quieres hacerlo.'
    '01_rodear_murete'        = 'La caja de la pastilla está del otro lado del murete. Sigue la línea del piso y párate frente a ella.'
    '02_quitar_tapa'          = 'Agarra la tapa con el grip, jálala hacia ti y déjala a un lado.'
    '03_montar_pastilla'      = 'Toma la pastilla termomagnética de la mesa. Acércala al centro de la caja y suéltala para que encaje.'
    '04_pelar_fase_caja'      = 'El cable rojo viene del medidor. Sácalo de la caja, coloca las pinzas sobre la marca de su punta, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    '05_conectar_fase_caja'   = 'Mete la punta pelada del cable rojo por arriba del bloque que alimenta la pastilla. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    '06_pelar_neutro_caja'    = 'El cable blanco también viene del medidor. Coloca las pinzas sobre la marca de su punta, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    '07_conectar_neutro_caja' = 'Mete la punta pelada del cable blanco por arriba de la barra de neutros, a la izquierda de la caja. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    '08_cerrar_caja'          = 'Toma la tapa, acércala al frente de la caja y suéltala para que se acomode.'
}

# El texto debe ser idéntico al del aviso en el juego, porque la voz se busca por ese texto.
$avisos = [ordered]@{
    'aviso_acercar_pastilla'  = 'Acerca la pastilla al centro de la caja antes de soltarla'
    'aviso_acercar_tapa'      = 'Acerca la tapa al frente de la caja antes de soltarla'
    'aviso_lugar_fase_caja'   = 'Ese no es su lugar: la fase va en el bloque de la pastilla'
    'aviso_lugar_neutro_caja' = 'Ese no es su lugar: el neutro va en la barra de neutros'
}

$entorno = @{}
foreach ($linea in Get-Content (Join-Path $raiz '.env')) {
    if ($linea -match '^\s*(?:export\s+)?([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$') { $entorno[$Matches[1]] = $Matches[2].Trim('"', "'") }
}
$llave = $entorno['DEMO_AZURE_SPEECH_KEY']
$region = $entorno['DEMO_AZURE_SPEECH_REGION']
if (-not $llave -or -not $region) { throw 'Faltan DEMO_AZURE_SPEECH_KEY o DEMO_AZURE_SPEECH_REGION en .env' }

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Sintetizar($voz, $texto, $ruta) {
    $seguro = [System.Security.SecurityElement]::Escape($texto)
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='es-MX'><voice name='$voz'>$seguro</voice></speak>"
    Invoke-WebRequest -UseBasicParsing -Method Post -Uri "https://$region.tts.speech.microsoft.com/cognitiveservices/v1" -Headers @{
        'Ocp-Apim-Subscription-Key' = $llave
        'X-Microsoft-OutputFormat'  = 'riff-24khz-16bit-mono-pcm'
        'User-Agent'                = 'UnityTesis'
    } -ContentType 'application/ssml+xml; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($ssml)) -OutFile $ruta
    Write-Host "Listo: $ruta"
}

function Generar($carpeta, $textos) {
    New-Item -ItemType Directory -Force $carpeta | Out-Null
    foreach ($nombre in $textos.Keys) {
        if ($Solo -and ($Solo -split ',') -notcontains $nombre) { continue }
        Sintetizar $Voz $textos[$nombre] (Join-Path $carpeta ($nombre + '.wav'))
    }
}

if ($Muestras) {
    New-Item -ItemType Directory -Force $carpetaMuestras | Out-Null
    # Con -File, la lista llega como un solo texto separado por comas.
    foreach ($voz in ($Muestras -split ',')) {
        $archivo = ($voz -replace '[^A-Za-z0-9-]', '_') + '.wav'
        Sintetizar $voz ($pasos['03_montar_pastilla'] + ' ' + $pasos['05_conectar_fase_caja']) (Join-Path $carpetaMuestras $archivo)
    }
    exit 0
}

Generar $carpetaPasos $pasos
Generar $carpetaAvisos $avisos
