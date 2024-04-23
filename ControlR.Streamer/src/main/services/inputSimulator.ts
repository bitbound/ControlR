import { mouse, keyboard, Key } from "@nut-tree/nut-js";

export async function invokeKeyEvent(
  keyCode: string,
  isPressed: boolean,
  shouldRelease: boolean,
) {
  if (isPressed) {
    pressKey(keyCode);
    if (shouldRelease) {
      releaseKey(keyCode);
    }
  } else {
    releaseKey(keyCode);
  }
}

export async function invokeMouseButtonEvent(
  button: number,
  isPressed: boolean,
  x: number,
  y: number,
) {
  movePointer(x, y);
  if (isPressed) {
    pressMouseButton(button);
  } else {
    releaseMouseButton(button);
  }
}

export async function invokeTypeText(text: string) {
  await keyboard.type(text);
}

export async function movePointer(x: number, y: number) {
  await mouse.setPosition({ x: x, y: y });
}

export async function movePointerBy(x: number, y: number) {
    const currentPos = await mouse.getPosition();
    await mouse.setPosition({
        x: currentPos.x + x,
        y: currentPos.y + y
    })
}

export async function pressMouseButton(button: number) {
  await mouse.pressButton(button);
}

export async function releaseMouseButton(button: number) {
  await mouse.releaseButton(button);
}

export async function pressKey(keyCode: string) {
  keyboard.config.autoDelayMs = 0;
  await keyboard.pressKey(keyMap[keyCode].nutValue);
}

export async function releaseKey(keyCode: string) {
  keyboard.config.autoDelayMs = 0;
  await keyboard.releaseKey(keyMap[keyCode].nutValue);
}

export async function resetKeyboardState() {
  keyboard.config.autoDelayMs = 0;

  const keys = [
    Key.LeftShift,
    Key.RightShift,
    Key.LeftAlt,
    Key.RightAlt,
    Key.LeftControl,
    Key.RightControl,
  ];

  keys.forEach((x) => {
    keyboard.releaseKey(x);
  });
}

export async function scrollWheel(
  deltaX: number,
  deltaY: number,
  deltaZ: number,
) {
  mouse.scrollRight(deltaX);
  mouse.scrollDown(deltaY);
}

interface NutObject {
  nutValue: number;
  nutKey: string;
}

