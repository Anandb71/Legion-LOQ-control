# Broker Install Policy

The elevated read broker may launch only as a sibling of the unelevated client. That
layout is not enough for a public package. A production install must also prove that the
directory is administrator-protected and that the broker is Authenticode-signed.

## Placement

`LegionLoqControl.Broker.exe` must sit in the same directory as the client that launches
it. A path outside that directory is rejected before UAC.

## Directory protection

The install directory is protected only when:

- the owner is `BUILTIN\Administrators`, `NT AUTHORITY\SYSTEM`, or TrustedInstaller; and
- every grant that can write, delete, change permissions, or take ownership belongs to
  those same identities.

Users, Authenticated Users, Everyone, or the installing account must not have write
access. A typical `%LOCALAPPDATA%` or repository `bin` directory is therefore a
development location, not a production install.

## Signature

Production mode requires a Signed broker file. The current inspector treats a readable
Authenticode signer certificate as Signed; it does not run WinVerifyTrust chain or catalog
validation. Full signature verification remains a release gate. An unsigned sibling is
allowed only in development mode so local `state-elevated` validation can continue. An
invalid signature blocks every mode.

## Modes

The client reads `LEGIONLOQ_BROKER_INSTALL_MODE`.

| Mode | Unsigned sibling in a user-writable directory | Signed sibling under administrator ACLs |
| --- | --- | --- |
| `development` (default) | Allowed for one read-only UAC probe | Allowed |
| `production` | Refused as `broker_install_unprotected` | Allowed |

Production mode never prompts for elevation when the install is unsigned or user-writable.

## Current artifact classes

- Local development builds keep the unsigned sibling broker and stay in development mode.
- The broker-free preview artifact has no broker file and reports `broker_not_found`.
- The public 0.3.0 GitHub Release zip includes the unsigned sibling broker and stays in
  development mode. It is not a production package.
- A production package that includes the broker remains blocked until signing, protected
  installation ACLs, and the rest of the [release gates](RELEASING.md) exist.
