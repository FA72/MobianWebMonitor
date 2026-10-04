# Porting to Other Devices

Mobian Web Monitor is designed for OnePlus 6 but supports generic Linux ARM64 devices.

## Hardware Profile Detection

The app auto-detects device type at startup via `HardwareProfileFactory`:

1. **OnePlus 6**: Detected by the presence of `bq27411-0` and `pmi8998-charger` in `/sys/class/power_supply/`
2. **Generic Linux**: Falls back to scanning available power supply devices and thermal zones

## Replacing OnePlus 6-Specific Paths

If your device has different battery/charger names:

1. Create a new class implementing `IHardwareProfile` (see `OnePlus6HardwareProfile.cs`)
2. Add detection logic in `HardwareProfileFactory.Create()`
3. Set the correct `BatteryGaugeName` and `ChargerName` for your device

### Finding your power_supply names

```bash
ls /sys/class/power_supply/
# Example output: BAT0  AC0

cat /sys/class/power_supply/BAT0/type
# Should output: Battery

cat /sys/class/power_supply/AC0/type
# Should output: Mains or USB
```

### Finding thermal zones

```bash
ls /sys/class/thermal/
for tz in /sys/class/thermal/thermal_zone*/type; do
    echo "$(dirname $tz): $(cat $tz)"
done
```

## Disabling Docker Tab

If Docker is not installed on the target device, the Docker tab will show "No containers found or Docker is unavailable." No code changes needed — `DockerCollector` handles connection failures gracefully.

To hide the tab entirely, remove the Docker tab button and case from `Dashboard.razor`.

## Disabling D-Bus / Systemd Service Monitoring

If the device doesn't use systemd or doesn't run `battery-limiter.service`, the service status will show N/A. The `SystemdServiceCollector` reads its state via `busctl` over the system D-Bus socket and handles connection failures gracefully. The monitor only observes this service; charge control is provided by the separate [battery-limiter](https://github.com/FA72/battery-limiter) project.

## Host Path Configuration

When running in Docker, host paths are mounted with prefixes. Configure via environment variables:

```env
HostPaths__ProcRoot=/host/proc    # Default: /proc
HostPaths__SysRoot=/host/sys      # Default: /sys
```

For local development on the host (without Docker), use default paths (`/proc`, `/sys`).

## Storage and Architecture

History and cookie data-protection keys use the fixed paths `/data/history` and `/data/protection-keys`. Create these directories with write access for the application user when running without Docker. The supplied Compose file persists them under `deploy/phone/volumes/` for UID/GID `1000:1000`.

The published container targets `linux/arm64`. Builds for other architectures require changing the publish workflow and using a matching .NET runtime image. Disk metrics use `DriveInfo` from the application's filesystem view, so a container does not report every host mount.
