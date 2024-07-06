import * as React from "react";
import { styled } from "@mui/material/styles";
import { HelpOutline } from "@mui/icons-material";
import { Button, Stack, Typography, Tooltip, IconButton, Link } from "@mui/material";

import DeployAgentDialog from "./DeployAgentDialog";
import MsStoreBadge from "./MsStoreBadge";

function Home() {
  const [isAgentDialogOpen, setAgentDialogOpen] = React.useState(false);

  const openAgentDialog = () => {
    setAgentDialogOpen(true);
  };

  const closeAgentDialog = () => {
    setAgentDialogOpen(false);
  };

  return (
    <Stack sx={{ textAlign: "center" }}>
      <Typography variant="h4" color="success.main" mt={3} mb={1}>
        Viewer
      </Typography>

      <Typography variant="h6">Store Links</Typography>
      <ButtonWrapper>
        <MsStoreBadge
          productid="9NS914B8GR04"
          window-mode="full"
          theme="dark"
          language="en-us"
          animation="on"
        ></MsStoreBadge>
      </ButtonWrapper>

      <ButtonWrapper>
        <div>
          <a href="https://play.google.com/store/apps/details?id=dev.jaredg.controlr.viewer" target="_blank">
            <PlayStoreImg src="/assets/GetItOnGooglePlay.png" />
          </a>

        </div>
        <div>
          <Link href="https://play.google.com/apps/testing/dev.jaredg.controlr.viewer" target="_blank">
            Join the Beta
          </Link>
        </div>
      </ButtonWrapper>

      <Typography variant="h6">Windows 10/11</Typography>
      <ButtonWrapper>
        <Button
          variant="outlined"
          href="/downloads/ControlR.Viewer.msix"
          target="_blank"
        >
          MSIX
        </Button>
      </ButtonWrapper>

      <Typography variant="h6" className="mt-10">
        Android
      </Typography>
      <ButtonWrapper>
        <Button
          variant="outlined"
          href="/downloads/ControlR.Viewer.apk"
          target="_blank"
        >
          APK
        </Button>
      </ButtonWrapper>

      <Typography variant="h4" color="success.main" mt={3} mb={1}>
        Agent
        <Tooltip title="Agent Deployment">
          <AgentHelpButton onClick={openAgentDialog}>
            <HelpOutline />
          </AgentHelpButton>
        </Tooltip>
        <DeployAgentDialog
          isOpen={isAgentDialogOpen}
          onClose={closeAgentDialog}
        />
      </Typography>

      <ButtonWrapper>
        <Button
          variant="outlined"
          href="/downloads/win-x86/ControlR.Agent.exe"
          target="_blank"
        >
          Windows
        </Button>
      </ButtonWrapper>

      <ButtonWrapper>
        <Button
          variant="outlined"
          href="/downloads/linux-x64/ControlR.Agent"
          target="_blank"
        >
          Ubuntu
        </Button>
      </ButtonWrapper>
    </Stack>
  );
}

const ButtonWrapper = styled("div")({
  marginTop: "0.5rem",
  marginBottom: "1.5rem",
});

const AgentHelpButton = styled(IconButton)({
  position: "absolute",
  transform: "translateX(5px)",
});

const PlayStoreImg = styled("img")({
  width: "200px",
  height: "auto",
})

export default Home;
