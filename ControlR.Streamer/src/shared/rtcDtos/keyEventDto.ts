declare interface KeyEventDto extends BaseDto {
    isPressed: boolean;
    keyCode: string;
    shouldRelease: boolean;
}