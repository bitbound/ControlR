# ControlR

A zero-trust remote control solution built with .NET 8, MAUI, and SignalR.

[![Build Status](https://dev.azure.com/translucency/ControlR/_apis/build/status%2FControlR?branchName=main)](https://dev.azure.com/translucency/ControlR/_build/latest?definitionId=35&branchName=main)

[![Discord](https://img.shields.io/discord/1245426111903699087?label=Discord&logo=discord&logoColor=white&color=7289DA)](https://discord.gg/JWJmMPc72H)

Website: https://controlr.app  
Docker: https://hub.docker.com/r/translucency/controlr  
Feature Requests: https://features.controlr.app/  
Discussions: https://github.com/bitbound/ControlR/discussions

## Testers Needed:

Please join the [Android Beta Test](https://play.google.com/apps/testing/dev.jaredg.controlr.viewer) if you can. I need at least 20 users to stay in the program for at least 14 days before I can publish the app to the Play Store.

## Quick Start:

Steps:

- Run Docker with the example compose file (see below).
- Download and install the viewer from `http[s]://{host_name}/downloads/ControlR.Viewer.{msix/apk}`.
  - For Windows, use the `msix` extension.
  - For Android, use the `apk` extension.
- In the Viewer app, create a new keypair by clicking the `Create` button.
- Change the server URL in `Settings` to your server.
- Restart the Viewer.
- (Optional) Lock your server down so only you can use it by doing the following:
  - In the Viewer, go to `Keys` page and copy your public key.
  - In docker-compose.yml, set `EnableRestrictedUserAccess` to true.
  - Add your public key to `AdminPublicKeys__0` and `AuthorizedUserPublicKeys__0`.
  - Now only your keypair will be able to access the server.
- Install the agent on a computer by copying and pasting one of the scripts on the `Deploy` page.

```
wget https://raw.githubusercontent.com/bitbound/ControlR/main/docker-compose/docker-compose.yml

# If needed, make changes to docker-compose.yml.  For example, you may want
# to use a bind mount for '/app/AppData' instead of a  Docker volume.
# See the comments in the compose file for additional configuration info.

sudo docker compose up -d
```

ControlR should now be available on port 5120 (by default). Running `curl http://127.0.0.1:5120/api/health` should return "Healthy".

Navigating to the root URI in the browser will redirect to `https://controlr.app`. This is because ControlR doesn't have a web UI and is only meant to be used through the native app.

## Side-loaded vs Store Installations

The viewer app can be installed from one of the stores (Microsoft/Google Play), or it can be side-loaded by downloading the installer file. The app will change auto-update behavior based on whether it was side-loaded or acquired from the store.

It's recommended that you only use one or the other to avoid conflicts. If you're self-hosting, you should only be using the side-loaded method so the version will stay in sync with your specific server instance.

If you're using the public server at app.controlr.app, you can use either installation method.

You can see which method the app is currently using on the `About` page, next to `Install Source`.

## How the Zero-Trust Works

Zero-trust is implemented via RSA public/private keypairs. When the agent is installed, you supply the public keys that are allowed to access the device.

This public key comes from the keypair that you create when you first open the viewer app. When the viewer connects to the server, it authenticates by including a signed message in the initial request, allowing the server to verify your public key. This is implemented through a custom `AuthenticationHandler`.

Your private key is never shared with the server; only your public key is.

This process allows the viewer to establish a connection with the server. However, it doesn't authenticate the viewer with any connected agents.

When the agent comes online, it broadcasts its presence via SignalR to public keys in its config file.

When viewers try to connect or issue commands, every message is signed with the viewer's private key. The agent verifies every signature and checks that the signer's public key exists in their `AuthorizedKeys` config section. If the key doesn't exist, a critical-level log entry is made, and the command is ignored. The messages' timestamps are also signed, so they can't be captured and reissued in the future.

This means that the agent doesn't implicitly trust anything coming from the server. It's able to independently verify all commands issued to it.

No user or device data is persisted on the server. There is no database. All state and identity information is maintained on local devices. When you uninstall the viewer and agent, it's like they were never connected.
