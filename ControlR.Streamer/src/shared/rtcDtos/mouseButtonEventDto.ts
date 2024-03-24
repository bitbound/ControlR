declare interface MouseButtonEventDto extends BaseDto {
    isPressed: boolean;
    button: number;
    percentX: number;
    percentY: number;
}