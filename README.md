# hyperv-info-api

This is a small API project, meant to run as a Windows Service, that uses Powershell to manage Hyper-V VMs.  

## VM Data

This API uses the `Notes` field on the Hyper-V VM to store extra data about the VM that is used to set the `AutomaticStartDelay` for VMs.  The data is stored in the following format:

```json
{
    "startGroup": 0,
    "delayOffset": 0
}
```

| Property      | Type | Description                                            | Default |
| ------------- | ---- | ------------------------------------------------------ | ------- |
| `startGroup`  | int  | The one-based group used to calculate the start delay. | 0       |
| `delayOffset` | int  | The offset within the group.                           | 0       |

## Delay Calculation

The `AutomaticStartDelay` is the delay, in seconds, before the host will start the VM.  Based on the VM Data above, the following calculation is used:

```bash
delay = ((startGroup - 1) * groupDelay) + delayOffset
```

The above calculation is only run if the `startGroup` is greater than 1.  The `groupDelay` can be specified in the `POST` to `/vm/refreshdelay`, but it defaults to 480 seconds.

## Installing the Service

You can install this service using any of the methods described in [Host ASP.NET Core in a Windows Service](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-7.0&tabs=visual-studio).  The service will need to run as a user with Powershell access and rights to modify the VMs.  

You will most likely have to create a new user and grant them service log on via local security policy.  See [Enable Service Logon](https://learn.microsoft.com/en-us/system-center/scsm/enable-service-log-on-sm?view=sc-sm-2022) for details.

### Deployment Powershell

Once the service is installed, Deploy-ToMachine.ps1 can be used to stop the service on the target machine, build this library in debug, and copy the build to the machine.  This is meant to be a template for your own deployments.

## Authentication

This API is setup with optional JWT Bearer Token authentication.  If you have an OAuth 2 service, you can configure authentication by setting the `Identity` values in the `appSettings.json` folder.

| Identity Property | Description                                               |
| ----------------- | --------------------------------------------------------- |
| AuthorityUrl      | The url of the Token Authority                            |
| ApiName           | This is the audience value required for the incoming call |

At this time, only the audience is validated via the ApiName.  Further validation, such as issuer and issuer signing key, can be added to the code if you need.  [How to Implement JWT Authentication in ASP.NET Core 6](https://www.infoworld.com/article/3669188/how-to-implement-jwt-authentication-in-aspnet-core-6.html) will get you started.
