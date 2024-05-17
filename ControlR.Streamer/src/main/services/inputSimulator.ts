import {
  MovePointerDto,
  KeyEventDto,
  MouseButtonEventDto,
  TypeTextDto,
  SidecarDtoBase,
  WheelScrollDto,
} from "../../shared/sidecarDtos";

import { sendMessage } from "./sidecarIpc";

export async function invokeKeyEvent(
  key: string,
  isPressed: boolean,
  shouldRelease: boolean,
) {
  const dto = {
    dtoType: "KeyEvent",
    key: key,
    isPressed: isPressed,
  } as KeyEventDto;

  await sendMessage(dto);

  if (isPressed && shouldRelease) {
    dto.isPressed = false;
    await sendMessage(dto);
  }
}

export async function invokeMouseButtonEvent(
  button: number,
  isPressed: boolean,
  x: number,
  y: number,
) {
  const dto = {
    button: button,
    isPressed: isPressed,
    x: x,
    y: y,
    dtoType: "MouseButtonEvent",
  } as MouseButtonEventDto;

  await sendMessage(dto);
}

export async function invokeTypeText(text: string) {
  const dto = {
    text: text,
    dtoType: "TypeText",
  } as TypeTextDto;

  await sendMessage(dto);
}

export async function movePointer(x: number, y: number) {
  const dto = {
    dtoType: "MovePointer",
    moveType: "Absolute",
    x: x,
    y: y,
  } as MovePointerDto;

  await sendMessage(dto);
}

export async function movePointerBy(x: number, y: number) {
  const dto = {
    dtoType: "MovePointer",
    moveType: "Relative",
    x: x,
    y: y,
  } as MovePointerDto;

  await sendMessage(dto);
}

export async function resetKeyboardState() {
  const dto = {
    dtoType: "ResetKeyboardState",
  } as SidecarDtoBase;
  sendMessage(dto);
}

export async function scrollWheel(
  x: number,
  y: number,
  scrollY: number,
  scrollX: number,
) {
  const dto = {
    x: x,
    y: y,
    scrollY: scrollY,
    scrollX: scrollX,
    dtoType: "WheelScroll",
  } as WheelScrollDto;
  sendMessage(dto);
}
