import type { ForgeConfig } from "@electron-forge/shared-types";
import { MakerSquirrel } from "@electron-forge/maker-squirrel";
import { MakerZIP } from "@electron-forge/maker-zip";
import { MakerDeb } from "@electron-forge/maker-deb";
import { MakerRpm } from "@electron-forge/maker-rpm";
//import { MakerAppImage } from "@reforged/maker-appimage";
import { WebpackPlugin } from "@electron-forge/plugin-webpack";
import { mainConfig } from "./webpack.main.config";
import { rendererConfig } from "./webpack.renderer.config";
import PortableMaker from "./src/infrastructure/PortableMaker";

const config: ForgeConfig = {
  packagerConfig: {
    icon: "./assets/appicon",
    extraResource: [
      "./assets/appicon.icns",
      "./assets/appicon.ico",
      "./assets/appicon.png",
    ],
  },
  rebuildConfig: {},
  makers: [
    new MakerSquirrel({
      setupIcon: "./assets/appicon.ico",
    }),
    new PortableMaker({
      icon: "./assets/appicon.ico",
      win: {
        artifactName: "ControlR.exe",
        icon: "./assets/appicon.ico",
        target: "portable",
      },
    }),
    new MakerZIP({}, ["darwin", "win32", "linux"]),
    new MakerRpm({
      options: {
        icon: "./assets/assets/appicon.png",
      },
    }),
    // new MakerAppImage({
    //   options: {
    //     icon: "./assets/assets/appicon.png",
    //     categories: ["Utility"],
    //   },
    // }),
    new MakerDeb({
      options: {
        icon: "./assets/assets/appicon.png",
      },
    }),
  ],
  plugins: [
    new WebpackPlugin({
      mainConfig,
      renderer: {
        config: rendererConfig,
        entryPoints: [
          {
            html: "./src/renderer/index.html",
            js: "./src/renderer/renderer.tsx",
            name: "main_window",
            preload: {
              js: "./src/renderer/preload.ts",
            },
          },
        ],
      },
    }),
  ],
};

export default config;
