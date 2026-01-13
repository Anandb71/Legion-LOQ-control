# Safety Philosophy & Mechanisms

Legion + LOQ Control is built on a "Safety First" philosophy. We prioritize hardware integrity over feature availability. This document outlines the mechanisms we use to ensure safety.

## Core Principles

1.  **Read-Only Default**: The application starts in a read-only state. No write operations are possible without explicit user intent.
2.  **Explicit Intent**: Write operations require specific flags (e.g., `--set-conservation-mode`). There are no "implicit" writes or background auto-tuning.
3.  **Fail Closed**: If we cannot verify the state of the hardware or the parameters of a request, we do nothing.

## Safety Mechanisms

### 1. Global Write Lock
We implement a **Global Write Lock** (`src/core/safety/guards.rs`).
-   **State**: The application processes default to `Locked`.
-   **Mechanism**: Code pathways that modify hardware must explicitly request a write token. This check is centralized and atomic.
-   **Enforcement**: Any attempt to call a write function without the lock being unlocked results in an immediate error.

### 2. Dry Run Capability
Every write feature supports a `--dry-run` flag.
-   **Start**: The process begins.
-   **Read**: The current state is read.
-   **Compare**: The intended change is compared against the current state.
-   **Report**: The tool reports what *would* happen.
-   **Stop**: The process exits before any write instruction is sent to the hardware.

### 3. Read-Before-Write
We never "blind set" a value.
-   We verify the platform is supported.
-   We read the current value from the hardware/WMI.
-   We validate that the new value is within safe, manufacturer-supported bounds.

### 4. Standard Windows APIs
we do not use direct memory access (DMA), undocumented EC registers, or kernel-level hacks.
-   **Methods**: We use standard WMI (Windows Management Instrumentation) calls.
-   **Backend**: We use PowerShell or standard Windows APIs, which are auditable and respected by the OS security model.

## Supported Hardware
Strict model detection is enforced.
-   **Allowed**: Lenovo "Legion" and "LOQ" series.
-   **Blocked**: IdeaPad, ThinkPad, and non-Lenovo devices.
-   **Mechanism**: If `Manufacturer != Lenovo` OR `Series` is unknown, the app runs in **Read-Only Mode** or exits with a supported error.
