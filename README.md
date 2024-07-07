# ControlR

A zero-trust remote control solution built with .NET 8, MAUI, and SignalR.

[![Build Status](https://dev.azure.com/translucency/ControlR/_apis/build/status%2FControlR?branchName=main)](https://dev.azure.com/translucency/ControlR/_build/latest?definitionId=35&branchName=main)

[![](https://dcbadge.limes.pink/api/server/https://discord.gg/JWJmMPc72H)](https://discord.gg/https://discord.gg/JWJmMPc72H)


Website: https://controlr.app  
Docker: https://hub.docker.com/r/translucency/controlr  
Feature Requests: https://features.controlr.app/  
Discussions: https://github.com/bitbound/ControlR/discussions  

> Note: ControlR is still in alpha and isn't ready for self-hosting yet. When it is, I'll add a quick start section here with Docker instructions.


## How the Zero-Trust Works

Zero-trust is implemented via RSA public/private keypairs. When the agent is installed, you supply the public keys that are allowed to access the device.

This public key comes from the keypair that you create when you first open the viewer app. When the viewer connects to the server, it authenticates by including a signed message in the initial request, allowing the server to verify your public key. This is implemented through a custom `AuthenticationHandler`.

Your private key is never shared with the server; only your public key is.

This process allows the viewer to establish a connection with the server.  However, it doesn't authenticate the viewer with any connected agents.

When the agent comes online, it broadcasts its presence via SignalR to public keys in its config file.

When viewers try to connect or issue commands, every message is signed with the viewer's private key.  The agent verifies every signature and checks that the signer's public key exists in their `AuthorizedKeys` config section.  If the key doesn't exist, a critical-level log entry is made, and the command is ignored.  The messages' timestamps are also signed, so they can't be captured and reissued in the future.

This means that the agent doesn't implicitly trust anything coming from the server. It's able to independently verify all commands issued to it.

No user or device data is persisted on the server. There is no database. All state and identity information is maintained on local devices.  When you uninstall the viewer and agent, it's like they were never connected.