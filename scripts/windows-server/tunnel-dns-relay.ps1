$ErrorActionPreference = "Stop"

$listenAddress = [Net.IPAddress]::Parse("10.77.0.1")
$listenPort = 53
$upstreams = @(
  [Net.IPEndPoint]::new([Net.IPAddress]::Parse("1.1.1.1"), 53),
  [Net.IPEndPoint]::new([Net.IPAddress]::Parse("8.8.8.8"), 53)
)

# At boot WireGuard may not have created 10.77.0.1 yet. Keep retrying rather
# than exiting, so the Scheduled Task survives ordinary service start ordering.
$listener = $null
while ($null -eq $listener) {
  try {
    $listener = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new($listenAddress, $listenPort))
  } catch {
    Start-Sleep -Seconds 5
  }
}

while ($true) {
  try {
    $client = [Net.IPEndPoint]::new([Net.IPAddress]::Any, 0)
    $query = $listener.Receive([ref]$client)

    foreach ($upstream in $upstreams) {
      $forwarder = [Net.Sockets.UdpClient]::new([Net.Sockets.AddressFamily]::InterNetwork)
      try {
        $forwarder.Connect($upstream)
        [void]$forwarder.Send($query, $query.Length)
        $pending = $forwarder.BeginReceive($null, $null)
        if (!$pending.AsyncWaitHandle.WaitOne(4000)) { continue }
        $server = [Net.IPEndPoint]::new([Net.IPAddress]::Any, 0)
        $answer = $forwarder.EndReceive($pending, [ref]$server)
        [void]$listener.Send($answer, $answer.Length, $client)
        break
      } finally {
        $forwarder.Dispose()
      }
    }
  } catch {
    Start-Sleep -Milliseconds 200
  }
}
