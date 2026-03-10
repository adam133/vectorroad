#!/usr/bin/env bash
# .devcontainer/setup.sh
# Runs once after the Codespace container is created (postCreateCommand).
# Installs Unity Hub and the runtime libraries it needs on Ubuntu 24.04.
set -euo pipefail

echo "==> Installing Unity Hub..."

# 1. Add the Unity Hub apt repository and signing key
wget -qO - https://hub.unity3d.com/linux/keys/public \
  | gpg --dearmor \
  | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg > /dev/null

echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] \
https://hub.unity3d.com/linux/repos/deb stable main" \
  | sudo tee /etc/apt/sources.list.d/unityhub.list > /dev/null

# 2. Install Unity Hub together with libraries required for headless CLI use
sudo apt-get update -q
sudo apt-get install -y \
  unityhub \
  libgbm1 \
  libasound2t64 \
  libgtk-3-0 \
  libxss1 \
  libnss3 \
  xvfb

echo "==> Unity Hub installed: $(unityhub --version 2>/dev/null || echo 'version unavailable in shell – use xvfb-run unityhub')"

# 3. Accept the Unity Hub End-User License Agreement non-interactively
#    so 'unityhub --headless ...' commands work without a prompt.
mkdir -p "${HOME}/.config/Unity Hub"
cat > "${HOME}/.config/Unity Hub/eulaAccepted" <<'EOF'
{
  "version": 1,
  "accepted": true
}
EOF

echo ""
echo "==> Setup complete."
echo "    Run Unity Hub CLI commands with:"
echo "      xvfb-run unityhub -- --headless help"
echo "    Unity license secrets (UNITY_LICENSE / UNITY_EMAIL / UNITY_PASSWORD)"
echo "    are available as environment variables when set in repository Codespace secrets."
