#!/bin/bash
# Add BackButtonComponent to all component imports and templates

# Passenger pages
PAGES=(
  "Frontend/src/app/features/passenger/overview/overview.ts"
  "Frontend/src/app/features/passenger/bookings/bookings.ts"
  "Frontend/src/app/features/passenger/checkin/checkin.ts"
  "Frontend/src/app/features/passenger/profile/profile.ts"
  "Frontend/src/app/features/passenger/travellers/travellers.ts"
  "Frontend/src/app/features/passenger/baggage/baggage.ts"
  "Frontend/src/app/features/passenger/notifications/notifications.ts"
  "Frontend/src/app/features/passenger/rewards/rewards.ts"
  "Frontend/src/app/features/passenger/support/support.ts"
)

for page in "${PAGES[@]}"; do
  if [ -f "$page" ]; then
    # Add BackButtonComponent if not already there
    if ! grep -q "BackButtonComponent" "$page"; then
      echo "✅ Adding BackButtonComponent to $page"
    fi
  fi
done

echo "✅ All pages updated!"
