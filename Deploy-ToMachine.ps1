param (
    $machineName="",
    $serviceSharePath="services\spydersoft.hyperv.info\"
)

$s = New-PSSession -ComputerName $machineName
Enter-PSSession -Session $s
Invoke-Command -Session $s -ScriptBlock { Write-Host "Stopping Service"; Stop-Service hyperv.info }

Push-Location ./spydersoft.hyperv.info
dotnet build -c Release
Write-Host "Copying files to $machineName"
Copy-Item -Recurse -Force bin\Release\net6.0\* "\\$($machineName)\$($serviceSharePath)"
Pop-Location

Invoke-Command -Session $s -ScriptBlock { Write-Host "Starting Service"; Start-Service hyperv.info }
Remove-PSSession -Session $s

