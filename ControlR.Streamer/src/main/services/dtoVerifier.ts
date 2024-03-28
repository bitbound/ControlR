import appState from "./appState";
import { writeLog } from "./logger";
import { verify, createPublicKey } from "crypto";

export function verifyDto(
    payload: Uint8Array,
    signature: Uint8Array,
    publicKey: Uint8Array,
    publicKeyPem: string
): boolean {
    console.log("Verifying DTO signature.");

    const publicKeyBase64 = Buffer.from(publicKey).toString('base64');

    writeLog(`Comparing public key ${publicKeyBase64}`);

    if (publicKeyBase64 != appState.authorizedKey) {
        writeLog("Public key from DTO does not match the authorized key.", 'Error');
        return false;
    }

    const publicKeyObject = createPublicKey({
        key: publicKeyPem,
        type: "pkcs1",
        format: "pem"
    });

    const result = verify(
        "RSA-SHA512",
        payload,
        publicKeyObject,
        signature);

    if (!result) {
        writeLog("Public key from DTO does not pass verification!", 'Error');
        return false;
    }

    console.info("DTO passed signature verification.");

    return true;
}
