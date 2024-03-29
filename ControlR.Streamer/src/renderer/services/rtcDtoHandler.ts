import { Point } from "@nut-tree/nut-js";
import RtcSession from "./rtcSession";
import { setMediaStreams } from "./mediaHelperRenderer";

export async function handleDataChannelMessage(data: string) {
    const dto = JSON.parse(data) as BaseDto;
    switch (dto.dtoType) {
        case "pointerMove":
            {
                const moveDto = dto as PointerMoveDto;
                const point = getAbsoluteScreenPoint(
                    moveDto.percentX,
                    moveDto.percentY,
                );
                await window.mainApi.movePointer(point.x, point.y);
            }
            break;
        case "keyEvent":
            {
                const keyDto = dto as KeyEventDto;
                await window.mainApi.invokeKeyEvent(
                    keyDto.keyCode,
                    keyDto.isPressed,
                    keyDto.shouldRelease,
                );
            }
            break;
        case "mouseButtonEvent":
            {
                const buttonDto = dto as MouseButtonEventDto;
                const point = getAbsoluteScreenPoint(
                    buttonDto.percentX,
                    buttonDto.percentY,
                );
                await window.mainApi.invokeMouseButtonEvent(
                    buttonDto.button,
                    buttonDto.isPressed,
                    point.x,
                    point.y,
                );
            }
            break;
        case "resetKeyboardState":
            {
                await window.mainApi.resetKeyboardState();
            }
            break;
        case "wheelScrollEvent":
            {
                const scrollDto = dto as WheelScrollDto;
                await window.mainApi.invokeWheelScroll(
                    scrollDto.deltaX,
                    scrollDto.deltaY,
                    scrollDto.deltaZ,
                );
            }
            break;
        case "typeText":
            {
                const typeDto = dto as TypeTextDto;
                await window.mainApi.invokeTypeText(typeDto.text);
            }
            break;
        case "changeDisplay":
            {
                const changeDisplayDto = dto as ChangeDisplayDto;
                window.mainApi.writeLog(`Changing display to media ID: ${changeDisplayDto.mediaId}`);
                await setMediaStreams(changeDisplayDto.mediaId, RtcSession.peerConnection);
                RtcSession.changeCurrentScreen(changeDisplayDto.mediaId);
            }
            break;
        default:
            console.warn("Unhandled DTO type: ", dto.dtoType);
            break;
    }
}


function getAbsoluteScreenPoint(percentX: number, percentY: number): Point {
    const currentScreen = RtcSession.currentScreen;
    console.log("Current Screen: ", currentScreen);
    const width = currentScreen.width * currentScreen.scaleFactor;
    const height = currentScreen.height * currentScreen.scaleFactor;
    const left = currentScreen.left * currentScreen.scaleFactor;
    const top = currentScreen.top * currentScreen.scaleFactor;

    const x = width * percentX + left;
    const y = height * percentY + top;
    return { x: x, y: y };
}