param (
    $machineName="",
    $serviceSharePath="services\Spydersoft.Hyperv.Info\",
    $serviceName="hyperv.info"
)

Push-Location ./Spydersoft.Hyperv.Info
dotnet build -c Release

Pop-Location


$s = New-PSSession -ComputerName $machineName
Enter-PSSession -Session $s
Invoke-Command -Session $s -ArgumentList $serviceName -ScriptBlock { Write-Host "Stopping Service $($args[0])"; Stop-Service "$($args[0])" }

Write-Host "Saving Appsettings";
Copy-Item "\\$($machineName)\$($serviceSharePath)\appsettings.json" "temp.appsettings.json"

Write-Host "Copying files to $machineName"
Copy-Item -Recurse -Force Spydersoft.Hyperv.Info\bin\Release\net8.0\* "\\$($machineName)\$($serviceSharePath)"

Write-Host "Loading Appsettings";
Move-Item "temp.appsettings.json" "\\$($machineName)\$($serviceSharePath)\appsettings.json" -Force

Invoke-Command -Session $s -ArgumentList $serviceName -ScriptBlock { Write-Host "Starting Service $($args[0])"; Start-Service "$($args[0])" }
Remove-PSSession -Session $s

