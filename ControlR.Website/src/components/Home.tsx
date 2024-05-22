import * as React from "react";
import { styled } from "@mui/material/styles";
import { HelpOutline } from "@mui/icons-material";
import {
  Button,
  Link,
  Stack,
  Typography,
  Tooltip,
  IconButton,
} from "@mui/material";
import CertificateInstructionsDialog from "./CertificateInstructionsDialog";
import DeployAgentDialog from "./DeployAgentDialog";

function Home() {
  const [isCertificateDialogOpen, setCertificateDialogOpen] =
    React.useState(false);
  const [isAgentDialogOpen, setAgentDialogOpen] = React.useState(false);

  const openCertificateDialog = () => {
    setCertificateDialogOpen(true);
  };

  const closeCertificateDialog = () => {
    setCertificateDialogOpen(false);
  };

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

      <Typography variant="h6">Windows 10/11</Typography>
      <ButtonWrapper>
        <CertificateInfoWrapper>
          <Link href="/downloads/ControlR.Viewer.cer" target="_blank">
            Certificate
          </Link>
          <Tooltip title="Certificate Instructions">
            <CertificateHelpButton onClick={openCertificateDialog}>
              <HelpOutline />
            </CertificateHelpButton>
          </Tooltip>

          <CertificateInstructionsDialog
            isOpen={isCertificateDialogOpen}
            onClose={closeCertificateDialog}
          />
        </CertificateInfoWrapper>
        <div>
          <Button
            variant="outlined"
            href="/downloads/ControlR.Viewer.msix"
            target="_blank"
          >
            MSIX
          </Button>
        </div>
      </ButtonWrapper>

      <Typography variant="h6">Android</Typography>
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

const CertificateInfoWrapper = styled("div")({
  marginBottom: "1rem",
});

const CertificateHelpButton = styled(IconButton)({
  position: "absolute",
  transform: "translate(5px, -25%)",
});

const AgentHelpButton = styled(IconButton)({
  position: "absolute",
  transform: "translateX(5px)",
});

export default Home;