const keyMap: Record<string, NutObject> = {
  Space: {
    nutValue: 0,
    nutKey: "Space",
  },
  Escape: {
    nutValue: 1,
    nutKey: "Escape",
  },
  Tab: {
    nutValue: 2,
    nutKey: "Tab",
  },
  AltLeft: {
    nutValue: 3,
    nutKey: "LeftAlt",
  },
  ControlLeft: {
    nutValue: 4,
    nutKey: "LeftControl",
  },
  AltRight: {
    nutValue: 5,
    nutKey: "RightAlt",
  },
  ControlRight: {
    nutValue: 6,
    nutKey: "RightControl",
  },
  ShiftLeft: {
    nutValue: 7,
    nutKey: "LeftShift",
  },
  LeftSuper: {
    nutValue: 8,
    nutKey: "LeftSuper",
  },
  ShiftRight: {
    nutValue: 9,
    nutKey: "RightShift",
  },
  RightSuper: {
    nutValue: 10,
    nutKey: "RightSuper",
  },
  F1: {
    nutValue: 11,
    nutKey: "F1",
  },
  F2: {
    nutValue: 12,
    nutKey: "F2",
  },
  F3: {
    nutValue: 13,
    nutKey: "F3",
  },
  F4: {
    nutValue: 14,
    nutKey: "F4",
  },
  F5: {
    nutValue: 15,
    nutKey: "F5",
  },
  F6: {
    nutValue: 16,
    nutKey: "F6",
  },
  F7: {
    nutValue: 17,
    nutKey: "F7",
  },
  F8: {
    nutValue: 18,
    nutKey: "F8",
  },
  F9: {
    nutValue: 19,
    nutKey: "F9",
  },
  F10: {
    nutValue: 20,
    nutKey: "F10",
  },
  F11: {
    nutValue: 21,
    nutKey: "F11",
  },
  F12: {
    nutValue: 22,
    nutKey: "F12",
  },
  F13: {
    nutValue: 23,
    nutKey: "F13",
  },
  F14: {
    nutValue: 24,
    nutKey: "F14",
  },
  F15: {
    nutValue: 25,
    nutKey: "F15",
  },
  F16: {
    nutValue: 26,
    nutKey: "F16",
  },
  F17: {
    nutValue: 27,
    nutKey: "F17",
  },
  F18: {
    nutValue: 28,
    nutKey: "F18",
  },
  F19: {
    nutValue: 29,
    nutKey: "F19",
  },
  F20: {
    nutValue: 30,
    nutKey: "F20",
  },
  F21: {
    nutValue: 31,
    nutKey: "F21",
  },
  F22: {
    nutValue: 32,
    nutKey: "F22",
  },
  F23: {
    nutValue: 33,
    nutKey: "F23",
  },
  F24: {
    nutValue: 34,
    nutKey: "F24",
  },
  Digit0: {
    nutValue: 35,
    nutKey: "Num0",
  },
  Digit1: {
    nutValue: 36,
    nutKey: "Num1",
  },
  Digit2: {
    nutValue: 37,
    nutKey: "Num2",
  },
  Digit3: {
    nutValue: 38,
    nutKey: "Num3",
  },
  Digit4: {
    nutValue: 39,
    nutKey: "Num4",
  },
  Digit5: {
    nutValue: 40,
    nutKey: "Num5",
  },
  Digit6: {
    nutValue: 41,
    nutKey: "Num6",
  },
  Digit7: {
    nutValue: 42,
    nutKey: "Num7",
  },
  Digit8: {
    nutValue: 43,
    nutKey: "Num8",
  },
  Digit9: {
    nutValue: 44,
    nutKey: "Num9",
  },
  KeyA: {
    nutValue: 45,
    nutKey: "A",
  },
  KeyB: {
    nutValue: 46,
    nutKey: "B",
  },
  KeyC: {
    nutValue: 47,
    nutKey: "C",
  },
  KeyD: {
    nutValue: 48,
    nutKey: "D",
  },
  KeyE: {
    nutValue: 49,
    nutKey: "E",
  },
  KeyF: {
    nutValue: 50,
    nutKey: "F",
  },
  KeyG: {
    nutValue: 51,
    nutKey: "G",
  },
  KeyH: {
    nutValue: 52,
    nutKey: "H",
  },
  KeyI: {
    nutValue: 53,
    nutKey: "I",
  },
  KeyJ: {
    nutValue: 54,
    nutKey: "J",
  },
  KeyK: {
    nutValue: 55,
    nutKey: "K",
  },
  KeyL: {
    nutValue: 56,
    nutKey: "L",
  },
  KeyM: {
    nutValue: 57,
    nutKey: "M",
  },
  KeyN: {
    nutValue: 58,
    nutKey: "N",
  },
  KeyO: {
    nutValue: 59,
    nutKey: "O",
  },
  KeyP: {
    nutValue: 60,
    nutKey: "P",
  },
  KeyQ: {
    nutValue: 61,
    nutKey: "Q",
  },
  KeyR: {
    nutValue: 62,
    nutKey: "R",
  },
  KeyS: {
    nutValue: 63,
    nutKey: "S",
  },
  KeyT: {
    nutValue: 64,
    nutKey: "T",
  },
  KeyU: {
    nutValue: 65,
    nutKey: "U",
  },
  KeyV: {
    nutValue: 66,
    nutKey: "V",
  },
  KeyW: {
    nutValue: 67,
    nutKey: "W",
  },
  KeyX: {
    nutValue: 68,
    nutKey: "X",
  },
  KeyY: {
    nutValue: 69,
    nutKey: "Y",
  },
  KeyZ: {
    nutValue: 70,
    nutKey: "Z",
  },
  Backquote: {
    nutValue: 71,
    nutKey: "Grave",
  },
  Minus: {
    nutValue: 72,
    nutKey: "Minus",
  },
  Equal: {
    nutValue: 73,
    nutKey: "Equal",
  },
  Backspace: {
    nutValue: 74,
    nutKey: "Backspace",
  },
  BracketLeft: {
    nutValue: 75,
    nutKey: "LeftBracket",
  },
  BracketRight: {
    nutValue: 76,
    nutKey: "RightBracket",
  },
  Backslash: {
    nutValue: 77,
    nutKey: "Backslash",
  },
  Semicolon: {
    nutValue: 78,
    nutKey: "Semicolon",
  },
  Quote: {
    nutValue: 79,
    nutKey: "Quote",
  },
  Enter: {
    nutValue: 80,
    nutKey: "Return",
  },
  Comma: {
    nutValue: 81,
    nutKey: "Comma",
  },
  Period: {
    nutValue: 82,
    nutKey: "Period",
  },
  Slash: {
    nutValue: 83,
    nutKey: "Slash",
  },
  ArrowLeft: {
    nutValue: 84,
    nutKey: "Left",
  },
  ArrowUp: {
    nutValue: 85,
    nutKey: "Up",
  },
  ArrowRight: {
    nutValue: 86,
    nutKey: "Right",
  },
  ArrowDown: {
    nutValue: 87,
    nutKey: "Down",
  },
  PrintScreen: {
    nutValue: 88,
    nutKey: "Print",
  },
  Pause: {
    nutValue: 89,
    nutKey: "Pause",
  },
  Insert: {
    nutValue: 90,
    nutKey: "Insert",
  },
  Delete: {
    nutValue: 91,
    nutKey: "Delete",
  },
  Home: {
    nutValue: 92,
    nutKey: "Home",
  },
  End: {
    nutValue: 93,
    nutKey: "End",
  },
  PageUp: {
    nutValue: 94,
    nutKey: "PageUp",
  },
  PageDown: {
    nutValue: 95,
    nutKey: "PageDown",
  },
  NumpadAdd: {
    nutValue: 96,
    nutKey: "Add",
  },
  NumpadSubtract: {
    nutValue: 97,
    nutKey: "Subtract",
  },
  NumpadMultiply: {
    nutValue: 98,
    nutKey: "Multiply",
  },
  NumpadDivide: {
    nutValue: 99,
    nutKey: "Divide",
  },
  NumpadDecimal: {
    nutValue: 100,
    nutKey: "Decimal",
  },
  NumpadEnter: {
    nutValue: 101,
    nutKey: "Enter",
  },
  Numpad0: {
    nutValue: 102,
    nutKey: "NumPad0",
  },
  Numpad1: {
    nutValue: 103,
    nutKey: "NumPad1",
  },
  Numpad2: {
    nutValue: 104,
    nutKey: "NumPad2",
  },
  Numpad3: {
    nutValue: 105,
    nutKey: "NumPad3",
  },
  Numpad4: {
    nutValue: 106,
    nutKey: "NumPad4",
  },
  Numpad5: {
    nutValue: 107,
    nutKey: "NumPad5",
  },
  Numpad6: {
    nutValue: 108,
    nutKey: "NumPad6",
  },
  Numpad7: {
    nutValue: 109,
    nutKey: "NumPad7",
  },
  Numpad8: {
    nutValue: 110,
    nutKey: "NumPad8",
  },
  Numpad9: {
    nutValue: 111,
    nutKey: "NumPad9",
  },
  CapsLock: {
    nutValue: 112,
    nutKey: "CapsLock",
  },
  ScrollLock: {
    nutValue: 113,
    nutKey: "ScrollLock",
  },
  NumLock: {
    nutValue: 114,
    nutKey: "NumLock",
  },
  AudioVolumeMute: {
    nutValue: 115,
    nutKey: "AudioMute",
  },
  AudioVolumeDown: {
    nutValue: 116,
    nutKey: "AudioVolDown",
  },
  AudioVolumeUp: {
    nutValue: 117,
    nutKey: "AudioVolUp",
  },
  MediaPlayPause: {
    nutValue: 118,
    nutKey: "AudioPlay",
  },
  MediaStop: {
    nutValue: 119,
    nutKey: "AudioStop",
  },
  AudioPause: {
    nutValue: 120,
    nutKey: "AudioPause",
  },
  MediaTrackPrevious: {
    nutValue: 121,
    nutKey: "AudioPrev",
  },
  MediaTrackNext: {
    nutValue: 122,
    nutKey: "AudioNext",
  },
  BrowserBack: {
    nutValue: 123,
    nutKey: "AudioRewind",
  },
  BrowserForward: {
    nutValue: 124,
    nutKey: "AudioForward",
  },
  AudioRepeat: {
    nutValue: 125,
    nutKey: "AudioRepeat",
  },
  AudioRandom: {
    nutValue: 126,
    nutKey: "AudioRandom",
  },
  MetaLeft: {
    nutValue: 127,
    nutKey: "LeftWin",
  },
  MetaRight: {
    nutValue: 128,
    nutKey: "RightWin",
  },
  LeftCmd: {
    nutValue: 129,
    nutKey: "LeftCmd",
  },
  RightCmd: {
    nutValue: 130,
    nutKey: "RightCmd",
  },
  ContextMenu: {
    nutValue: 131,
    nutKey: "Menu",
  },
  Fn: {
    nutValue: 132,
    nutKey: "Fn",
  },
};
