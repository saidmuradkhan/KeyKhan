# Security policy

PassIn guards passwords, so security reports get priority over everything else.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private reporting instead:
**[Report a vulnerability](https://github.com/saidmuradkhan/PassIn/security/advisories/new)**.

Include what you found, the steps to reproduce it and the version you tested (Settings → Updates shows it).
You can expect a first answer within 72 hours. Once a fix is released you will be credited in the advisory,
unless you prefer not to be.

## Supported versions

Only the [latest release](https://github.com/saidmuradkhan/PassIn/releases/latest) receives security fixes.
Installed copies update themselves, so a fix reaches users within hours of its release.

## Scope

In scope:

- the vault file format and its encryption (`src/PassIn.Native`, `src/PassIn.Vault`)
- anything that could expose a secret: clipboard handling, auto-type, memory handling, the recovery key, "Remember me"
- the updater, which downloads and runs the installer after checking its SHA-256 digest

Out of scope, because no local password manager can defend against them:

- an attacker who already runs code as your Windows user while the vault is unlocked
- an attacker with administrator rights on the machine
- physical access to an unlocked, unattended computer

The [security design](README.md#security-design) section of the README describes how PassIn protects your data and
its known limitations.
