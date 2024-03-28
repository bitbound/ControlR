export function parseBoolean(value: string) : boolean | null {
    if (/^\s*true\s*$/i.test(value)) {
        return true;
    }

    if (/^\s*false\s*$/i.test(value)) {
        return false;
    }

    return null;
}