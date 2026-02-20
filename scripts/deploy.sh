#!/usr/bin/env bash
set -euo pipefail

APP_NAME="openmu-server"
DB_NAME="openmu-db"
REGION="mia"
FIRST_DEPLOY="${1:-false}"

log() { echo "==> $*"; }

check_fly_cli() {
  if ! command -v fly &>/dev/null; then
    echo "Error: flyctl not installed. Install from https://fly.io/docs/hands-on/install-flyctl/"
    exit 1
  fi
  if ! fly auth whoami &>/dev/null; then
    echo "Error: Not logged in. Run 'fly auth login' first."
    exit 1
  fi
}

create_postgres() {
  if fly postgres list 2>/dev/null | grep -q "$DB_NAME"; then
    log "Postgres cluster '$DB_NAME' already exists, skipping."
    return
  fi

  log "Creating Postgres cluster..."
  fly postgres create \
    --name "$DB_NAME" \
    --region "$REGION" \
    --initial-cluster-size 1 \
    --vm-size shared-cpu-1x \
    --volume-size 1
}

launch_app() {
  if fly apps list 2>/dev/null | grep -q "$APP_NAME"; then
    log "App '$APP_NAME' already exists, skipping launch."
    return
  fi

  log "Launching app (no deploy yet)..."
  fly launch --no-deploy --name "$APP_NAME" --region "$REGION" --copy-config
}

configure_secrets() {
  log "Fetching Postgres credentials..."
  local db_password
  db_password=$(fly postgres connect -a "$DB_NAME" -c "SHOW password" 2>/dev/null || true)

  if [ -z "$db_password" ]; then
    log "Could not auto-fetch password. Set secrets manually:"
    echo "  fly secrets set -a $APP_NAME \\"
    echo "    DB_HOST=\"$DB_NAME.flycast\" \\"
    echo "    DB_ADMIN_USER=\"postgres\" \\"
    echo "    DB_ADMIN_PW=\"<your-password>\""
    return
  fi

  fly secrets set -a "$APP_NAME" \
    DB_HOST="$DB_NAME.flycast" \
    DB_ADMIN_USER="postgres" \
    DB_ADMIN_PW="$db_password"
}

allocate_ip() {
  local existing_v4
  existing_v4=$(fly ips list -a "$APP_NAME" 2>/dev/null | grep "v4" || true)

  if [ -n "$existing_v4" ]; then
    log "IPv4 already allocated:"
    echo "$existing_v4"
    return
  fi

  log "Allocating dedicated IPv4..."
  fly ips allocate-v4 -a "$APP_NAME"
}

attach_database() {
  log "Attaching Postgres to app..."
  fly postgres attach "$DB_NAME" -a "$APP_NAME" 2>/dev/null || \
    log "Database already attached or attach failed (may need manual setup)."
}

deploy() {
  if [ "$FIRST_DEPLOY" = "true" ] || [ "$FIRST_DEPLOY" = "--reinit" ]; then
    log "First deploy with -reinit flag (creates schema + seed data)..."
    fly deploy -a "$APP_NAME" \
      --build-arg CMD_ARGS="-autostart -reinit"
  else
    log "Deploying..."
    fly deploy -a "$APP_NAME"
  fi
}

print_summary() {
  log "Deploy complete!"
  echo ""
  echo "--- Summary ---"
  fly ips list -a "$APP_NAME"
  echo ""
  echo "Admin Panel: https://$APP_NAME.fly.dev"
  echo "Connect Server: <IPv4>:44405"
  echo "Game Server:    <IPv4>:55901"
  echo "Chat Server:    <IPv4>:55980"
  echo ""
  echo "Next steps:"
  echo "  1. Open the admin panel and set the public IPv4 on the Game Server"
  echo "  2. Configure your mobile client with the IPv4 address"
  echo "  3. Run scripts/verify.sh to confirm everything works"
}

main() {
  log "Starting OpenMU deployment to Fly.io"
  check_fly_cli
  create_postgres
  launch_app
  configure_secrets
  allocate_ip
  attach_database
  deploy
  print_summary
}

main
