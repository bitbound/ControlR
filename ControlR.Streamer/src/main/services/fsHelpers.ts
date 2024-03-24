import path from 'path';
import {app} from 'electron';

export const assetsPath = app.isPackaged
  ? path.join(process.resourcesPath)
  : path.join(__dirname, '../../assets');


export function getAssetsPath(...paths: string[]): string {
  return path.join(assetsPath, ...paths);
}