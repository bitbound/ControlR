import { spawn } from "child_process";
import crypto from "crypto";
import net from "net";
import { writeLog } from "./logger";
import { app, dialog } from "electron";
import fs from "fs";
import { DesktopChangedDto, SidecarDtoBase } from "../../shared/sidecarDtos";
import { sendDesktopChanged } from "./rendererApi";

let server: net.Server;
let client: net.Socket;

export function launchSidecar(): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    if (server) {
      reject("Sidecar IPC server has already been created.");
    }

    const pipeName = crypto.randomUUID();
    const serverPipePath = `\\\\.\\pipe\\${pipeName}`;

    writeLog("Starting pipe server at path: ", "Info", serverPipePath);

    const connectTimeout = setTimeout(() => {
      writeLog("Timed out while waiting for sidecar to connect.", "Error");
      reject();
    }, 10000);

    server = net.createServer(function (stream) {
      clearTimeout(connectTimeout);
      writeLog("Sidecar connected to IPC channel.");

      stream.on("data", function (data) {
        const message = JSON.parse(data.toString()) as SidecarDtoBase;
        writeLog("Received message from sidecar: ", "Info", message);
        switch (message?.dtoType) {
          case "DesktopChanged": {
            const changeDto = message as DesktopChangedDto;
            writeLog(
              "Input desktop changed to: ",
              "Info",
              changeDto.desktopName,
            );
            sendDesktopChanged();
            break;
          }
          default:
            writeLog("Unexpected DTO type.", "Warning");
            break;
        }
      });

      stream.on("end", function () {
        writeLog("Sidecar pipe server closing.");
        server.close();
      });

      client = stream;
      resolve();
    });

    server.on("close", function () {
      writeLog("Sidecar pipe server closed. Exiting.");
      app.exit();
    });

    server.on("error", (e) => {
      writeLog("Error in sidecar pipe server.", "Error", e);
      reject();
    });

    server.listen(serverPipePath);

    let binaryPath =
      process.platform === "win32"
        ? "ControlR.Streamer.Sidecar.exe"
        : "ControlR.Streamer.Sidecar";

    if (app.isPackaged) {
      binaryPath = `${process.resourcesPath}\\${binaryPath}`;
    } else {
      binaryPath = `.\\.artifacts\\${binaryPath}`;
    }

    if (!fs.existsSync(binaryPath)) {
      dialog.showErrorBox(
        "File Not Found",
        `File does not exist: ${binaryPath}`,
      );
      reject();
      return;
    }

    const sidecarProc = spawn(
      binaryPath,
      ["--streamer-pipe", pipeName, "--parent-id", process.pid.toString()],
      {
        shell: app.isPackaged ? undefined : true,
        detached: app.isPackaged ? undefined : true,
        env: process.env,
        cwd: app.getAppPath(),
      },
    );

    if (sidecarProc.exitCode !== null) {
      writeLog("Sidecar failed to start.", "Error");
      reject();
    }
  });
}

export async function sendMessage<T extends SidecarDtoBase>(dto: T) {
  const json = JSON.stringify(dto);
  const result = client.write(`${json}\n`, (err) => {
    if (err) {
      writeLog("Error while sending message to sidecar: ", "Error", err);
    }
  });

  if (!result) {
    writeLog("Drain needed in sidecar IPC.");
  }
}
