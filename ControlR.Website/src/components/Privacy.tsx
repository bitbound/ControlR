import { Link, Stack, Typography } from "@mui/material";

function Privacy() {
  return (
    <Stack mt={5}>
      <Typography
        variant="h4"
        color="secondary"
        textAlign={"center"}
        gutterBottom
      >
        Privacy Policy
      </Typography>
      <Stack maxWidth={"sm"} alignSelf={"center"}>
        <Typography variant="body1" mb={2}>
          ControlR does not gather or store any user or device data. The server
          does not have a database in which to store such data. The server
          merely relays messages between users and devices based on public keys.
          All settings and other data is stored locally on devices.
        </Typography>
        <Typography variant="body1" mb={2}>
          There is no intentional effort made to gather user data in any way.
        </Typography>
        <Typography variant="body1" mb={2}>
          The only possible exceptions to the above are as follows:
          <ul>
            <li>
              Standard ASP.NET Core logs are written to disk and kept for 7
              days. These logs may contain IP addresses, although there is no
              effort made to log them intentionally.
            </li>
            <li>
              The publicly-available servers hosted at controlr.app use Twilio
              and Metered for globally-distributed WebRTC TURN servers to relay
              video. You can see their privacy policies&nbsp;
              <Link href="https://www.twilio.com/en-us/legal/privacy">
                here
              </Link>
              &nbsp;and&nbsp;
              <Link href="https://www.metered.ca/privacy">here</Link>,
              respectively.
            </li>
            <li>
              A feature may be offered in the future where users can revoke
              their key in the event of it being compromised. This would
              function similarly to password reset in a standard user-based
              system. A copy of the revoked public key would need to be stored
              on the server. This key cannot be used to identify a person,
              however.
            </li>
          </ul>
        </Typography>
      </Stack>
    </Stack>
  );
}

export default Privacy;
