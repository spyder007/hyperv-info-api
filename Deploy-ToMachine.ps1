param (
    $machineName="",
    $serviceSharePath="services\spydersoft.hyperv.info\",
    $serviceName="hyperv.info"
)

Push-Location ./spydersoft.hyperv.info
dotnet build -c Release

Pop-Location


$s = New-PSSession -ComputerName $machineName
Enter-PSSession -Session $s
Invoke-Command -Session $s -ScriptBlock { Write-Host "Stopping Service"; Stop-Service "$serviceName" }
Write-Host "Copying files to $machineName"
Copy-Item -Recurse -Force spydersoft.hyperv.info\bin\Release\net6.0\* "\\$($machineName)\$($serviceSharePath)"


Invoke-Command -Session $s -ScriptBlock { Write-Host "Starting Service"; Start-Service "$serviceName" }
Remove-PSSession -Session $s

