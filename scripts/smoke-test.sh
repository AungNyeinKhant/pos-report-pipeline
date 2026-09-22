#!/usr/bin/env bash
#
# End-to-end check against a deployed stack: submits a POS report, then polls the status
# endpoint until the calculator has produced a result.
#
# Usage:
#   ./scripts/smoke-test.sh                       # reads the API URL from CloudFormation
#   ./scripts/smoke-test.sh https://xxxx.execute-api.ap-southeast-1.amazonaws.com/prod/
#
# Requires: curl, and the AWS CLI when the API URL is not given.

set -euo pipefail

STACK_NAME="${STACK_NAME:-PosReportPipeline}"
MESSAGE="${MESSAGE:-POS/UL204.FR RGN/TO BKK/041205/N1642.3E09612.5/450/12500/2800}"
ATTEMPTS="${ATTEMPTS:-15}"

api_url="${1:-}"
if [[ -z "$api_url" ]]; then
  echo "Reading the API URL from the ${STACK_NAME} stack outputs..."
  api_url=$(aws cloudformation describe-stacks \
    --stack-name "$STACK_NAME" \
    --query "Stacks[0].Outputs[?OutputKey=='ApiUrl'].OutputValue" \
    --output text)
fi

api_url="${api_url%/}"

echo "POST ${api_url}/pos-reports"
echo "  ${MESSAGE}"

response=$(curl -sS -X POST "${api_url}/pos-reports" \
  -H "Content-Type: text/plain" \
  --data-binary "$MESSAGE")

echo "  -> ${response}"

# The response is small and predictable, so a grep keeps this script dependency-free.
# `|| true` keeps `set -e` from killing the script here: a response with no flight ID is
# handled by the check below, which reports it properly.
flight_id=$(printf '%s' "$response" | grep -o '"flightId": *"[^"]*"' | head -1 | cut -d'"' -f4 || true)

if [[ -z "$flight_id" ]]; then
  echo "No flight ID in the response; stopping." >&2
  exit 1
fi

echo
echo "Polling GET ${api_url}/status/${flight_id}"

for attempt in $(seq 1 "$ATTEMPTS"); do
  status=$(curl -sS "${api_url}/status/${flight_id}")
  echo "  attempt ${attempt}: ${status}"

  # The calculated fields only appear once the calculator has written a result.
  if printf '%s' "$status" | grep -q 'remainingFlightTimeMinutes'; then
    echo
    echo "Calculation complete for ${flight_id}."
    exit 0
  fi

  sleep 2
done

echo
echo "Gave up after ${ATTEMPTS} attempts. Check the Lambda logs and the dead-letter queues." >&2
exit 1
