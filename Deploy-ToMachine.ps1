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
Invoke-Command -Session $s -ArgumentList $serviceName -ScriptBlock { Write-Host "Stopping Service $($args[0])"; Stop-Service "$($args[0])" }
Write-Host "Copying files to $machineName"
Copy-Item -Recurse -Force spydersoft.hyperv.info\bin\Release\net6.0\* "\\$($machineName)\$($serviceSharePath)"


Invoke-Command -Session $s -ArgumentList $serviceName -ScriptBlock { Write-Host "Starting Service $($args[0])"; Start-Service "$($args[0])" }
Remove-PSSession -Session $s

