# Genera la narración del nivel 2 (caja de la pastilla) y sus avisos con la voz de Sabina.
# Mismo formato que los audios que ya hay: WAV 22050 Hz, 16 bits, mono, velocidad por defecto.
# Uso (desde la raíz del proyecto): powershell -ExecutionPolicy Bypass -File .\generar_voces_nivel2.ps1
Add-Type -AssemblyName System.Speech

$raiz = $PSScriptRoot
$carpetaPasos = Join-Path $raiz 'Assets\Audio\Nivel2'
$carpetaAvisos = Join-Path $carpetaPasos 'Avisos'

$pasos = [ordered]@{
    '00_intro'                = 'El medidor ya está cableado. Ahora conecta los cables de carga en la caja de la pastilla, detrás del murete. Elige cómo quieres hacerlo.'
    '01_rodear_murete'        = 'La caja de la pastilla está del otro lado del murete. Sigue la línea del piso y párate frente a ella.'
    '02_quitar_tapa'          = 'Agarra la tapa con el grip, jálala hacia ti y déjala a un lado.'
    '03_montar_pastilla'      = 'Toma la pastilla termomagnética del piso. Acércala al centro de la caja y suéltala para que encaje.'
    '04_pelar_fase_caja'      = 'El cable rojo viene del medidor. Sácalo de la caja, coloca las pinzas sobre la marca de su punta, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    '05_conectar_fase_caja'   = 'Mete la punta pelada del cable rojo por arriba del bloque que alimenta la pastilla. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    '06_pelar_neutro_caja'    = 'El cable negro también viene del medidor. Coloca las pinzas sobre la marca de su punta, aprieta el gatillo y jala hacia la punta. Sostén el cable con la otra mano.'
    '07_conectar_neutro_caja' = 'Mete la punta pelada del cable negro por arriba de la barra de neutros, a la izquierda de la caja. Luego aprieta su tornillo con el destornillador: mantén el gatillo y gira a la derecha.'
    '08_cerrar_caja'          = 'Toma la tapa, acércala al frente de la caja y suéltala para que se acomode.'
}

# El texto debe ser idéntico al del aviso en el juego, porque la voz se busca por ese texto.
$avisos = [ordered]@{
    'aviso_acercar_pastilla' = 'Acerca la pastilla al centro de la caja antes de soltarla'
    'aviso_acercar_tapa'     = 'Acerca la tapa al frente de la caja antes de soltarla'
    'aviso_lugar_fase_caja'  = 'Ese no es su lugar: la fase va en el bloque de la pastilla'
    'aviso_lugar_neutro_caja' = 'Ese no es su lugar: el neutro va en la barra de neutros'
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
Generar $carpetaAvisos $avisos
$synth.Dispose()
