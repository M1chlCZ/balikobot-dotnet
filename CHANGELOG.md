## [0.2.1] - 2026-11-16

### Fixed

- `AddPackageAsync` reports a matching package's `carrier_error` as
  `BalikobotError.CarrierOutage`, including in HTTP error responses.
  `RequiresReconciliation` covers it, so reconcile with `OverviewAsync`
  before another attempt.
- `OverviewAsync` requires an explicit package array. A missing or null array
  reports `BalikobotError.InvalidResponse` instead of an empty list.

## [0.2.0] - 2026-10-13

### Added

- Batch label export through `DownloadBatchLabelsAsync`. It requests fresh
  labels for 1 to 1000 package ids, follows the returned label URL and
  validates the body. A single package id returns a PDF or ZPL body; several
  package ids return a ZIP archive.
- Typed tracking webhook parsing through `TrackingNotification` and
  `ParseTrackingNotifications`. The parser accepts a single status-change
  object or an array of objects and rejects malformed input with
  `BalikobotError.InvalidResponse`.

## 0.1.1

- Documentation and internal cleanup.

## 0.1.0

- Parity port of balikobot-go v0.2.0.
- The `BalikobotError` classifications.
- The typed carrier, currency and country codes.
- The account mode with the WHOAMI check and the five-minute cache.
- The JSON and label response limits.
- The label host allowlist.
