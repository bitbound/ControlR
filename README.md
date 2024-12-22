# ControlR

Open-source, self-hostable remote control and remote access.

[![Build Status](https://dev.azure.com/translucency/ControlR/_apis/build/status%2FControlR?branchName=main)](https://dev.azure.com/translucency/ControlR/_build/latest?definitionId=35&branchName=main)
[![Discord](https://img.shields.io/discord/1245426111903699087?label=Discord&logo=discord&logoColor=white&color=7289DA)](https://discord.gg/JWJmMPc72H)

Website: https://controlr.app  
Docker: https://hub.docker.com/r/translucency/controlr  
Discussions: https://github.com/bitbound/ControlR/discussions  
Project Board: https://github.com/users/bitbound/projects/1

## Quick Start:

### Public Server

Go to https://controlr.app and create an account.

### Self-Hosted

```
wget https://raw.githubusercontent.com/bitbound/ControlR/main/docker-compose/docker-compose.yml
sudo docker compose up -d
```

At minimum, you will need to supply values for the variables at the top of the compose file. By default, they're expected to be passed in via the environment variables shown to the right of the variables.

See the comments in the compose file for additional configuration info.

Afterward, ControlR should be available on port 5120 (by default). Running `curl http://127.0.0.1:5120/health` should return "Healthy".

## Reverse Proxy Configuration:

Some ControlR features require [forwarded headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer). These concepts are not unique to ASP.NET Core, so it's important to understand them when self-hosting.

When using a reverse proxy, including Cloudflare proxying, the proxy IPs must be trusted by the service receiving the forwarded traffic. By default, ControlR will trust the Docker gateway IP. If `EnableCloudflareProxySupport` option is enabled, the [Cloudflare IP ranges](https://www.cloudflare.com/ips/) will automatically be trusted too.

Every proxy server IP needs to be added to the `X-Forwarded-For` header, creating a chain of all hops until it reaches the service that handles the request. Each proxy server in the chain needs to trust all IPs that came before it. When the request reaches the servce, the header should have a complete chain of all proxy servers.

If you have another reverse proxy in front of Docker (e.g. Nginx, Caddy, etc.), it must trust the IPs of any proxies that came before it (e.g. Cloudflare). Likewise, your service in Docker (i.e. ControlR) must also trust the IP of your reverse proxy. If the reverse proxy is on the same machine as the service, and is forwarding to localhost, the service will automatically trust it.

Additional proxy IPs can be added to the `KnownProxies` list in the compose file.

If the public IP for your connected devices are not showing correctly, the problem is likely due to a misconfiguration here.

## Agent OS Support:

### Windows (10/11)

- Remote control
- Terminal uses PowerShell 7+ (pwsh.exe) if detected, otherwise PowerShell 5.1 (powershell.exe)

### Ubuntu (latest LTS)

- No remote control
- Terminal uses Bash

## Metrics

Logs, traces, and metrics will be sent to the Aspire Dashboard container. The web interface
is exposed on port 18888, and it's secured by the `aspireToken` value.

The dashboard also supports OpenIdConnect authentication. See their [readme](https://github.com/dotnet/aspire/tree/main/src/Aspire.Dashboard) for more information.

You can also add a connection string for Azure Monitor to see your data there. This can be used in combination with the Aspire Dashboard (OTLP) or on its own.

## Relay Servers

ControlR has the ability to integrate with another of my projects ([WebSocketBridge](https://github.com/bitbound/WebSocketBridge)) and transfer remote control sessions to a server closest to you. See the comments in the Docker Compose file for configuration information.

Relay servers are currently disabled on the public server (https://controlr.app), which is located in Seattle, WA.

## Screenshots

![Windows Sessions on Desktop](.assets/screenshots/desktop_windows-sessions.png)

![System Details on Desktop](.assets/screenshots/desktop_details-row.png)

![System Details on Desktop](.assets/screenshots/desktop_terminal.png)

![Remote Control on Desktop](.assets/screenshots/desktop_remote-control.png)

![Windows Sessions on Mobile](.assets/screenshots/mobile_windows-sessions.png)
