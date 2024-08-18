# ControlR

A zero-trust remote control solution built with .NET 8, MAUI, and SignalR.

[![Build Status](https://dev.azure.com/translucency/ControlR/_apis/build/status%2FControlR?branchName=main)](https://dev.azure.com/translucency/ControlR/_build/latest?definitionId=35&branchName=main)
[![Discord](https://img.shields.io/discord/1245426111903699087?label=Discord&logo=discord&logoColor=white&color=7289DA)](https://discord.gg/JWJmMPc72H)

Website: https://controlr.app  
Docker: https://hub.docker.com/r/translucency/controlr  
Feature Requests: https://features.controlr.app/  
Discussions: https://github.com/bitbound/ControlR/discussions

Microsoft Store: https://www.microsoft.com/store/productId/9NS914B8GR04  
Play Store: https://play.google.com/apps/testing/dev.jaredg.controlr.viewer

## Testers Needed:

If you'd like to help to get ControlR on the Play Store, please join the beta test. It needs at least 20 users to stay in the program for at least 14 days before I can publish the app to the Play Store.

Accessing the beta is a two-step process:

- Join the [ControlR Testers](https://groups.google.com/g/controlr-testers/about) Google Group.
- Install [ControlR (Early Access)](https://play.google.com/store/apps/details?id=dev.jaredg.controlr.viewer) from the Play Store.

## Quick Start:

Public Server:

- Install the Viewer through the Microsoft Store or Play Store.
- In the Viewer app, create a new keypair by clicking the Create button.
- Install the agent on a computer by copying and pasting one of the scripts on the Deploy page.
- You should now see the computer in the devices list and be able to remote control it.
  - Note: Remote control is only supported for agents running on Windows.

Self-Hosted:

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

## Relay Servers

On the public server (https://app.controlr.app), when you start a remote control session, the main server will attempt to transfer the session to the relay server that's closest to you. These relay servers use another of my projects ([WebSocketBridge](https://github.com/bitbound/WebSocketBridge)) to stream the remote control data.

Currently, there are servers deployed in the following locations:

- Phoenix, Arizona, United States
- St. Louis, Missouri, United States
- Boydton, Virginia, United States

I may add or remove servers in the future, depending on sponsorships/donations.

## Screenshots

![Windows Sessions on Desktop](.assets/screenshots/desktop_windows-sessions.png)

![Windows Sessions on Android](.assets/screenshots/mobile_windows-sessions.jpg)

## How the Zero-Trust Works

Zero-trust is implemented via RSA public/private keypairs. When the agent is installed, you supply the public keys that are allowed to access the device.

This public key comes from the keypair that you create when you first open the viewer app. When the viewer connects to the server, it authenticates by including a signed message in the initial request, allowing the server to verify your public key. This is implemented through a custom `AuthenticationHandler`.

Your private key is never shared with the server; only your public key is.

This process allows the viewer to establish a connection with the server. However, it doesn't authenticate the viewer with any connected agents.

When the agent comes online, it broadcasts its presence via SignalR to public keys in its config file.

When viewers try to connect or issue commands, every message is signed with the viewer's private key. The agent verifies every signature and checks that the signer's public key exists in their `AuthorizedKeys` config section. If the key doesn't exist, a critical-level log entry is made, and the command is ignored. The messages' timestamps are also signed, so they can't be captured and reissued in the future.

This means that the agent doesn't implicitly trust anything coming from the server. It's able to independently verify all commands issued to it.

No user or device data is persisted on the server. There is no database. All state and identity information is maintained on local devices. When you uninstall the viewer and agent, it's like they were never connected.

## Agent Auto-Updates

Installed agents will automatically update themselves when new versions are released. To increase security, the agent will verify that the SHA256 hash of the new version's binary/archive exists in a separate data store (currently hosted by Cloudflare R2). If not, the update is aborted, and a critical-level log entry is written.

The hashes are uploaded to Cloudflare with each build, from a different location and server than where ControlR is hosted. Write access to R2 is limited to a single IP address.

This separation ensures that, even if the ControlR server is compromised, they wouldn't be able to get agents to auto-update with malicious binaries.
