param([int]$Port = 5080)
$ErrorActionPreference = 'Stop'

function Assert-True($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Read-Message($Socket) {
    $buffer = New-Object byte[] 16384
    $stream = New-Object System.IO.MemoryStream
    $timeout = New-Object System.Threading.CancellationTokenSource
    $timeout.CancelAfter(15000)
    try {
        do {
            $result = $Socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $timeout.Token).GetAwaiter().GetResult()
            if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { throw 'Unerwartetes Verbindungsende' }
            $stream.Write($buffer, 0, $result.Count)
        } while (-not $result.EndOfMessage)
        return ([Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json)
    }
    finally { $timeout.Dispose(); $stream.Dispose() }
}

function Send-Command($Socket, [string]$Json) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Json)
    $null = $Socket.SendAsync([ArraySegment[byte]]::new($bytes), [System.Net.WebSockets.WebSocketMessageType]::Text,
        $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
}

function Read-Until($Socket, [string]$Type) {
    for ($i = 0; $i -lt 10000; $i++) {
        $message = Read-Message $Socket
        if ($message.type -eq 'error') { throw "Serverfehler: $($message.code)" }
        if ($message.type -eq $Type) { return $message }
    }
    throw "Nachricht $Type fehlt"
}

$clients = @([Net.WebSockets.ClientWebSocket]::new(), [Net.WebSockets.ClientWebSocket]::new())
try {
    $ids = @()
    foreach ($client in $clients) {
        $null = $client.ConnectAsync([Uri]"ws://localhost:$Port/ws/simulation", [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        $ready = Read-Message $client
        Assert-True ($ready.type -eq 'ready') 'ready fehlt'
        $ids += $ready.sessionId
        $clock = [Diagnostics.Stopwatch]::StartNew()
        Send-Command $client '{"type":"start","speed":60}'
        $started = Read-Until $client 'started'
        $event = Read-Until $client 'event'
        Assert-True ($event.data.eventTyp -eq 'betritt_klinik') 'Erstes Ereignis ist kein Klinikeintritt'
        Assert-True ($event.sequence -eq 1) 'Sequenz beginnt nicht bei 1'
        Assert-True ($event.runId -eq $started.runId) 'Falsche Lauf-ID'
        Assert-True ($event.data.patientId -eq 1) 'Erster Patient fehlt'
        # Bei Speed 60 entspricht eine Simulationsminute einer Echtzeitsekunde.
        Assert-True ($clock.Elapsed.TotalSeconds -ge ($event.data.zeit - 0.15)) 'Simulation rechnet vor der Live-Uhr voraus'
        Write-Output "Live-Patient empfangen: Sitzung $($ready.sessionId), Zeit $($event.data.zeit) min"
    }
    Assert-True ($ids[0] -ne $ids[1]) 'Sitzungen sind nicht getrennt'

    Send-Command $clients[0] '{"type":"stop"}'
    $stopped = Read-Until $clients[0] 'stopped'
    # Der andere Client muss weiterhin Ereignisse erhalten.
    $other = Read-Until $clients[1] 'event'
    Assert-True ($other.sessionId -eq $ids[1]) 'Sitzungsereignisse vermischt'
    Send-Command $clients[0] '{"type":"start","speed":600}'
    $restart = Read-Until $clients[0] 'started'
    Assert-True ($restart.runId -ne $stopped.runId) 'Neustart verwendet alte Lauf-ID'
    $firstAgain = Read-Until $clients[0] 'event'
    Assert-True ($firstAgain.sequence -eq 1) 'Neustart setzt Sequenz nicht zurueck'
    Send-Command $clients[0] '{"type":"stop"}'
    $null = Read-Until $clients[0] 'stopped'
    Send-Command $clients[0] '{"type":"start","speed":0}'
    $invalid = Read-Message $clients[0]
    Assert-True ($invalid.code -eq 'invalid_speed') 'Ungueltige Geschwindigkeit akzeptiert'
    Send-Command $clients[0] '{kaputt'
    $invalid = Read-Message $clients[0]
    Assert-True ($invalid.code -eq 'invalid_json') 'Ungueltiges JSON akzeptiert'
    Write-Output 'OK: Live-Ereignisse, zwei Sitzungen, Stop, Neustart und Eingabevalidierung.'
}
finally {
    foreach ($client in $clients) { $client.Abort(); $client.Dispose() }
}
