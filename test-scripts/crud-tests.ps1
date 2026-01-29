Start-Sleep -Seconds 1
$headers = @{ Authorization = 'Bearer dev-secret-token' }
Write-Output '--- GET /api/users ---'
Invoke-RestMethod -Uri 'http://localhost:5124/api/users' -Method GET -Headers $headers | ConvertTo-Json -Depth 5

Write-Output '--- POST /api/users ---'
$new = Invoke-RestMethod -Uri 'http://localhost:5124/api/users' -Method Post -Headers $headers -Body (ConvertTo-Json @{firstName='Carlos'; lastName='Lopez'; email='carlos@techhive.local'; role='IT'}) -ContentType 'application/json'
$new | ConvertTo-Json -Depth 5

$id = $new.Id
Write-Output ("--- GET /api/users/{0} ---" -f $id)
Invoke-RestMethod -Uri "http://localhost:5124/api/users/$id" -Method GET -Headers $headers | ConvertTo-Json -Depth 5

Write-Output '--- PUT update role to Admin ---'
$updated = Invoke-RestMethod -Uri "http://localhost:5124/api/users/$id" -Method PUT -Headers $headers -Body (ConvertTo-Json @{firstName=$new.FirstName; lastName=$new.LastName; email=$new.Email; role='Admin'}) -ContentType 'application/json'
$updated | ConvertTo-Json -Depth 5

Write-Output '--- POST duplicate email (expect 409) ---'
try {
    Invoke-WebRequest -Uri 'http://localhost:5124/api/users' -Method Post -Headers $headers -Body (ConvertTo-Json @{firstName='Dup'; lastName='User'; email='alice.rogers@techhive.local'; role='IT'}) -ContentType 'application/json' -UseBasicParsing -ErrorAction Stop
    Write-Output 'Unexpected success: duplicate allowed'
} catch {
    $resp = $_.Exception.Response
    if ($resp -ne $null) {
        $status = $resp.StatusCode.value__
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        Write-Output "Expected status: $status"
        Write-Output "Body: $content"
    } else {
        Write-Output $_.Exception.Message
    }
}

Write-Output '--- POST invalid data (expect 400 validation) ---'
try {
    Invoke-WebRequest -Uri 'http://localhost:5124/api/users' -Method Post -Headers $headers -Body (ConvertTo-Json @{firstName='Bad'; lastName='NoEmail'; role='IT'}) -ContentType 'application/json' -UseBasicParsing -ErrorAction Stop
    Write-Output 'Unexpected success: invalid data accepted'
} catch {
    $resp = $_.Exception.Response
    if ($resp -ne $null) {
        $status = $resp.StatusCode.value__
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        Write-Output "Expected status: $status"
        Write-Output "Body: $content"
    } else {
        Write-Output $_.Exception.Message
    }
}

Write-Output '--- PUT duplicate email on existing user (expect 409) ---'
try {
    Invoke-WebRequest -Uri "http://localhost:5124/api/users/$id" -Method Put -Headers $headers -Body (ConvertTo-Json @{firstName='Carlos'; lastName='Lopez'; email='bob.nguyen@techhive.local'; role='Admin'}) -ContentType 'application/json' -UseBasicParsing -ErrorAction Stop
    Write-Output 'Unexpected success: duplicate allowed on update'
} catch {
    $resp = $_.Exception.Response
    if ($resp -ne $null) {
        $status = $resp.StatusCode.value__
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        Write-Output "Expected status: $status"
        Write-Output "Body: $content"
    } else {
        Write-Output $_.Exception.Message
    }
}

Write-Output '--- DELETE ---'
$del = Invoke-WebRequest -Uri "http://localhost:5124/api/users/$id" -Method DELETE -Headers $headers -UseBasicParsing -ErrorAction Stop
Write-Output ("DELETE status: {0}" -f $del.StatusCode)

Write-Output '--- GET after delete ---'
Invoke-RestMethod -Uri 'http://localhost:5124/api/users' -Method GET -Headers $headers | ConvertTo-Json -Depth 5

Write-Output '--- DEBUG throw endpoint (expect 500 JSON) ---'
try {
    Invoke-WebRequest -Uri 'http://localhost:5124/api/debug/throw' -Method Get -Headers $headers -UseBasicParsing -ErrorAction Stop
    Write-Output 'Unexpected success: debug endpoint did not throw'
} catch {
    $resp = $_.Exception.Response
    if ($resp -ne $null) {
        $status = $resp.StatusCode.value__
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $content = $reader.ReadToEnd()
        Write-Output "Expected status: $status"
        Write-Output "Body: $content"
    } else {
        Write-Output $_.Exception.Message
    }
}
