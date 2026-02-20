#!/usr/bin/env bash
set -euo pipefail

APP_NAME="lara-mu"
PORTS=(44405 55901 55980)
PORT_NAMES=("Connect Server" "Game Server" "Chat Server")

log() { echo "==> $*"; }
pass() { echo "  [PASS] $*"; }
fail() { echo "  [FAIL] $*"; }

get_ipv4() {
  fly ips list -a "$APP_NAME" 2>/dev/null \
    | grep "v4" \
    | awk '{print $2}' \
    | head -1
}

check_app_status() {
  log "Checking app status..."
  local status
  status=$(fly status -a "$APP_NAME" 2>/dev/null)

  if echo "$status" | grep -q "running"; then
    pass "App is running"
  else
    fail "App is not running"
    echo "$status"
    return 1
  fi
}

check_tcp_ports() {
  local ip="$1"
  log "Checking TCP ports on $ip..."

  for i in "${!PORTS[@]}"; do
    local port="${PORTS[$i]}"
    local name="${PORT_NAMES[$i]}"

    if nc -zw5 "$ip" "$port" 2>/dev/null; then
      pass "$name (port $port) is reachable"
    else
      fail "$name (port $port) is NOT reachable"
    fi
  done
}

check_admin_panel() {
  log "Checking admin panel..."
  local url="https://$APP_NAME.fly.dev"
  local http_code
  http_code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$url" 2>/dev/null || echo "000")

  if [ "$http_code" -ge 200 ] && [ "$http_code" -lt 400 ]; then
    pass "Admin panel responds (HTTP $http_code) at $url"
  else
    fail "Admin panel not responding (HTTP $http_code) at $url"
  fi
}

main() {
  log "OpenMU Post-Deploy Verification"
  echo ""

  check_app_status

  local ipv4
  ipv4=$(get_ipv4)
  if [ -z "$ipv4" ]; then
    fail "No IPv4 found. Run: fly ips allocate-v4 -a $APP_NAME"
    exit 1
  fi
  pass "IPv4 address: $ipv4"
  echo ""

  check_tcp_ports "$ipv4"
  echo ""

  check_admin_panel
  echo ""

  log "Verification complete."
}

main
