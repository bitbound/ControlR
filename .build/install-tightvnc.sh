#!/bin/bash

if [ "$EUID" -ne 0 ]
  then echo "Please run as root (i.e. with sudo)."
  exit
fi

read -p "Enter username to use for VNC server: " Username
read -p "Enter a display to use: " DisplayNum
read -p "Enter password to use for VNC server: " VncPassword

apt update
apt install -y xfce4 xfce4-goodies tightvncserver
mkdir -p /home/$Username/.vnc/passwd
echo $VncPassword | vncpasswd -f > /home/$Username/.vnc/passwd
chown $Username /home/$Username/.vnc/passwd
chmod 700 /home/$Username/.vnc/passwd

xstartup="#!/bin/bash
xrdb \$HOME/.Xresources
startxfce4 &"

echo "$xstartup" > /home/$Username/.vnc/xstartup

serviceConfig="[Unit]
Description=Start TightVNC server at startup
After=syslog.target network.target

[Service]
Type=forking
User=$Username
Group=$Username
WorkingDirectory=/home/$Username

PIDFile=/home/$Username/.vnc/%H:%i.pid
ExecStartPre=-/usr/bin/vncserver -kill :%i > /dev/null 2>&1
ExecStart=/usr/bin/vncserver -depth 24 -geometry 1280x800 -localhost :%i
ExecStop=/usr/bin/vncserver -kill :%i

[Install]
WantedBy=multi-user.target"

echo "$serviceConfig" > /etc/systemd/system/vncserver@.service

systemctl daemon-reload
systemctl enable vncserver@$DisplayNum
systemctl restart vncserver@$DisplayNum

echo Install complete. 