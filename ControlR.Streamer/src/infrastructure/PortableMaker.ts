import MakerBase, { MakerOptions } from "@electron-forge/maker-base";
import { ForgePlatform } from "@electron-forge/shared-types";
import { build, Configuration } from "app-builder-lib";
import { resolve } from "path";

export default class PortableMaker extends MakerBase<Configuration> {
  name = "portable";
  defaultPlatforms: ForgePlatform[] = ["win32"];

  isSupportedOnCurrentPlatform() {
    return true;
  }

  async make(options: MakerOptions) {
    if (options.targetPlatform !== "win32") {
      throw new Error("Portable apps can only target the 'win32' platform");
    }

    const appDir = options.dir;

    return build({
      prepackaged: appDir,
      win: [`portable:${options.targetArch}`],
      config: {
        ...this.config,
        directories: {
          output: resolve(appDir, "..", "make", "portable-windows"),
          ...this.config?.directories,
        },
      },
    });
  }
}